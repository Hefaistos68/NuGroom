using NuGroom.Configuration;
using NuGroom.Nuget;

namespace NuGroom.Workflows
{
	/// <summary>
	/// Executes package sync operations in local mode by updating files directly on disk.
	/// </summary>
	internal static class LocalSyncWorkflow
	{
		private readonly record struct FileSourceKey(string FilePath, PackageSourceKind SourceKind);

		/// <summary>
		/// Runs all configured sync operations against already scanned local references.
		/// </summary>
		/// <param name="parseResult">Parse result containing sync options and feed settings.</param>
		/// <param name="references">Scanned package references from local mode.</param>
		public static async Task ExecuteAsync(ParseResult parseResult, List<PackageReferenceExtractor.PackageReference> references)
		{
			ArgumentNullException.ThrowIfNull(parseResult);
			ArgumentNullException.ThrowIfNull(references);

			if (parseResult.SyncConfigs.Count == 0)
			{
				return;
			}

			var effectiveUpdateConfig = parseResult.UpdateConfig ?? new UpdateConfig();
			var resolver = new NuGetPackageResolver(parseResult.Feeds, parseResult.FeedAuth);

			foreach (var syncConfig in parseResult.SyncConfigs)
			{
				var targetVersion = await ResolveTargetVersionAsync(syncConfig, resolver);

				if (string.IsNullOrWhiteSpace(targetVersion))
				{
					continue;
				}

				var fileUpdates = BuildFileUpdates(references, syncConfig.PackageName, targetVersion);

				if (fileUpdates.Count == 0)
				{
					ConsoleWriter.Out.WriteLine($"No local references found that require syncing for '{syncConfig.PackageName}'.");
					continue;
				}

				if (effectiveUpdateConfig.DryRun)
				{
					PrintDryRun(syncConfig.PackageName, targetVersion, fileUpdates);
					continue;
				}

				ApplyLocalSync(fileUpdates, syncConfig.PackageName, targetVersion);
			}
		}

		private static List<FileUpdate> BuildFileUpdates(
			List<PackageReferenceExtractor.PackageReference> references,
			string packageName,
			string targetVersion)
		{
			var matchingReferences = references
				.Where(r => r.PackageName.Equals(packageName, StringComparison.OrdinalIgnoreCase))
				.Where(r => !string.IsNullOrWhiteSpace(r.Version))
				.Where(r => !r.Version!.Equals(targetVersion, StringComparison.OrdinalIgnoreCase))
				.ToList();

			var fileUpdates = new List<FileUpdate>();
			var groupedByFile = matchingReferences
				.GroupBy(r => new FileSourceKey(r.ProjectPath, r.SourceKind));

			foreach (var fileGroup in groupedByFile)
			{
				var updates = fileGroup
					.Select(r => r.Version)
					.Where(v => !string.IsNullOrWhiteSpace(v))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.Select(v => new PackageUpdate(packageName, v!, targetVersion))
					.ToList();

				if (updates.Count == 0)
				{
					continue;
				}

				var fileUpdate = new FileUpdate(fileGroup.Key.FilePath, fileGroup.Count(), updates, fileGroup.Key.SourceKind);
				fileUpdates.Add(fileUpdate);
			}

			return fileUpdates;
		}

		private static async Task<string?> ResolveTargetVersionAsync(SyncConfig syncConfig, NuGetPackageResolver resolver)
		{
			if (!string.IsNullOrWhiteSpace(syncConfig.TargetVersion))
			{
				return syncConfig.TargetVersion;
			}

			ConsoleWriter.Out.WriteLine($"Resolving latest version for {syncConfig.PackageName}...");
			var packageInfo = await resolver.ResolvePackageAsync(syncConfig.PackageName);

			if (string.IsNullOrWhiteSpace(packageInfo.LatestVersion))
			{
				ConsoleWriter.Out.Red()
					.WriteLine($"Error: Could not resolve latest version for '{syncConfig.PackageName}' from configured feeds.")
					.ResetColor();

				return null;
			}

			ConsoleWriter.Out.WriteLine($"Resolved latest version: {packageInfo.LatestVersion}");

			return packageInfo.LatestVersion;
		}

		private static void PrintDryRun(string packageName, string targetVersion, List<FileUpdate> fileUpdates)
		{
			ConsoleWriter.Out
				.WriteLine()
				.WriteLine(new string('=', 80))
				.WriteLine($"LOCAL SYNC (dry-run): {packageName} -> {targetVersion}")
				.WriteLine(new string('=', 80));

			foreach (var fileUpdate in fileUpdates)
			{
				foreach (var update in fileUpdate.Updates)
				{
					ConsoleWriter.Out.WriteLine($"  {fileUpdate.ProjectPath}: {update.OldVersion} -> {update.NewVersion}");
				}
			}
		}

		private static void ApplyLocalSync(List<FileUpdate> fileUpdates, string packageName, string targetVersion)
		{
			ConsoleWriter.Out
				.WriteLine()
				.WriteLine(new string('=', 80))
				.WriteLine($"LOCAL SYNC: {packageName} -> {targetVersion}")
				.WriteLine(new string('=', 80));

			var updatedFiles = 0;

			foreach (var fileUpdate in fileUpdates)
			{
				var filePath = fileUpdate.ProjectPath;

				if (!File.Exists(filePath))
				{
					ConsoleWriter.Out.Yellow().WriteLine($"  Warning: File not found: {filePath}").ResetColor();
					continue;
				}

				var content = File.ReadAllText(filePath);

				if (string.IsNullOrWhiteSpace(content))
				{
					continue;
				}

				var updatedContent = UpdateWorkflow.ApplyUpdatesBySourceKind(content, fileUpdate);

				if (updatedContent == content)
				{
					continue;
				}

				File.WriteAllText(filePath, updatedContent);
				updatedFiles++;
				ConsoleWriter.Out.Green().WriteLine($"  Updated: {filePath}").ResetColor();
			}

			ConsoleWriter.Out.WriteLine($"Local sync complete: {updatedFiles} file(s) updated.");
		}
	}
}
