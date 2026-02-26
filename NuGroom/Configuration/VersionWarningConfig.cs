using System.Text.Json.Serialization;

namespace NuGroom.Configuration
{
	/// <summary>
	/// Defines the granularity level for version difference warnings.
	/// Each level detects only differences at that specific scope:
	/// Major detects cross-major diffs, Minor detects cross-minor diffs within the same major,
	/// and Patch detects cross-patch diffs within the same major.minor.
	/// </summary>
	[JsonConverter(typeof(JsonStringEnumConverter))]
	public enum VersionWarningLevel
	{
		/// <summary>No warnings</summary>
		None = 0,

		/// <summary>Warn only on major version differences (e.g., 1.x → 2.x)</summary>
		Major = 1,

		/// <summary>Warn only on minor version differences within the same major version (e.g., 1.2.x → 1.3.x)</summary>
		Minor = 2,

		/// <summary>Warn only on patch version differences within the same major.minor version (e.g., 1.2.3 → 1.2.5)</summary>
		Patch = 3
	}

	/// <summary>
	/// Package-specific version warning configuration
	/// </summary>
	public class PackageWarningRule
	{
		public string PackageName { get; set; } = string.Empty;

		[JsonConverter(typeof(JsonStringEnumConverter))]
		public VersionWarningLevel Level { get; set; }
	}

	/// <summary>
	/// Configuration for version warnings
	/// </summary>
	public class VersionWarningConfig
	{
		/// <summary>
		/// Global default warning level for all packages
		/// </summary>
		[JsonConverter(typeof(JsonStringEnumConverter))]
		public VersionWarningLevel DefaultLevel { get; set; } = VersionWarningLevel.None;

		/// <summary>
		/// Package-specific warning overrides
		/// </summary>
		public List<PackageWarningRule>? PackageRules { get; set; }

		/// <summary>
		/// Gets the warning level for a specific package
		/// </summary>
		public VersionWarningLevel GetLevelForPackage(string packageName)
		{
			if (PackageRules != null)
			{
				var rule = PackageRules.FirstOrDefault(r =>
					r.PackageName.Equals(packageName, StringComparison.OrdinalIgnoreCase));
				if (rule != null)
					return rule.Level;
			}
			return DefaultLevel;
		}
	}

	/// <summary>
	/// Represents a version difference warning
	/// </summary>
	public record VersionWarning(
		string PackageName,
		string Repository,
		string ProjectPath,
		string CurrentVersion,
		string ReferenceVersion,
		string WarningType,
		VersionWarningLevel Level,
		string Description);

	/// <summary>
	/// Represents a package update recommendation
	/// </summary>
	public record PackageRecommendation(
		string PackageName,
		string Repository,
		string ProjectPath,
		string CurrentVersion,
		string RecommendedVersion,
		string RecommendationType,
		string Reason);

	/// <summary>
	/// Defines the scope of version updates to apply when updating package references
	/// </summary>
	[JsonConverter(typeof(JsonStringEnumConverter))]
	public enum UpdateScope
	{
		/// <summary>Only update patch versions (e.g., 1.2.3 → 1.2.5)</summary>
		Patch = 1,

		/// <summary>Update minor and patch versions (e.g., 1.2.3 → 1.4.0)</summary>
		Minor = 2,

		/// <summary>Update major, minor, and patch versions (e.g., 1.2.3 → 2.0.0)</summary>
		Major = 3
	}

	/// <summary>
	/// Represents a package pinned to a specific version, preventing automatic updates
	/// </summary>
	public class PinnedPackage
	{
		/// <summary>
		/// The package ID to pin (e.g., "Newtonsoft.Json")
		/// </summary>
		public string PackageName { get; set; } = string.Empty;

		/// <summary>
		/// The version to pin the package to. If null, the currently used version is kept.
		/// </summary>
		public string? Version { get; set; }

		/// <summary>
		/// Optional reason for pinning this package
		/// </summary>
		public string? Reason { get; set; }
	}

	/// <summary>
	/// Configuration for the automatic update and PR creation feature
	/// </summary>
	public class UpdateConfig
	{
		/// <summary>
		/// The scope of updates to apply (Patch, Minor, or Major)
		/// </summary>
		[JsonConverter(typeof(JsonStringEnumConverter))]
		public UpdateScope Scope { get; set; } = UpdateScope.Patch;

		/// <summary>
		/// Branch name pattern to use for the feature branch. Supports {date} and {scope} placeholders.
		/// </summary>
		public string FeatureBranchName { get; set; } = "nugroom/update-nuget-references";

		/// <summary>
		/// Pattern to match the source branch to create the feature branch from (e.g., "develop/*").
		/// The latest semver matching branch is selected. If null, defaults to <see cref="TargetBranchPattern"/>.
		/// </summary>
		public string? SourceBranchPattern { get; set; }

