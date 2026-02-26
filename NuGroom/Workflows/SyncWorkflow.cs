using NuGroom.ADO;
using NuGroom.Configuration;
using NuGroom.Nuget;

using System.Text;

namespace NuGroom.Workflows
{
	/// <summary>
	/// Executes the package sync workflow: resolves the target version, scans all repositories
	/// for the package, creates feature branches and PRs to update or downgrade to the specified version.
	/// </summary>
	internal static class SyncWorkflow
	{
		/// <summary>
		/// Runs the sync pipeline for a single <see cref="SyncConfig"/>.
		/// </summary>
		/// <param name="config">Azure DevOps connection configuration.</param>
		/// <param name="syncConfig">Sync configuration with package name and optional target version.</param>
		/// <param name="updateConfig">Update configuration for branch patterns and reviewer settings.</param>
		/// <param name="feeds">NuGet feeds used for version resolution.</param>
		/// <param name="feedAuth">Feed authentication entries.</param>
		/// <param name="ignoreRenovate">Whether to skip reading Renovate configuration files from repositories.</param>
		public static async Task ExecuteAsync(
			AzureDevOpsConfig config,
			SyncConfig syncConfig,
			UpdateConfig? updateConfig,
			List<Feed> feeds,
			List<FeedAuth> feedAuth,
			bool ignoreRenovate)
		{
			var effectiveUpdateConfig = updateConfig ?? new UpdateConfig();
			effectiveUpdateConfig.ValidateReviewers();

			// Resolve target version from feeds if not explicitly provided
			var targetVersion = syncConfig.TargetVersion;

			if (string.IsNullOrWhiteSpace(targetVersion))
			{
				ConsoleWriter.Out.WriteLine($"Resolving latest version for {syncConfig.PackageName}...");
				var resolver = new NuGetPackageResolver(feeds, feedAuth);
				var packageInfo = await resolver.ResolvePackageAsync(syncConfig.PackageName);

				if (string.IsNullOrWhiteSpace(packageInfo.LatestVersion))
				{
					ConsoleWriter.Out.Red().WriteLine($"Error: Could not resolve latest version for '{syncConfig.PackageName}' from configured feeds.").ResetColor();

					return;
				}

				targetVersion = packageInfo.LatestVersion;
				ConsoleWriter.Out.WriteLine($"Resolved latest version: {targetVersion}");
			}

			ConsoleWriter.Out
				.WriteLine()
				.WriteLine(new string('=', 80))
				.WriteLine($"SYNC: {syncConfig.PackageName} → {targetVersion}")
				.WriteLine(new string('=', 80));

			using var client = new AzureDevOpsClient(config);
			var repositories = await client.GetRepositoriesAsync();

			ConsoleWriter.Out
				.WriteLine($"Scanning {repositories.Count} repository(ies) for {syncConfig.PackageName}...")
				.WriteLine();

			var exclusionList = PackageReferenceExtractor.ExclusionList.CreateEmpty();
			exclusionList.ExcludedPrefixes.Clear();
			var extractor = new PackageReferenceExtractor(exclusionList);
			var syncedCount = 0;
			var skippedCount = 0;
			var unchangedCount = 0;
			var createdPrs = new List<(string Repository, int PrId, int ProjectCount, string Url)>();

			foreach (var repository in repositories)
			{
				// Resolve source branch
				(string RefName, string ObjectId)? sourceBranch;

				if (effectiveUpdateConfig.SourceBranchPattern != null)
				{
					sourceBranch = await client.FindLatestBranchAsync(repository, effectiveUpdateConfig.SourceBranchPattern);
				}
				else
				{
					sourceBranch = await client.GetDefaultBranchAsync(repository);
				}

				if (sourceBranch == null)
				{
					continue;
				}

				// Get project files
					var projectFiles = await client.GetProjectFilesAsync(repository);

					if (projectFiles.Count == 0)
					{
						continue;
					}

					// Check Renovate overrides — skip if this package is excluded
					RenovateOverrides? repoRenovate = null;

					if (!ignoreRenovate)
					{
						try
						{
							repoRenovate = await RenovateConfigReader.TryReadFromRepositoryAsync(client, repository);
						}
						catch (Exception ex)
						{
							Logger.Debug($"Failed to read Renovate config for {repository.Name}: {ex.Message}");
						}
					}

					if (repoRenovate != null && RenovateConfigReader.IsPackageExcluded(syncConfig.PackageName, repoRenovate))
					{
						Logger.Debug($"Skipping {repository.Name}: package excluded by Renovate config");
						continue;
					}

					var fileChanges = new Dictionary<string, string>();
					var updates = new List<(string ProjectPath, string OldVersion)>();

					foreach (var projectFile in projectFiles)
					{
						var content = await client.GetFileContentFromBranchAsync(
							repository, projectFile.Path, sourceBranch.Value.RefName);

						if (string.IsNullOrWhiteSpace(content))
						{
							continue;
						}

						// Extract references to find the current version of the target package
						var refs = extractor.ExtractPackageReferences(content, repository.Name, projectFile.Path);
						var match = refs.FirstOrDefault(r =>
							r.PackageName.Equals(syncConfig.PackageName, StringComparison.OrdinalIgnoreCase));

						if (match == null || string.IsNullOrEmpty(match.Version))
						{
							continue;
						}

						if (match.Version.Equals(targetVersion, StringComparison.OrdinalIgnoreCase))
						{
							continue;
						}

						// Apply the update (works for both upgrade and downgrade)
						var updateList = new List<PackageUpdate>
						{
							new(syncConfig.PackageName, match.Version, targetVersion)
						};

						var updatedContent = PackageReferenceUpdater.ApplyUpdates(content, updateList);

						if (updatedContent != content)
						{
							fileChanges[projectFile.Path] = updatedContent;
							updates.Add((projectFile.Path, match.Version));
						}
					}

				if (fileChanges.Count == 0)
				{
					unchangedCount++;
					continue;
				}

				// Dry-run check
				if (effectiveUpdateConfig.DryRun)
				{
					var c = ConsoleWriter.Out
						.WriteLine($"Repository: {repository.Name}")
						.WriteLine(new string('-', 50));

					foreach (var (projectPath, oldVersion) in updates)
					{
						c.WriteLine($"  {projectPath}: {oldVersion} → {targetVersion}");
					}

					c.WriteLine($"  [Would create] Branch + PR to sync {syncConfig.PackageName} to {targetVersion}")
						.WriteLine();
					syncedCount++;
					continue;
				}

				// Resolve target branch
				var targetBranch = await client.FindLatestBranchAsync(repository, effectiveUpdateConfig.TargetBranchPattern);

				if (targetBranch == null)
				{
					ConsoleWriter.Out.Yellow().WriteLine($"  {repository.Name}: No target branch matching '{effectiveUpdateConfig.TargetBranchPattern}'. Skipping.").ResetColor();
					skippedCount++;
					continue;
				}

				// Create branch, push, and open PR
				var featureBranch = $"feature/sync-{syncConfig.PackageName.ToLowerInvariant()}-{targetVersion}-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
				var commitMessage = $"chore: sync {syncConfig.PackageName} to {targetVersion}";

				try
				{
					var (branchRef, commitId) = await client.CreateBranchAndPushAsync(
						repository, sourceBranch.Value.ObjectId, featureBranch, fileChanges, commitMessage);

					// Optionally tag the commit
					if (effectiveUpdateConfig.TagCommits)
					{
						var tagName = $"nugroom/{featureBranch}";

						try
						{
							await client.CreateTagAsync(repository, tagName, commitId);
							ConsoleWriter.Out.Green().WriteLine($"  {repository.Name}: Created tag: {tagName}").ResetColor();
						}
						catch (Exception ex)
						{
							ConsoleWriter.Out.Yellow().WriteLine($"  {repository.Name}: Warning: Failed to create tag '{tagName}': {ex.Message}").ResetColor();
						}
					}

					// Build PR description
					var prDescription = BuildSyncPrDescription(syncConfig.PackageName, targetVersion, updates);

					// Resolve reviewers
					var allReviewers = new List<Microsoft.TeamFoundation.SourceControl.WebApi.IdentityRefWithVote>();

					if (effectiveUpdateConfig.RequiredReviewers != null && effectiveUpdateConfig.RequiredReviewers.Count > 0)
					{
						var required = await client.ResolveReviewerIdentitiesAsync(effectiveUpdateConfig.RequiredReviewers, isRequired: true);
						allReviewers.AddRange(required);
					}

					// Renovate reviewers are always optional
					var effectiveOptionalReviewers = CombineOptionalReviewers(effectiveUpdateConfig.OptionalReviewers, repoRenovate?.Reviewers);

					if (effectiveOptionalReviewers.Count > 0)
					{
						var optional = await client.ResolveReviewerIdentitiesAsync(effectiveOptionalReviewers, isRequired: false);
						allReviewers.AddRange(optional);
					}

					var pr = await client.CreatePullRequestAsync(
						repository,
						branchRef,
						targetBranch.Value.RefName,
						commitMessage,
						prDescription,
						allReviewers.Count > 0 ? allReviewers : null);

					var prProject = Uri.EscapeDataString(repository.ProjectReference?.Name ?? config.ProjectName ?? "");
					var prUrl = $"{config.OrganizationUrl.TrimEnd('/')}/{prProject}/_git/{Uri.EscapeDataString(repository.Name)}/pullrequest/{pr.PullRequestId}";

					ConsoleWriter.Out.Green().WriteLine($"  {repository.Name}: Created PR #{pr.PullRequestId} ({updates.Count} project(s) updated)").ResetColor();
					createdPrs.Add((repository.Name, pr.PullRequestId, updates.Count, prUrl));
					syncedCount++;
				}
				catch (Exception ex)
				{
					ConsoleWriter.Out.Red().WriteLine($"  {repository.Name}: Failed — {ex.Message}").ResetColor();
					skippedCount++;
				}
			}

			PrintSyncSummary(effectiveUpdateConfig, syncedCount, skippedCount, unchangedCount, targetVersion, createdPrs);
		}

