using NuGroom.Configuration;
using NuGroom.Nuget;

namespace NuGroom
{
	/// <summary>
	/// Analyzes package references and generates version warnings based on configuration
	/// </summary>
	public class VersionWarningAnalyzer
	{
		private readonly VersionWarningConfig _config;
		private readonly Dictionary<string, string?> _pinnedPackages;

		public VersionWarningAnalyzer(VersionWarningConfig config, Dictionary<string, string?>? pinnedPackages = null)
		{
			_config = config ?? new VersionWarningConfig();
			_pinnedPackages = pinnedPackages ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Analyzes package references and generates version warnings
		/// </summary>
		public List<VersionWarning> AnalyzeVersionWarnings(
			IEnumerable<PackageReferenceExtractor.PackageReference> packageReferences)
		{
			var warnings = new List<VersionWarning>();

			// Group by package name to find version differences
			var packageGroups = packageReferences
				.Where(pr => !string.IsNullOrEmpty(pr.Version))
				.GroupBy(pr => pr.PackageName)
				.ToList();

			foreach (var group in packageGroups)
			{
				var packageName = group.Key;
				var level = _config.GetLevelForPackage(packageName);

				if (level == VersionWarningLevel.None)
				{
					continue;
				}

				// For pinned packages, only warn when used version differs from the explicit pinned version
				if (_pinnedPackages.TryGetValue(packageName, out var pinnedVersion))
				{
					warnings.AddRange(AnalyzePinnedPackage(packageName, level, pinnedVersion, group));
					continue;
				}

				warnings.AddRange(AnalyzeUnpinnedPackage(packageName, level, group));
			}

			return warnings.OrderBy(w => w.PackageName)
						  .ThenBy(w => w.Repository)
						  .ThenBy(w => w.ProjectPath)
						  .ToList();
		}

		/// <summary>
		/// Analyzes a pinned package group and returns warnings for references that differ from the pinned version.
		/// </summary>
		private static List<VersionWarning> AnalyzePinnedPackage(
			string packageName, VersionWarningLevel level, string? pinnedVersion,
			IEnumerable<PackageReferenceExtractor.PackageReference> references)
		{
			var warnings = new List<VersionWarning>();

			if (string.IsNullOrEmpty(pinnedVersion))
			{
				return warnings;
			}

			foreach (var reference in references)
			{
				if (string.IsNullOrEmpty(reference.Version))
				{
					continue;
				}

				if (!reference.Version.Equals(pinnedVersion, StringComparison.OrdinalIgnoreCase))
				{
					warnings.Add(new VersionWarning(
						PackageName: packageName,
						Repository: reference.RepositoryName,
						ProjectPath: reference.ProjectPath,
						CurrentVersion: reference.Version,
						ReferenceVersion: pinnedVersion,
						WarningType: "pinned-version-mismatch",
						Level: level,
						Description: $"Package version {reference.Version} differs from pinned version {pinnedVersion}"
					));
				}
			}

			return warnings;
		}

		/// <summary>
		/// Analyzes an unpinned package group and returns warnings for version mismatches against used and available versions.
		/// </summary>
		private List<VersionWarning> AnalyzeUnpinnedPackage(
			string packageName, VersionWarningLevel level,
			IEnumerable<PackageReferenceExtractor.PackageReference> group)
		{
			var warnings = new List<VersionWarning>();
			var references = group.ToList();

			// Find the latest version among all references
			var latestUsedVersion = FindLatestVersion(references.Select(r => r.Version).ToList());

			// Find the latest available version from NuGet
			var latestAvailableVersion = references
				.Select(r => r.NuGetInfo?.LatestVersion)
				.FirstOrDefault(v => !string.IsNullOrEmpty(v));

			// Check each reference against both latest used and latest available
			foreach (var reference in references)
			{
				if (string.IsNullOrEmpty(reference.Version))
				{
					continue;
				}

				var usedWarning = CreateVersionMismatchWarning(
					packageName, level, reference, latestUsedVersion, "version-mismatch-used");

				if (usedWarning != null)
				{
					warnings.Add(usedWarning);
				}

				var availableWarning = CreateVersionMismatchWarning(
					packageName, level, reference, latestAvailableVersion, "version-mismatch-available");

				if (availableWarning != null)
				{
					warnings.Add(availableWarning);
				}
			}

			return warnings;
		}

		/// <summary>
		/// Creates a version mismatch warning if the reference version differs significantly from the target version.
		/// </summary>
		private static VersionWarning? CreateVersionMismatchWarning(
			string packageName, VersionWarningLevel level,
			PackageReferenceExtractor.PackageReference reference,
			string? referenceVersion, string warningType)
		{
			if (string.IsNullOrEmpty(referenceVersion))
			{
				return null;
			}

			if (reference.Version!.Equals(referenceVersion, StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}

			if (!NuGetPackageResolver.VersionsDiffer(reference.Version, referenceVersion, level))
			{
				return null;
			}

			var diffType = NuGetPackageResolver.GetVersionDifference(reference.Version, referenceVersion);

			var versionLabel = warningType == "version-mismatch-available"
				? "latest available version"
				: "latest used version";

			return new VersionWarning(
				PackageName: packageName,
				Repository: reference.RepositoryName,
				ProjectPath: reference.ProjectPath,
				CurrentVersion: reference.Version,
				ReferenceVersion: referenceVersion,
				WarningType: warningType,
				Level: level,
				Description: $"Package version {reference.Version} differs from {versionLabel} {referenceVersion} ({diffType} version difference)"
			);
		}

		/// <summary>
		/// Finds the latest version from a list of version strings
		/// </summary>
		private string? FindLatestVersion(List<string?> versions)
		{
			var validVersions = versions
				.Where(v => !string.IsNullOrEmpty(v))
				.Select(v =>
				{
					try { return NuGet.Versioning.NuGetVersion.Parse(v!); }
					catch { return null; }
				})
				.Where(v => v != null)
				.ToList();

			if (!validVersions.Any())
				return null;

			return validVersions.Max()?.ToNormalizedString();
		}

		/// <summary>
		/// Groups warnings by package for summary reporting
		/// </summary>
		public Dictionary<string, List<VersionWarning>> GroupWarningsByPackage(List<VersionWarning> warnings)
		{
			return warnings.GroupBy(w => w.PackageName)
						  .ToDictionary(g => g.Key, g => g.ToList());
		}

		/// <summary>
		/// Gets warning statistics
		/// </summary>
		public (int TotalWarnings, int PackagesWithWarnings, int MajorWarnings, int MinorWarnings, int PatchWarnings)
			GetWarningStats(List<VersionWarning> warnings)
		{
			var packagesWithWarnings = warnings.Select(w => w.PackageName).Distinct().Count();
			var majorWarnings = warnings.Count(w => w.Level >= VersionWarningLevel.Major &&
				NuGetPackageResolver.GetVersionDifference(w.CurrentVersion, w.ReferenceVersion) == "major");
			var minorWarnings = warnings.Count(w => w.Level >= VersionWarningLevel.Minor &&
				NuGetPackageResolver.GetVersionDifference(w.CurrentVersion, w.ReferenceVersion) == "minor");
			var patchWarnings = warnings.Count(w => w.Level >= VersionWarningLevel.Patch &&
				NuGetPackageResolver.GetVersionDifference(w.CurrentVersion, w.ReferenceVersion) == "patch");

			return (warnings.Count, packagesWithWarnings, majorWarnings, minorWarnings, patchWarnings);
		}

		/// <summary>
		/// Generates package update recommendations based on version warnings
		/// </summary>
		public List<PackageRecommendation> GenerateRecommendations(
			IEnumerable<PackageReferenceExtractor.PackageReference> packageReferences)
		{
			var recommendations = new List<PackageRecommendation>();

			// Group by package name
			var packageGroups = packageReferences
				.Where(pr => !string.IsNullOrEmpty(pr.Version))
				.GroupBy(pr => pr.PackageName)
				.ToList();

			foreach (var group in packageGroups)
			{
				var packageName = group.Key;
				var level = _config.GetLevelForPackage(packageName);

				// Skip if no warnings configured for this package
				if (level == VersionWarningLevel.None)
				{
					continue;
				}

				// Pinned packages must not be recommended for update
				if (_pinnedPackages.ContainsKey(packageName))
				{
					continue;
				}

				recommendations.AddRange(GeneratePackageRecommendations(packageName, level, group));
			}

			return recommendations.OrderBy(r => r.PackageName)
								 .ThenBy(r => r.Repository)
								 .ThenBy(r => r.ProjectPath)
								 .ToList();
		}

		/// <summary>
		/// Generates recommendations for a single unpinned package group by comparing each reference to the recommended version.
		/// </summary>
		private List<PackageRecommendation> GeneratePackageRecommendations(
			string packageName, VersionWarningLevel level,
			IEnumerable<PackageReferenceExtractor.PackageReference> group)
		{
			var recommendations = new List<PackageRecommendation>();
			var references = group.ToList();

			// Find the latest version among all references
			var latestUsedVersion = FindLatestVersion(references.Select(r => r.Version).ToList());

			// Find the latest available version from NuGet
			var latestAvailableVersion = references
				.Select(r => r.NuGetInfo?.LatestVersion)
				.FirstOrDefault(v => !string.IsNullOrEmpty(v));

			// Determine the recommended version (prefer latest available, fallback to latest used)
			var recommendedVersion = latestAvailableVersion ?? latestUsedVersion;

			if (string.IsNullOrEmpty(recommendedVersion))
			{
				return recommendations;
			}

			// Check each reference and create recommendation if needed
			foreach (var reference in references)
			{
				var recommendation = CreateRecommendation(
					packageName, level, reference, recommendedVersion, latestAvailableVersion);

				if (recommendation != null)
				{
					recommendations.Add(recommendation);
				}
			}

			return recommendations;
		}

		/// <summary>
		/// Creates a recommendation for a single reference if its version differs significantly from the recommended version.
		/// </summary>
		private static PackageRecommendation? CreateRecommendation(
			string packageName, VersionWarningLevel level,
			PackageReferenceExtractor.PackageReference reference,
			string recommendedVersion, string? latestAvailableVersion)
		{
			if (string.IsNullOrEmpty(reference.Version))
			{
				return null;
			}

			// Skip if already at recommended version
			if (reference.Version.Equals(recommendedVersion, StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}

			// Check if this differs according to the warning level
			if (!NuGetPackageResolver.VersionsDiffer(reference.Version, recommendedVersion, level))
			{
				return null;
			}

			var diffType = NuGetPackageResolver.GetVersionDifference(reference.Version, recommendedVersion);

			var recommendationType = latestAvailableVersion != null ? "latest-available" : "latest-used";

			var reason = latestAvailableVersion != null
				? $"Upgrade to latest available version (currently {diffType} version behind)"
				: $"Align with latest version used in solution (currently {diffType} version behind)";

			return new PackageRecommendation(
				PackageName: packageName,
				Repository: reference.RepositoryName,
				ProjectPath: reference.ProjectPath,
				CurrentVersion: reference.Version,
				RecommendedVersion: recommendedVersion,
				RecommendationType: recommendationType,
				Reason: reason
			);
		}

		/// <summary>
		/// Groups recommendations by package for summary reporting
		/// </summary>
		public Dictionary<string, List<PackageRecommendation>> GroupRecommendationsByPackage(
			List<PackageRecommendation> recommendations)
		{
			return recommendations.GroupBy(r => r.PackageName)
								 .ToDictionary(g => g.Key, g => g.ToList());
		}

		/// <summary>
		/// Gets recommendation statistics
		/// </summary>
		public (int TotalRecommendations, int PackagesNeedingUpdate, int ProjectsAffected)
			GetRecommendationStats(List<PackageRecommendation> recommendations)
		{
			var packagesNeedingUpdate = recommendations.Select(r => r.PackageName).Distinct().Count();
			var projectsAffected = recommendations.Select(r => new { r.Repository, r.ProjectPath })
												 .Distinct()
												 .Count();

			return (recommendations.Count, packagesNeedingUpdate, projectsAffected);
		}
	}
}
