using NuGroom.Configuration;
using NuGroom.Nuget;

namespace NuGroom.Workflows
{
	/// <summary>
	/// Applies package reference updates directly to local files on disk.
	/// In dry-run mode, shows planned updates without modifying any files.
	/// </summary>
	internal static class LocalUpdateWorkflow
	{
		/// <summary>
		/// Builds update plans from scanned references and either applies them to local files
		/// or prints a dry-run summary, depending on the update configuration.
		/// </summary>
		/// <param name="references">All scanned package references with resolved NuGet metadata.</param>
		/// <param name="updateConfig">Configuration controlling update scope and dry-run mode.</param>
		public static void Execute(
			List<PackageReferenceExtractor.PackageReference> references,
			UpdateConfig updateConfig)
		{
			if (!updateConfig.IsRequested)
			{
				return;
			}

			ConsoleWriter.Out.WriteLine("Preparing local update workflow.");

			var updater = new PackageReferenceUpdater(updateConfig.Scope, updateConfig.PinnedPackages, updateConfig.SourcePackagesOnly);
			var plans = updater.BuildUpdatePlans(references);

			PackageReferenceUpdater.PrintUpdateSummary(plans, updateConfig, isLocalMode: true);

			if (updateConfig.DryRun || plans.Count == 0)
			{
				return;
			}

			ApplyLocalUpdates(plans, updateConfig);
		}

		/// <summary>
		/// Applies all planned updates by reading each file from disk, modifying the content,
		/// and writing the result back. Files that produce no effective changes are skipped.
		/// </summary>
		private static void ApplyLocalUpdates(List<RepositoryUpdatePlan> plans, UpdateConfig updateConfig)
		{
			var totalFilesUpdated = 0;
			var totalUpdatesApplied = 0;

			foreach (var plan in plans)
			{
				ConsoleWriter.Out.WriteLine($"\nApplying updates for: {plan.RepositoryName}");

				foreach (var fileUpdate in plan.FileUpdates)
				{
					try
					{
						var filePath = fileUpdate.ProjectPath;

						if (!File.Exists(filePath))
						{
							ConsoleWriter.Out.Yellow()
								.WriteLine($"  Warning: File not found: {filePath}, skipping.")
								.ResetColor();

							continue;
						}

						var currentContent = File.ReadAllText(filePath);

						if (string.IsNullOrWhiteSpace(currentContent))
						{
							continue;
						}

						var updatedContent = UpdateWorkflow.ApplyUpdatesBySourceKind(currentContent, fileUpdate);

						if (fileUpdate.SourceKind == PackageSourceKind.ProjectFile
							&& updateConfig.VersionIncrement is { IsEnabled: true }
							&& updatedContent != currentContent)
						{
							updatedContent = ProjectVersionIncrementer.ApplyVersionIncrements(updatedContent, updateConfig.VersionIncrement);
						}

						if (updatedContent != currentContent)
						{
							File.WriteAllText(filePath, updatedContent);
							totalFilesUpdated++;
							totalUpdatesApplied += fileUpdate.Updates.Count;

							ConsoleWriter.Out.Green()
								.WriteLine($"  Updated: {filePath} ({fileUpdate.Updates.Count} package(s))")
								.ResetColor();
						}
						else
						{
							ConsoleWriter.Out.WriteLine($"  No effective changes: {filePath}");
						}
					}
					catch (Exception ex)
					{
						ConsoleWriter.Out.Red()
							.WriteLine($"  Error updating {fileUpdate.ProjectPath}: {ex.Message}")
							.ResetColor();
					}
				}
			}

			ConsoleWriter.Out.WriteLine();
			ConsoleWriter.Out.Green()
				.WriteLine($"Local update complete: {totalUpdatesApplied} update(s) applied across {totalFilesUpdated} file(s).")
				.ResetColor();
		}
	}
}
