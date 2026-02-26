using NuGet.Versioning;

using NuGroom.Configuration;
using NuGroom.Nuget;

using System.Text.RegularExpressions;

namespace NuGroom
{
	/// <summary>
	/// Represents a single package version update to apply
	/// </summary>
	/// <param name="PackageName">The package identifier.</param>
	/// <param name="OldVersion">The currently used version string.</param>
	/// <param name="NewVersion">The target version string to update to.</param>
	public record PackageUpdate(string PackageName, string OldVersion, string NewVersion);

	/// <summary>
	/// Represents all updates to apply within a single repository, ordered by project dependency count
	/// </summary>
	/// <param name="RepositoryName">The repository containing the projects.</param>
	/// <param name="FileUpdates">Ordered list of file-level updates (projects with fewest dependencies first).</param>
	public record RepositoryUpdatePlan(string RepositoryName, List<FileUpdate> FileUpdates);

	/// <summary>
	/// Represents the updates to apply to a single .csproj file
	/// </summary>
	/// <param name="ProjectPath">Path to the .csproj file within the repository.</param>
	/// <param name="DependencyCount">Number of package references in this project (used for ordering).</param>
	/// <param name="Updates">Package updates to apply in this file.</param>
	/// <param name="SourceKind">
	/// Identifies the file format so the workflow can dispatch to the correct
	/// <c>Apply*Updates</c> method. Defaults to <see cref="PackageSourceKind.ProjectFile"/>.
	/// </param>
	public record FileUpdate(
		string ProjectPath,
		int DependencyCount,
		List<PackageUpdate> Updates,
		PackageSourceKind SourceKind = PackageSourceKind.ProjectFile);

	/// <summary>
	/// Modifies .csproj file content to update package reference versions, respecting pinning rules and update scope
	/// </summary>
	public class PackageReferenceUpdater
	{
		private readonly UpdateScope _scope;
		private readonly Dictionary<string, string?> _pinnedPackages;
		private readonly bool _sourcePackagesOnly;