		/// <summary>
		/// Builds a markdown PR description for a sync operation
		/// </summary>
		private static string BuildSyncPrDescription(
			string packageName,
			string targetVersion,
			List<(string ProjectPath, string OldVersion)> updates)
		{
			var sb = new StringBuilder();
			sb.AppendLine($"## Package Sync: {packageName} → {targetVersion}");
			sb.AppendLine();
			sb.AppendLine("This PR was generated by `NuGroom --sync`.");
			sb.AppendLine();
			sb.AppendLine("| Project | Old Version | New Version |");
			sb.AppendLine("|---------|-------------|-------------|");

			foreach (var (projectPath, oldVersion) in updates)
			{
				sb.AppendLine($"| `{projectPath}` | {oldVersion} | {targetVersion} |");
			}

			return sb.ToString();
		}

		/// <summary>
		/// Merges config optional reviewers with Renovate reviewers into a single deduplicated list.
		/// </summary>
		private static List<string> CombineOptionalReviewers(List<string>? configOptional, List<string>? renovateReviewers)
		{
			var combined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			if (configOptional != null)
			{
				foreach (var r in configOptional)
				{
					combined.Add(r);
				}
			}

			if (renovateReviewers != null)
			{
				foreach (var r in renovateReviewers)
				{
					combined.Add(r);
				}
			}

			return combined.ToList();
		}

