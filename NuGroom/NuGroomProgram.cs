using NuGroom.Workflows;

namespace NuGroom
{
	/// <summary>
	/// Application entry point. Delegates to dedicated workflow classes for each pipeline stage.
	/// </summary>
	internal class NuGroomProgram
	{
		/// <summary>
		/// Parses command line arguments and dispatches to the appropriate workflow:
		/// sync, scan + report, or scan + report + update.
		/// </summary>
		/// <param name="args">Command line arguments.</param>
		/// <returns>Zero on success, non-zero on error.</returns>
		static async Task<int> Main(string[] args)
		{
			try
			{
				var parseResult = CommandLineParser.Parse(args);

				if (parseResult.Config == null)
				{
					return 1;
				}

				// Sync workflow is self-contained — skip the full scan/report pipeline
				if (parseResult.SyncConfigs.Count > 0)
				{
					return await RunSyncWorkflowsAsync(parseResult);
				}

				return await RunScanReportUpdatePipelineAsync(parseResult);
			}
			catch (Exception ex)
			{
				ConsoleWriter.Out.Red().WriteLine($"Error: {ex.Message}").ResetColor();
				return 1;
			}
		}

		/// <summary>
		/// Executes all configured sync workflows sequentially
		/// </summary>
		private static async Task<int> RunSyncWorkflowsAsync(ParseResult parseResult)
		{
			foreach (var syncConfig in parseResult.SyncConfigs)
			{
				await SyncWorkflow.ExecuteAsync(
					parseResult.Config!,
					syncConfig,
					parseResult.UpdateConfig,
					parseResult.Feeds,
					parseResult.FeedAuth,
					parseResult.IgnoreRenovate);
			}

			return 0;
		}

		/// <summary>
		/// Executes the full scan → report → update pipeline
		/// </summary>
		private static async Task<int> RunScanReportUpdatePipelineAsync(ParseResult parseResult)
		{
			var scanOptions = ScanOptions.FromParseResult(parseResult);
			var scanResult = await ScanWorkflow.ExecuteAsync(scanOptions);

			var references = scanResult.References;

			if (references != null && references.Any())
			{
				ReportWorkflow.Execute(references, parseResult);

				if (parseResult.UpdateConfig != null)
				{
					await UpdateWorkflow.ExecuteAsync(
						parseResult.Config!,
						references,
						parseResult.UpdateConfig,
						scanResult.RenovateOverrides);
				}
			}

			return 0;
		}
	}
}
