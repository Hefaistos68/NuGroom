using NuGroom.ADO;
using NuGroom.Configuration;
using NuGroom.Nuget;

using System.Text;

namespace NuGroom.Workflows
{
	/// <summary>
	/// Executes the package update workflow: builds update plans, creates feature branches,
	/// pushes changes, and opens pull requests.
	/// </summary>
	internal static class UpdateWorkflow
	{
		/// <summary>
		/// Runs the full update pipeline for all scanned repositories.
		/// </summary>
		/// <param name="config">Azure DevOps connection configuration.</param>
		/// <param name="references">All scanned package references with resolved NuGet metadata.</param>
		/// <param name="updateConfig">Configuration controlling update scope, branching, and dry-run mode.</param>
		/// <param name="renovateOverrides">Per-repository Renovate configuration overrides.</param>
		public static async Task ExecuteAsync(
			AzureDevOpsConfig config,
			List<PackageReferenceExtractor.PackageReference> references,
			UpdateConfig updateConfig,
			Dictionary<string, RenovateOverrides> renovateOverrides)
		{
			updateConfig.ValidateReviewers();

			var updater = new PackageReferenceUpdater(updateConfig.Scope, updateConfig.PinnedPackages, updateConfig.SourcePackagesOnly);
			var plans = updater.BuildUpdatePlans(references);

			PackageReferenceUpdater.PrintUpdateSummary(plans, updateConfig);

			if (updateConfig.DryRun || plans.Count == 0)
			{
				return;
			}

			using var client = new AzureDevOpsClient(config);
			var repositories = await client.GetRepositoriesAsync();
			var repoLookup = repositories.ToDictionary(r => r.Name, r => r, StringComparer.OrdinalIgnoreCase);

			foreach (var plan in plans)
			{
				if (!repoLookup.TryGetValue(plan.RepositoryName, out var repository))
				{
					ConsoleWriter.Out.Yellow().WriteLine($"Warning: Repository '{plan.RepositoryName}' not found, skipping updates.").ResetColor();
					continue;
				}

				renovateOverrides.TryGetValue(plan.RepositoryName, out var repoRenovate);

				try
				{
					await ApplyRepositoryUpdatesAsync(client, repository, plan, updateConfig, repoRenovate);
				}
				catch (Exception ex)
				{
					ConsoleWriter.Out.Red().WriteLine($"Error updating repository '{plan.RepositoryName}': {ex.Message}").ResetColor();
				}
			}
		}

