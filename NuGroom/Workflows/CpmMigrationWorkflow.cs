using NuGroom.ADO;
using NuGroom.Configuration;
using NuGroom.Nuget;

using System.Text;

namespace NuGroom.Workflows
{
	/// <summary>
	/// Executes the Central Package Management migration workflow: reads project files,
	/// generates <c>Directory.Packages.props</c>, removes inline versions, and pushes
	/// the result as a feature branch with a pull request.
	/// </summary>
	internal static class CpmMigrationWorkflow
	{
		/// <summary>
		/// Runs the CPM migration for all repositories that contain scanned package references.
		/// </summary>
		/// <param name="config">Azure DevOps connection configuration.</param>
		/// <param name="references">All scanned package references with resolved metadata.</param>
		/// <param name="perProject">
		/// When <c>true</c>, creates a <c>Directory.Packages.props</c> per project instead of per repository.
		/// </param>
		/// <param name="updateConfig">
		/// Optional update configuration for branching, dry-run, and PR settings.
		/// When <c>null</c>, defaults to dry-run mode.
		/// </param>
		public static async Task ExecuteAsync(
			AzureDevOpsConfig config,
			List<PackageReferenceExtractor.PackageReference> references,
			bool perProject,
			UpdateConfig? updateConfig)
		{
			ArgumentNullException.ThrowIfNull(config);
			ArgumentNullException.ThrowIfNull(references);

			var isDryRun = updateConfig?.DryRun ?? true;

			// Filter to only references with explicit versions from project files
			var eligibleRefs = references
				.Where(r => r.SourceKind == PackageSourceKind.ProjectFile && r.Version != null)
				.ToList();

			if (eligibleRefs.Count == 0)
			{
				ConsoleWriter.Out.Yellow().WriteLine("No eligible package references found for CPM migration.").ResetColor();
				return;
			}

			// Group references by repository
			var repoGroups = eligibleRefs
				.GroupBy(r => r.RepositoryName, StringComparer.OrdinalIgnoreCase);

			using var client = new AzureDevOpsClient(config);
			var repositories = await client.GetRepositoriesAsync();
			var repoLookup = repositories.ToDictionary(r => r.Name, r => r, StringComparer.OrdinalIgnoreCase);

			foreach (var repoGroup in repoGroups)
			{
				var repoName = repoGroup.Key;

				if (!repoLookup.TryGetValue(repoName, out var repository))
				{
					ConsoleWriter.Out.Yellow().WriteLine($"Warning: Repository '{repoName}' not found, skipping CPM migration.").ResetColor();
					continue;
				}

				try
				{
					await MigrateRepositoryAsync(client, repository, repoGroup.ToList(), perProject, isDryRun, updateConfig);
				}
				catch (Exception ex)
				{
					ConsoleWriter.Out.Red().WriteLine($"Error migrating repository '{repoName}': {ex.Message}").ResetColor();
				}
			}
		}