		/// <summary>
		/// Prints the final sync summary and created PR list
		/// </summary>
		private static void PrintSyncSummary(
			UpdateConfig updateConfig,
			int syncedCount,
			int skippedCount,
			int unchangedCount,
			string targetVersion,
			List<(string Repository, int PrId, int ProjectCount, string Url)> createdPrs)
		{
			ConsoleWriter.Out
				.WriteLine()
				.WriteLine(new string('=', 80));

			if (updateConfig.DryRun)
			{
				ConsoleWriter.Out
					.WriteLine($"DRY RUN: Would sync {syncedCount} repository(ies). {unchangedCount} already at {targetVersion}.")
					.WriteLine($"Run without --dry-run (or set \"DryRun\": false) to apply changes.");
			}
			else
			{
				ConsoleWriter.Out.WriteLine($"Synced {syncedCount} repository(ies). {skippedCount} skipped. {unchangedCount} already at {targetVersion}.");
			}

			if (createdPrs.Count > 0)
			{
				ConsoleWriter.Out
					.WriteLine()
					.WriteLine($"CREATED PULL REQUESTS ({createdPrs.Count})")
					.WriteLine(new string('-', 80));

				foreach (var (repoName, prId, projectCount, url) in createdPrs)
				{
					ConsoleWriter.Out
						.WriteLine($"  PR #{prId,-6} | {repoName,-45} | {projectCount} project(s)")
						.DarkGray().WriteLine($"           {url}").ResetColor();
				}

				ConsoleWriter.Out.WriteLine(new string('-', 80));
			}
		}
	}
}