		/// <summary>
		/// Applies all planned updates to a single repository by creating a feature branch,
		/// pushing changes, and opening a PR.
		/// </summary>
		private static async Task ApplyRepositoryUpdatesAsync(
			AzureDevOpsClient client,
			Microsoft.TeamFoundation.SourceControl.WebApi.GitRepository repository,
			RepositoryUpdatePlan plan,
			UpdateConfig updateConfig,
			RenovateOverrides? renovateOverrides)
		{
			ConsoleWriter.Out.WriteLine($"\nProcessing updates for repository: {plan.RepositoryName}");

			// Check for existing open NuGroom PRs when --no-incremental-prs is set
			if (updateConfig.NoIncrementalPrs)
			{
				var openPrs = await client.GetOpenPullRequestsByBranchPrefixAsync(repository, updateConfig.FeatureBranchName);

				if (openPrs.Count > 0)
				{
					ConsoleWriter.Out.Yellow()
						.WriteLine($"  Warning: Repository '{plan.RepositoryName}' already has {openPrs.Count} open NuGroom PR(s). Skipping (--no-incremental-prs).")
						.ResetColor();

					foreach (var existingPr in openPrs)
					{
						ConsoleWriter.Out.Yellow()
							.WriteLine($"    PR #{existingPr.PullRequestId}: {existingPr.Title}")
							.ResetColor();
					}

					return;
				}
			}

			// Resolve source branch (to branch from and read files)
			(string RefName, string ObjectId)? sourceBranch;

			if (updateConfig.SourceBranchPattern != null)
			{
				sourceBranch = await client.FindLatestBranchAsync(repository, updateConfig.SourceBranchPattern);

				if (sourceBranch == null)
				{
					ConsoleWriter.Out.Yellow().WriteLine($"  No source branch matching '{updateConfig.SourceBranchPattern}' found. Skipping.").ResetColor();
					return;
				}
			}
			else
			{
				sourceBranch = await client.GetDefaultBranchAsync(repository);

				if (sourceBranch == null)
				{
					ConsoleWriter.Out.Yellow().WriteLine($"  Could not resolve default branch for '{plan.RepositoryName}'. Skipping.").ResetColor();
					return;
				}
			}

			// Resolve target branch (PR destination)
			var targetBranch = await client.FindLatestBranchAsync(repository, updateConfig.TargetBranchPattern);

			if (targetBranch == null)
			{
				ConsoleWriter.Out.Yellow().WriteLine($"  No target branch matching '{updateConfig.TargetBranchPattern}' found. Skipping.").ResetColor();
				return;
			}

			ConsoleWriter.Out
				.WriteLine($"  Source branch: {sourceBranch.Value.RefName}")
				.WriteLine($"  Target branch: {targetBranch.Value.RefName}");

			// Read current file contents from the source branch and apply updates
			var fileChanges = new Dictionary<string, string>();

			foreach (var fileUpdate in plan.FileUpdates)
			{
				var currentContent = await client.GetFileContentFromBranchAsync(
					repository, fileUpdate.ProjectPath, sourceBranch.Value.RefName);

				if (string.IsNullOrWhiteSpace(currentContent))
				{
					ConsoleWriter.Out.WriteLine($"  Warning: Could not read {fileUpdate.ProjectPath}, skipping.");
					continue;
				}

				var updatedContent = PackageReferenceUpdater.ApplyUpdates(currentContent, fileUpdate.Updates);

				if (updatedContent != currentContent)
				{
					fileChanges[fileUpdate.ProjectPath] = updatedContent;
				}
			}

			if (fileChanges.Count == 0)
			{
				ConsoleWriter.Out.WriteLine("  No effective changes to push.");
				return;
			}

			// Build descriptive branch name and commit message
			var featureBranch = $"{updateConfig.FeatureBranchName}-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
			var totalUpdates = plan.FileUpdates.Sum(f => f.Updates.Count);
			var commitMessage = $"chore: update {totalUpdates} NuGet package reference(s) ({updateConfig.Scope} scope)";

			// Create branch from source and push
			var (branchRef, commitId) = await client.CreateBranchAndPushAsync(
				repository, sourceBranch.Value.ObjectId, featureBranch, fileChanges, commitMessage);

			ConsoleWriter.Out.Green().WriteLine($"  Created branch: {branchRef}").ResetColor();

			// Optionally tag the commit
			if (updateConfig.TagCommits)
			{
				var tagName = $"nugroom/{featureBranch}";

				try
				{
					await client.CreateTagAsync(repository, tagName, commitId);
					ConsoleWriter.Out.Green().WriteLine($"  Created tag: {tagName}").ResetColor();
				}
				catch (Exception ex)
				{
					ConsoleWriter.Out.Yellow().WriteLine($"  Warning: Failed to create tag '{tagName}': {ex.Message}").ResetColor();
				}
			}

			// Build PR description
			var prDescription = BuildPullRequestDescription(plan, updateConfig.Scope);

			// Resolve reviewer identities
			var allReviewers = new List<Microsoft.TeamFoundation.SourceControl.WebApi.IdentityRefWithVote>();

			if (updateConfig.RequiredReviewers != null && updateConfig.RequiredReviewers.Count > 0)
			{
				var required = await client.ResolveReviewerIdentitiesAsync(updateConfig.RequiredReviewers, isRequired: true);
				allReviewers.AddRange(required);

				if (required.Count > 0)
				{
					ConsoleWriter.Out.WriteLine($"  Required reviewers (config): {string.Join(", ", updateConfig.RequiredReviewers.Take(required.Count))}");
				}
			}

			// Renovate reviewers are always optional
			var effectiveOptionalReviewers = CombineOptionalReviewers(updateConfig.OptionalReviewers, renovateOverrides?.Reviewers);

			if (effectiveOptionalReviewers.Count > 0)
			{
				var optional = await client.ResolveReviewerIdentitiesAsync(effectiveOptionalReviewers, isRequired: false);
				allReviewers.AddRange(optional);

				if (optional.Count > 0)
				{
					ConsoleWriter.Out.WriteLine($"  Optional reviewers: {string.Join(", ", effectiveOptionalReviewers.Take(optional.Count))}");
				}
			}

			// Create PR
			var pr = await client.CreatePullRequestAsync(
				repository,
				branchRef,
				targetBranch.Value.RefName,
				commitMessage,
				prDescription,
				allReviewers.Count > 0 ? allReviewers : null);

			ConsoleWriter.Out.Green().WriteLine($"  Created PR #{pr.PullRequestId}: {commitMessage}").ResetColor();
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
		/// Builds a markdown description for the pull request listing all package updates
		/// </summary>
		/// <param name="plan">The repository update plan.</param>
		/// <param name="scope">The update scope that was applied.</param>
		/// <returns>A markdown-formatted PR description.</returns>
		internal static string BuildPullRequestDescription(RepositoryUpdatePlan plan, UpdateScope scope)
		{
			var sb = new StringBuilder();
			sb.AppendLine($"## Automated NuGet Package Updates ({scope} scope)");
			sb.AppendLine();
			sb.AppendLine("This PR was generated by FindPackageReferences.");
			sb.AppendLine("Projects are ordered by dependency count (fewest first).");
			sb.AppendLine();

			foreach (var file in plan.FileUpdates)
			{
				sb.AppendLine($"### `{file.ProjectPath}` ({file.DependencyCount} dependencies)");
				sb.AppendLine();
				sb.AppendLine("| Package | Old Version | New Version |");
				sb.AppendLine("|---------|-------------|-------------|");

				foreach (var update in file.Updates)
				{
					sb.AppendLine($"| {update.PackageName} | {update.OldVersion} | {update.NewVersion} |");
				}

				sb.AppendLine();
			}

			return sb.ToString();
		}
	}
}