		/// <summary>
		/// Migrates a single repository to Central Package Management.
		/// </summary>
		private static async Task MigrateRepositoryAsync(
			AzureDevOpsClient client,
			Microsoft.TeamFoundation.SourceControl.WebApi.GitRepository repository,
			List<PackageReferenceExtractor.PackageReference> references,
			bool perProject,
			bool isDryRun,
			UpdateConfig? updateConfig)
		{
			ConsoleWriter.Out.WriteLine($"\nMigrating repository '{repository.Name}' to Central Package Management...");

			// Resolve source branch
			var sourceBranch = await ResolveSourceBranchAsync(client, repository, updateConfig);

			if (sourceBranch == null)
			{
				return;
			}

			// Read current project file contents from the source branch
			var projectPaths = references.Select(r => r.ProjectPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
			var projectContents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			foreach (var projectPath in projectPaths)
			{
				var content = await client.GetFileContentFromBranchAsync(repository, projectPath, sourceBranch.Value.RefName);

				if (!string.IsNullOrWhiteSpace(content))
				{
					projectContents[projectPath] = content;
				}
				else
				{
					ConsoleWriter.Out.Yellow().WriteLine($"  Warning: Could not read {projectPath}, skipping.").ResetColor();
				}
			}

			if (projectContents.Count == 0)
			{
				ConsoleWriter.Out.Yellow().WriteLine("  No project files could be read. Skipping.").ResetColor();
				return;
			}

			// Generate migration
			var result = CpmMigrationGenerator.Migrate(references, projectContents, perProject);

			// Print warnings for version conflicts
			foreach (var conflict in result.Conflicts)
			{
				Logger.Warning(
					$"Version conflict: package '{conflict.PackageName}' in project '{conflict.ProjectPath}' " +
					$"uses version {conflict.OverrideVersion} (VersionOverride) while central version is {conflict.CentralVersion}");
			}

			// Print summary
			var newFiles = result.FileChanges.Count(f => f.IsNew);
			var modifiedFiles = result.FileChanges.Count(f => !f.IsNew);
			ConsoleWriter.Out.WriteLine($"  CPM migration plan: {newFiles} new file(s), {modifiedFiles} modified file(s), {result.Conflicts.Count} version conflict(s)");

			if (isDryRun)
			{
				PrintDryRunSummary(result);
				return;
			}

			// Push changes
			var fileChanges = result.FileChanges.ToDictionary(f => f.FilePath, f => f.Content, StringComparer.OrdinalIgnoreCase);

			if (fileChanges.Count == 0)
			{
				ConsoleWriter.Out.WriteLine("  No effective changes to push.");
				return;
			}

			var targetBranch = await ResolveTargetBranchAsync(client, repository, updateConfig);

			if (targetBranch == null)
			{
				return;
			}

			var now = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
			var featureBranch = $"feature/migrate-to-cpm-{now}";
			var commitMessage = "chore: migrate to Central Package Management (Directory.Packages.props)";

			var (branchRef, _) = await client.CreateBranchAndPushAsync(
				repository, sourceBranch.Value.ObjectId, featureBranch, fileChanges, commitMessage);

			ConsoleWriter.Out.Green().WriteLine($"  Created branch: {branchRef}").ResetColor();

			var prDescription = BuildPrDescription(result, perProject);

			var pr = await client.CreatePullRequestAsync(
				repository,
				branchRef,
				targetBranch.Value.RefName,
				commitMessage,
				prDescription,
				reviewers: null);

			ConsoleWriter.Out.Green().WriteLine($"  Created PR #{pr.PullRequestId}: {commitMessage}").ResetColor();
		}

		/// <summary>
		/// Resolves the source branch from the repository.
		/// </summary>
		private static async Task<(string RefName, string ObjectId)?> ResolveSourceBranchAsync(
			AzureDevOpsClient client,
			Microsoft.TeamFoundation.SourceControl.WebApi.GitRepository repository,
			UpdateConfig? updateConfig)
		{
			var sourcePattern = updateConfig?.SourceBranchPattern ?? updateConfig?.TargetBranchPattern;

			if (!string.IsNullOrEmpty(sourcePattern))
			{
				var branch = await client.FindLatestBranchAsync(repository, sourcePattern);

				if (branch == null)
				{
					ConsoleWriter.Out.Yellow().WriteLine($"  No source branch matching '{sourcePattern}' found. Skipping.").ResetColor();
				}

				return branch;
			}

			var defaultBranch = await client.GetDefaultBranchAsync(repository);

			if (defaultBranch == null)
			{
				ConsoleWriter.Out.Yellow().WriteLine($"  Could not resolve default branch for '{repository.Name}'. Skipping.").ResetColor();
			}

			return defaultBranch;
		}

		/// <summary>
		/// Resolves the target branch for the pull request.
		/// </summary>
		private static async Task<(string RefName, string ObjectId)?> ResolveTargetBranchAsync(
			AzureDevOpsClient client,
			Microsoft.TeamFoundation.SourceControl.WebApi.GitRepository repository,
			UpdateConfig? updateConfig)
		{
			if (!string.IsNullOrEmpty(updateConfig?.TargetBranchPattern))
			{
				var branch = await client.FindLatestBranchAsync(repository, updateConfig.TargetBranchPattern);

				if (branch == null)
				{
					ConsoleWriter.Out.Yellow().WriteLine($"  No target branch matching '{updateConfig.TargetBranchPattern}' found. Skipping.").ResetColor();
				}

				return branch;
			}

			var defaultBranch = await client.GetDefaultBranchAsync(repository);

			if (defaultBranch == null)
			{
				ConsoleWriter.Out.Yellow().WriteLine($"  Could not resolve default branch for '{repository.Name}'. Skipping.").ResetColor();
			}

			return defaultBranch;
		}

		/// <summary>
		/// Prints a dry-run summary of the planned CPM migration changes.
		/// </summary>
		private static void PrintDryRunSummary(CpmMigrationResult result)
		{
			ConsoleWriter.Out.Cyan().WriteLine("  Dry-run mode — no changes will be pushed.").ResetColor();

			foreach (var change in result.FileChanges)
			{
				var action = change.IsNew ? "CREATE" : "MODIFY";
				ConsoleWriter.Out.WriteLine($"    [{action}] {change.FilePath}");
			}
		}

		/// <summary>
		/// Builds a markdown description for the CPM migration pull request.
		/// </summary>
		private static string BuildPrDescription(CpmMigrationResult result, bool perProject)
		{
			var sb = new StringBuilder();
			sb.AppendLine("## Central Package Management Migration");
			sb.AppendLine();
			sb.AppendLine("This PR migrates the repository to use Central Package Management (CPM).");

			if (perProject)
			{
				sb.AppendLine("A `Directory.Packages.props` file is created alongside each project.");
			}
			else
			{
				sb.AppendLine("A single `Directory.Packages.props` file is created at the repository root.");
			}

			sb.AppendLine();

			if (result.Conflicts.Count > 0)
			{
				sb.AppendLine("### Version Conflicts");
				sb.AppendLine();
				sb.AppendLine("| Package | Project | Override Version | Central Version |");
				sb.AppendLine("|---------|---------|------------------|-----------------|");

				foreach (var conflict in result.Conflicts)
				{
					sb.AppendLine($"| {conflict.PackageName} | {conflict.ProjectPath} | {conflict.OverrideVersion} | {conflict.CentralVersion} |");
				}

				sb.AppendLine();
			}

			sb.AppendLine("### File Changes");
			sb.AppendLine();

			foreach (var change in result.FileChanges)
			{
				var action = change.IsNew ? "📄 New" : "✏️ Modified";
				sb.AppendLine($"- {action}: `{change.FilePath}`");
			}

			return sb.ToString();
		}
	}
}