		/// <summary>
		/// Initializes a new instance of the <see cref="PackageReferenceUpdater"/> class.
		/// </summary>
		/// <param name="scope">The maximum version change scope to allow.</param>
		/// <param name="pinnedPackages">Packages pinned to specific versions (null version means keep current).</param>
		public PackageReferenceUpdater(UpdateScope scope, List<PinnedPackage>? pinnedPackages = null, bool sourcePackagesOnly = false)
		{
			_scope = scope;
			_sourcePackagesOnly = sourcePackagesOnly;
			_pinnedPackages = (pinnedPackages ?? [])
				.ToDictionary(p => p.PackageName, p => p.Version, StringComparer.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Determines whether a package is pinned and should not be updated
		/// </summary>
		/// <param name="packageName">The package name to check.</param>
		/// <returns><c>true</c> if the package is pinned; otherwise <c>false</c>.</returns>
		public bool IsPinned(string packageName)
		{
			return _pinnedPackages.ContainsKey(packageName);
		}

		/// <summary>
		/// Determines whether an update from <paramref name="currentVersion"/> to <paramref name="latestVersion"/>
		/// falls within the configured <see cref="UpdateScope"/>.
		/// </summary>
		/// <param name="currentVersion">The currently referenced version string.</param>
		/// <param name="latestVersion">The latest available version string.</param>
		/// <returns><c>true</c> if the update is within scope; otherwise <c>false</c>.</returns>
		public bool IsUpdateWithinScope(string currentVersion, string latestVersion)
		{
			if (!NuGetVersion.TryParse(currentVersion, out var current) ||
				!NuGetVersion.TryParse(latestVersion, out var latest))
			{
				return false;
			}

			if (current >= latest)
			{
				return false;
			}

			return _scope switch
			{
				UpdateScope.Patch => current.Major == latest.Major && current.Minor == latest.Minor,
				UpdateScope.Minor => current.Major == latest.Major,
				UpdateScope.Major => true,
				_ => false
			};
		}

		/// <summary>
		/// Computes the target version for a package considering the update scope.
		/// Returns the latest version if it is within the allowed scope.
		/// When the latest version is out of scope but <paramref name="availableVersions"/> are provided,
		/// falls back to the best in-scope version from the available versions list.
		/// </summary>
		/// <param name="currentVersion">The currently referenced version string.</param>
		/// <param name="latestVersion">The latest available version string.</param>
		/// <param name="availableVersions">Optional list of all available versions for in-scope fallback.</param>
		/// <returns>The target version string, or <c>null</c> if no update is applicable.</returns>
		public string? GetTargetVersion(string? currentVersion, string? latestVersion, IReadOnlyList<string>? availableVersions = null)
		{
			if (string.IsNullOrEmpty(currentVersion) || string.IsNullOrEmpty(latestVersion))
			{
				return null;
			}

			if (IsUpdateWithinScope(currentVersion, latestVersion))
			{
				return latestVersion;
			}

			if (availableVersions == null || availableVersions.Count == 0)
			{
				return null;
			}

			var warningLevel = _scope switch
			{
				UpdateScope.Patch => VersionWarningLevel.Patch,
				UpdateScope.Minor => VersionWarningLevel.Minor,
				UpdateScope.Major => VersionWarningLevel.Major,
				_ => VersionWarningLevel.None
			};

			return NuGetPackageResolver.FindLatestInScope(currentVersion, availableVersions, warningLevel);
		}

		/// <summary>
		/// Attempts to create a <see cref="PackageUpdate"/> for a single reference, applying source-only and pinning filters.
		/// </summary>
		/// <param name="pkgRef">The package reference to evaluate.</param>
		/// <param name="skippedNoSource">Tracks packages already logged as skipped due to missing source.</param>
		/// <param name="skippedPinned">Tracks packages already logged as skipped due to pinning.</param>
		/// <returns>A <see cref="PackageUpdate"/> if the reference qualifies for an update; otherwise <c>null</c>.</returns>
		private PackageUpdate? TryCreatePackageUpdate(
			PackageReferenceExtractor.PackageReference pkgRef,
			HashSet<string> skippedNoSource,
			HashSet<string> skippedPinned)
		{
			if (string.IsNullOrEmpty(pkgRef.Version) || pkgRef.NuGetInfo == null)
			{
				return null;
			}

			if (_sourcePackagesOnly && !pkgRef.NuGetInfo.SourceProjects.Any())
			{
				if (skippedNoSource.Add(pkgRef.PackageName))
				{
					Logger.Debug($"Skipping package without source code: {pkgRef.PackageName}");
				}

				return null;
			}

			if (IsPinned(pkgRef.PackageName))
			{
				if (skippedPinned.Add(pkgRef.PackageName))
				{
					Logger.Debug($"Skipping pinned package: {pkgRef.PackageName}");
				}

				return null;
			}

			var targetVersion = GetTargetVersion(pkgRef.Version, pkgRef.NuGetInfo.LatestVersion, pkgRef.NuGetInfo.AvailableVersions);

			if (targetVersion == null)
			{
				return null;
			}

			return new PackageUpdate(pkgRef.PackageName, pkgRef.Version, targetVersion);
		}

		/// <summary>
		/// Builds a <see cref="FileUpdate"/> for a single project, collecting applicable package updates.
		/// </summary>
		/// <param name="projectPath">Path to the project file.</param>
		/// <param name="references">All package references within the project.</param>
		/// <param name="skippedNoSource">Tracks packages already logged as skipped due to missing source.</param>
		/// <param name="skippedPinned">Tracks packages already logged as skipped due to pinning.</param>
		/// <returns>A <see cref="FileUpdate"/> if any updates apply; otherwise <c>null</c>.</returns>
		private FileUpdate? BuildFileUpdate(
			string projectPath,
			List<PackageReferenceExtractor.PackageReference> references,
			HashSet<string> skippedNoSource,
			HashSet<string> skippedPinned,
			PackageSourceKind sourceKind = PackageSourceKind.ProjectFile)
		{
			var updates = references
				.Select(r => TryCreatePackageUpdate(r, skippedNoSource, skippedPinned))
				.Where(u => u != null)
				.Cast<PackageUpdate>()
				.ToList();

			if (updates.Count == 0)
			{
				return null;
			}

			return new FileUpdate(projectPath, references.Count, updates, sourceKind);
		}

		/// <summary>
		/// Builds update plans for all repositories, ordered so that projects with the fewest dependencies are updated first
		/// </summary>
		/// <param name="references">All scanned package references with resolved NuGet info.</param>
		/// <returns>List of repository update plans.</returns>
		public List<RepositoryUpdatePlan> BuildUpdatePlans(List<PackageReferenceExtractor.PackageReference> references)
		{
			var skippedNoSource = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var skippedPinned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			return references
				.GroupBy(r => r.RepositoryName)
				.OrderBy(g => g.Key)
				.Select(repoGroup => BuildRepositoryUpdatePlan(repoGroup, skippedNoSource, skippedPinned))
				.Where(plan => plan != null)
				.Cast<RepositoryUpdatePlan>()
				.ToList();
		}

		/// <summary>
		/// Builds a <see cref="RepositoryUpdatePlan"/> for a single repository group.
		/// CPM-sourced references are consolidated into a single <c>Directory.Packages.props</c>
		/// entry; project-level and packages.config references are grouped per file.
		/// </summary>
		private RepositoryUpdatePlan? BuildRepositoryUpdatePlan(
			IGrouping<string, PackageReferenceExtractor.PackageReference> repoGroup,
			HashSet<string> skippedNoSource,
			HashSet<string> skippedPinned)
		{
			var fileUpdates = new List<FileUpdate>();

			// Classify references by source kind
			var cpmRefs = repoGroup.Where(r => r.SourceKind == PackageSourceKind.CentralPackageManagement).ToList();
			var pkgConfigRefs = repoGroup.Where(r => r.SourceKind == PackageSourceKind.PackagesConfig).ToList();
			var projectRefs = repoGroup.Where(r => r.SourceKind == PackageSourceKind.ProjectFile).ToList();

			// Build a single FileUpdate for Directory.Packages.props from all CPM references (deduplicated by package)
			if (cpmRefs.Count > 0)
			{
				var cpmUpdate = BuildCpmFileUpdate(cpmRefs, skippedNoSource, skippedPinned);

				if (cpmUpdate != null)
				{
					fileUpdates.Add(cpmUpdate);
				}
			}

			// Build per-file updates for packages.config references (grouped by actual packages.config path)
			if (pkgConfigRefs.Count > 0)
			{
				var pkgConfigUpdates = pkgConfigRefs
					.GroupBy(r => r.PackagesConfigPath ?? r.ProjectPath)
					.OrderBy(g => g.Key)
					.Select(pg => BuildFileUpdate(pg.Key, pg.ToList(), skippedNoSource, skippedPinned, PackageSourceKind.PackagesConfig))
					.Where(f => f != null)
					.Cast<FileUpdate>()
					.OrderBy(f => f.DependencyCount);

				fileUpdates.AddRange(pkgConfigUpdates);
			}

			// Build per-file updates for project-level references
			var perFileUpdates = projectRefs
				.GroupBy(r => r.ProjectPath)
				.OrderBy(g => g.Key)
				.Select(pg => BuildFileUpdate(pg.Key, pg.ToList(), skippedNoSource, skippedPinned))
				.Where(f => f != null)
				.Cast<FileUpdate>()
				.OrderBy(f => f.DependencyCount)
				.ToList();

			fileUpdates.AddRange(perFileUpdates);

			if (fileUpdates.Count == 0)
			{
				return null;
			}

			return new RepositoryUpdatePlan(repoGroup.Key, fileUpdates);
		}

		/// <summary>
		/// Consolidates CPM-sourced references into a single <see cref="FileUpdate"/>
		/// targeting the repository's <c>Directory.Packages.props</c> file.
		/// Duplicate packages (same package from multiple projects) are collapsed.
		/// </summary>
		private FileUpdate? BuildCpmFileUpdate(
			List<PackageReferenceExtractor.PackageReference> cpmRefs,
			HashSet<string> skippedNoSource,
			HashSet<string> skippedPinned)
		{
			var seenPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var updates = new List<PackageUpdate>();

			foreach (var pkgRef in cpmRefs)
			{
				if (!seenPackages.Add(pkgRef.PackageName))
				{
					continue;
				}

				var update = TryCreatePackageUpdate(pkgRef, skippedNoSource, skippedPinned);

				if (update != null)
				{
					updates.Add(update);
				}
			}

			if (updates.Count == 0)
			{
				return null;
			}

			var cpmPath = cpmRefs.Select(r => r.CpmFilePath).FirstOrDefault(p => p != null)
				?? "/Directory.Packages.props";

			return new FileUpdate(
				cpmPath,
				cpmRefs.Count,
				updates,
				PackageSourceKind.CentralPackageManagement);
		}

		/// <summary>
		/// Applies package version updates to .csproj XML content, preserving formatting
		/// </summary>
		/// <param name="csprojContent">The raw .csproj file content.</param>
		/// <param name="updates">The list of package updates to apply.</param>
		/// <returns>The modified .csproj content with updated version attributes.</returns>
		public static string ApplyUpdates(string csprojContent, List<PackageUpdate> updates)
		{
			ArgumentNullException.ThrowIfNull(csprojContent);

			if (updates.Count == 0)
			{
				return csprojContent;
			}

			var result = csprojContent;

			foreach (var update in updates)
			{
				// Replace Version attribute in PackageReference elements matching the package name
				var pattern = $@"(<PackageReference\s+[^>]*Include\s*=\s*[""']){Regex.Escape(update.PackageName)}([""'][^>]*Version\s*=\s*[""']){Regex.Escape(update.OldVersion)}([""'])";
				var replacement = $"${{1}}{update.PackageName}${{2}}{update.NewVersion}${{3}}";

				result = Regex.Replace(result, pattern, replacement, RegexOptions.IgnoreCase);
			}

			return result;
		}

		/// <summary>
		/// Applies package version updates to <c>Directory.Packages.props</c> content
		/// by modifying <c>&lt;PackageVersion&gt;</c> elements.
		/// </summary>
		/// <param name="propsContent">The raw <c>Directory.Packages.props</c> file content.</param>
		/// <param name="updates">The list of package updates to apply.</param>
		/// <returns>The modified content with updated version attributes.</returns>
		public static string ApplyCpmUpdates(string propsContent, List<PackageUpdate> updates)
		{
			ArgumentNullException.ThrowIfNull(propsContent);

			if (updates.Count == 0)
			{
				return propsContent;
			}

			var result = propsContent;

			foreach (var update in updates)
			{
				var pattern = $@"(<PackageVersion\s+[^>]*Include\s*=\s*[""']){Regex.Escape(update.PackageName)}([""'][^>]*Version\s*=\s*[""']){Regex.Escape(update.OldVersion)}([""'])";
				var replacement = $"${{1}}{update.PackageName}${{2}}{update.NewVersion}${{3}}";

				result = Regex.Replace(result, pattern, replacement, RegexOptions.IgnoreCase);
			}

			return result;
		}

		/// <summary>
		/// Applies package version updates to <c>packages.config</c> content
		/// by modifying <c>&lt;package&gt;</c> elements.
		/// </summary>
		/// <param name="packagesConfigContent">The raw <c>packages.config</c> file content.</param>
		/// <param name="updates">The list of package updates to apply.</param>
		/// <returns>The modified content with updated version attributes.</returns>
		public static string ApplyPackagesConfigUpdates(string packagesConfigContent, List<PackageUpdate> updates)
		{
			ArgumentNullException.ThrowIfNull(packagesConfigContent);

			if (updates.Count == 0)
			{
				return packagesConfigContent;
			}

			var result = packagesConfigContent;

			foreach (var update in updates)
			{
				var pattern = $@"(<package\s+[^>]*id\s*=\s*[""']){Regex.Escape(update.PackageName)}([""'][^>]*version\s*=\s*[""']){Regex.Escape(update.OldVersion)}([""'])";
				var replacement = $"${{1}}{update.PackageName}${{2}}{update.NewVersion}${{3}}";

				result = Regex.Replace(result, pattern, replacement, RegexOptions.IgnoreCase);
			}

			return result;
		}

		/// <summary>
		/// Prints a summary of the update plans to the console, including what branches and PRs would be created in dry-run mode
		/// </summary>
		/// <param name="plans">The update plans to display.</param>
		/// <param name="updateConfig">Update configuration with scope, branch naming, and dry-run settings.</param>
		public static void PrintUpdateSummary(List<RepositoryUpdatePlan> plans, UpdateConfig updateConfig)
		{
			var w = ConsoleWriter.Out;

			if (plans.Count == 0)
			{
				w.WriteLine("No package updates found within the configured scope.");
				return;
			}

			w.WriteLine()
			 .WriteLine(new string('=', 80));

			if (updateConfig.DryRun)
			{
				w.WriteLineColored(ConsoleColor.Yellow, "DRY RUN - UPDATE PLAN (no changes will be made)");
			}
			else
			{
				w.WriteLine("UPDATE PLAN");
			}

			w.WriteLine(new string('=', 80))
			 .WriteLine($"Update scope: {updateConfig.Scope}");

			if (updateConfig.DryRun)
			{
				PrintDryRunDetails(w, updateConfig);
			}

			w.WriteLine();

			var totalUpdates = 0;

			foreach (var plan in plans)
			{
				totalUpdates += PrintRepositoryPlan(w, plan, updateConfig);
			}

			w.WriteLine($"Total: {totalUpdates} update(s) across {plans.Count} repository(ies)");

			if (updateConfig.DryRun)
			{
				w.Yellow()
				 .WriteLine($"Would create {plans.Count} feature branch(es) and {plans.Count} pull request(s).")
				 .WriteLine("Run with --update-references (without --dry-run) to apply changes.")
				 .ResetColor();
			}
		}

		/// <summary>
		/// Prints dry-run configuration details (source/target branches, reviewers).
		/// </summary>
		private static void PrintDryRunDetails(ConsoleWriter w, UpdateConfig updateConfig)
		{
			var sourceBranchDisplay = updateConfig.SourceBranchPattern ?? "(default branch)";

			w.WriteLine($"Source branch: {sourceBranchDisplay}")
			 .WriteLine($"Target branch pattern: {updateConfig.TargetBranchPattern}")
			 .WriteLine($"Feature branch prefix: {updateConfig.FeatureBranchName}");

			if (updateConfig.RequiredReviewers != null && updateConfig.RequiredReviewers.Count > 0)
			{
				w.WriteLine($"Required reviewers: {string.Join(", ", updateConfig.RequiredReviewers)}");
			}

			if (updateConfig.OptionalReviewers != null && updateConfig.OptionalReviewers.Count > 0)
			{
				w.WriteLine($"Optional reviewers: {string.Join(", ", updateConfig.OptionalReviewers)}");
			}
		}

		/// <summary>
		/// Prints the file updates and optional dry-run branch/PR info for a single repository.
		/// </summary>
		/// <returns>The number of package updates printed.</returns>
		private static int PrintRepositoryPlan(ConsoleWriter w, RepositoryUpdatePlan plan, UpdateConfig updateConfig)
		{
			var updateCount = 0;

			w.WriteLine($"Repository: {plan.RepositoryName}")
			 .WriteLine(new string('-', 50));

			foreach (var file in plan.FileUpdates)
			{
				w.WriteLine($"  {file.ProjectPath} ({file.DependencyCount} total dependencies)");

				foreach (var update in file.Updates)
				{
					w.WriteLineColored(ConsoleColor.Cyan, $"    {update.PackageName}: {update.OldVersion} → {update.NewVersion}");
					updateCount++;
				}
			}

			if (updateConfig.DryRun)
			{
				var repoUpdates = plan.FileUpdates.Sum(f => f.Updates.Count);
				var branchName = $"{updateConfig.FeatureBranchName}-{{timestamp}}";
				var commitMsg = $"chore: update {repoUpdates} NuGet package reference(s) ({updateConfig.Scope} scope)";

				var sourceDryRun = updateConfig.SourceBranchPattern ?? "default branch";

				w.DarkYellow()
				 .WriteLine($"  [Would create] Branch: {branchName} from {sourceDryRun}")
				 .WriteLine($"  [Would create] PR: \"{commitMsg}\" → {updateConfig.TargetBranchPattern}")
				 .ResetColor();
			}

			w.WriteLine();

			return updateCount;
		}
	}
}