		/// <summary>
		/// Pattern to match the target branch for the pull request (e.g., "develop/*").
		/// The latest semver matching branch is selected.
		/// </summary>
		public string TargetBranchPattern { get; set; } = "develop/*";

		/// <summary>
		/// If true, show what would be changed without creating branches or PRs
		/// </summary>
		public bool DryRun { get; set; } = true;

		/// <summary>
		/// If true, only update packages that have identified source projects in the scanned repositories
		/// </summary>
		public bool SourcePackagesOnly { get; set; }

		/// <summary>
		/// List of packages pinned to specific versions that should not be updated
		/// </summary>
		public List<PinnedPackage>? PinnedPackages { get; set; }

		/// <summary>
		/// List of required reviewer email addresses or unique names. Required reviewers must approve before the PR can be completed.
		/// </summary>
		public List<string>? RequiredReviewers { get; set; }

		/// <summary>
		/// List of optional reviewer email addresses or unique names. Optional reviewers are notified but their approval is not required.
		/// </summary>
		public List<string>? OptionalReviewers { get; set; }

		/// <summary>
		/// If true, a lightweight git tag is created on the feature branch commit after pushing changes.
		/// The tag name is derived from the branch name and timestamp.
		/// </summary>
		public bool TagCommits { get; set; }

		/// <summary>
		/// If true, skip creating update PRs for repositories that already have open NuGroom PRs.
		/// When an existing open PR is detected, a warning is printed and the repository is skipped.
		/// </summary>
		public bool NoIncrementalPrs { get; set; }

		/// <summary>
		/// Configuration for incrementing project version properties when package references are updated.
		/// If null, no version increment is performed.
		/// </summary>
		public VersionIncrementConfig? VersionIncrement { get; set; }

		/// <summary>
		/// Validates that no identity appears in both required and optional reviewer lists.
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown when the same identity is in both lists.</exception>
		public void ValidateReviewers()
		{
			if (RequiredReviewers == null || OptionalReviewers == null)
			{
				return;
			}

			var duplicates = RequiredReviewers
				.Intersect(OptionalReviewers, StringComparer.OrdinalIgnoreCase)
				.ToList();

			if (duplicates.Count > 0)
			{
				throw new InvalidOperationException(
					$"The following reviewer(s) appear in both RequiredReviewers and OptionalReviewers: {string.Join(", ", duplicates)}");
			}
		}
	}

	/// <summary>
	/// Configuration for the package sync operation, targeting a single package to a specific version across all repositories
	/// </summary>
	/// <param name="PackageName">The full package name to synchronize.</param>
	/// <param name="TargetVersion">The target version to sync to. If null, the latest available version is resolved from feeds.</param>
	public record SyncConfig(string PackageName, string? TargetVersion);

	/// <summary>
	/// Defines which component of a version number to increment
	/// </summary>
	[JsonConverter(typeof(JsonStringEnumConverter))]
	public enum VersionIncrementScope
	{
		/// <summary>Increment the patch component (e.g., 1.2.3 → 1.2.4)</summary>
		Patch = 1,

		/// <summary>Increment the minor component and reset patch (e.g., 1.2.3 → 1.3.0)</summary>
		Minor = 2,

		/// <summary>Increment the major component and reset minor and patch (e.g., 1.2.3 → 2.0.0)</summary>
		Major = 3
	}

	/// <summary>
	/// Configuration for automatically incrementing project version properties when package references are updated
	/// </summary>
	public class VersionIncrementConfig
	{
		/// <summary>
		/// If true, increment the &lt;Version&gt; property in the project file
		/// </summary>
		public bool IncrementVersion { get; set; }

		/// <summary>
		/// If true, increment the &lt;AssemblyVersion&gt; property in the project file
		/// </summary>
		public bool IncrementAssemblyVersion { get; set; }

		/// <summary>
		/// If true, increment the &lt;FileVersion&gt; property in the project file
		/// </summary>
		public bool IncrementFileVersion { get; set; }

		/// <summary>
		/// The version component to increment. Defaults to <see cref="VersionIncrementScope.Patch"/>.
		/// </summary>
		[JsonConverter(typeof(JsonStringEnumConverter))]
		public VersionIncrementScope Scope { get; set; } = VersionIncrementScope.Patch;

		/// <summary>
		/// Gets whether any version increment is configured
		/// </summary>
		public bool IsEnabled => IncrementVersion || IncrementAssemblyVersion || IncrementFileVersion;

		/// <summary>
		/// Sets all version increment flags to true
		/// </summary>
		public void EnableAll()
		{
			IncrementVersion         = true;
			IncrementAssemblyVersion = true;
			IncrementFileVersion     = true;
		}
	}
}
