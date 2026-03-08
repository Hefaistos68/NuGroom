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
		/// sync, local scan, scan + report, or scan + report + update.
		/// </summary>
		/// <param name="args">Command line arguments.</param>
		/// <returns>Zero on success, non-zero on error.</returns>
		static async Task<int> Main(string[] args)
		{
			try
			{
				var parseResult = CommandLineParser.Parse(args);

				// Local mode: Config is null but LocalPaths is populated — no ADO credentials needed
				if (parseResult.LocalPaths is { Count: > 0 })
				{
					return await RunLocalScanPipelineAsync(parseResult);
				}

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
		/// Executes the scan → report → update pipeline for local files and directories (no Azure DevOps connection).
		/// </summary>
		private static async Task<int> RunLocalScanPipelineAsync(ParseResult parseResult)
		{
			var scanResult = await LocalScanWorkflow.ExecuteAsync(parseResult);

			var references = scanResult.References;

			if (references != null && references.Any())
			{
				if (parseResult.SyncConfigs.Count > 0)
				{
					await LocalSyncWorkflow.ExecuteAsync(parseResult, references);

					return 0;
				}

				ReportWorkflow.Execute(references, parseResult);

				if (parseResult.MigrateToCpm)
				{
					LocalCpmMigrationWorkflow.Execute(parseResult, references);
				}
				else if (parseResult.UpdateConfig != null)
				{
					LocalUpdateWorkflow.Execute(references, parseResult.UpdateConfig);
				}
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

				if (parseResult.MigrateToCpm)
				{
					await CpmMigrationWorkflow.ExecuteAsync(
						parseResult.Config!,
						references,
						parseResult.PerProject,
						parseResult.UpdateConfig);
				}
				else if (parseResult.UpdateConfig != null)
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

