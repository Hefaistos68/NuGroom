using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

using NuGroom.Configuration;

using System.Collections.Concurrent;

namespace NuGroom.Nuget
{
	/// <summary>
	/// Service for resolving NuGet package information and metadata from one or more feeds
	/// </summary>
	public class NuGetPackageResolver
	{
		private readonly List<(string Name, SourceRepository Repository)> _repositories;
		private readonly ConcurrentDictionary<string, PackageInfo> _packageCache;
		private readonly ILogger _logger;
		private readonly Dictionary<string, FeedAuth> _authMap;
		private readonly ISettings _settings;

		/// <summary>
		/// Represents NuGet package information
		/// </summary>
		public record PackageInfo(
			string PackageName,
			bool ExistsOnNuGetOrg,
			string? PackageUrl,
			string? Description,
			string? Authors,
			string? ProjectUrl,
			DateTimeOffset? Published,
			long? DownloadCount,
			string? LicenseUrl,
			string? IconUrl,
			List<string> Tags,
			bool IsDeprecated,
			string? DeprecationMessage,
			List<ProjectReference> SourceProjects = null!,
			string? LatestVersion = null,
			string? ResolvedVersion = null,
			bool IsOutdated = false,
			bool IsVulnerable = false,
			List<string>? Vulnerabilities = null,
			string? FeedName = null,
			IReadOnlyList<string>? AvailableVersions = null);

		/// <summary>
		/// Represents a potential source project for a package
		/// </summary>
		public record ProjectReference(
			string ProjectName,
			string RepositoryName,
			string ProjectPath,
			double MatchConfidence);

		/// <summary>
		/// Initializes a new instance of the <see cref="NuGetPackageResolver"/> class.
		/// </summary>
		/// <param name="feeds">The feeds.</param>
		/// <param name="feedAuth">The feed auth.</param>
		public NuGetPackageResolver(List<Feed>? feeds = null, List<FeedAuth>? feedAuth = null)
		{
			_packageCache = new ConcurrentDictionary<string, PackageInfo>();
			_logger = NullLogger.Instance;
			_repositories = new List<(string, SourceRepository)>();
			_authMap = (feedAuth ?? new List<FeedAuth>()).ToDictionary(
				a => a.FeedName.Trim(),
				a => a,
				StringComparer.OrdinalIgnoreCase);

			// Load NuGet settings to enable credential providers
			try
			{
				_settings = Settings.LoadDefaultSettings(root: null);
			}
			catch (Exception ex)
			{
				Logger.Warning($"Failed to load NuGet settings, credential providers may not work: {ex.Message}");
				_settings = Settings.LoadDefaultSettings(root: Directory.GetCurrentDirectory());
			}

			// Default to nuget.org if no feeds specified or empty
			if (feeds == null || !feeds.Any())
			{
				feeds = new List<Feed> { new Feed("NuGet.org", "https://api.nuget.org/v3/index.json") };
			}

			foreach (var feed in feeds)
			{
				try
				{
					var source = new PackageSource(feed.Url.Trim(), feed.Name);

					// Apply PAT authentication if explicitly provided. Otherwise rely on standard credential providers (Azure Artifacts, etc.)
					if (_authMap.TryGetValue(feed.Name, out var auth) &&
						!string.IsNullOrEmpty(auth.Pat) &&
						!auth.Pat.Equals("USE_CURRENT_USER", StringComparison.OrdinalIgnoreCase))
					{
						var username = string.IsNullOrEmpty(auth.Username) ? "VssSessionToken" : auth.Username!;
						source.Credentials = new PackageSourceCredential(
							source: source.Name,
							username: username,
							passwordText: auth.Pat!,
							isPasswordClearText: true,
							validAuthenticationTypesText: null);
						Logger.Debug($"Applied PAT authentication for feed: {feed.Name} ({feed.Url})");
					}
					else if (IsAzureDevOpsFeed(feed.Url))
					{
						Logger.Debug($"No PAT for Azure DevOps feed '{feed.Name}'. Relying on installed credential providers.");
					}

					var repo = Repository.Factory.GetCoreV3(source);
					_repositories.Add((feed.Name, repo));
					Logger.Debug($"Initialized feed: {feed.Name} ({feed.Url})");
				}
				catch (Exception ex)
				{
					Logger.Warning($"Failed to initialize feed '{feed.Name}' ({feed.Url}): {ex.Message}");
				}
			}
		}

		/// <summary>
		/// Determines if a feed URL is an Azure DevOps feed
		/// </summary>
		private static bool IsAzureDevOpsFeed(string feedUrl)
		{
			return feedUrl.Contains("pkgs.dev.azure.com", StringComparison.OrdinalIgnoreCase) ||
				   feedUrl.Contains(".visualstudio.com", StringComparison.OrdinalIgnoreCase) ||
				   feedUrl.Contains("pkgs.visualstudio.com", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Resolves package information attempting each configured feed until found.
		/// Stops searching other feeds after first successful resolution.
		/// </summary>
		public async Task<PackageInfo> ResolvePackageAsync(string packageName)
		{
			if (string.IsNullOrWhiteSpace(packageName))
			{
				return CreateNotFoundPackageInfo(packageName);
			}

			if (_packageCache.TryGetValue(packageName, out var cached))
				return cached;

			PackageInfo? foundInfo = null;

			foreach (var (feedName, repo) in _repositories)
			{
				foundInfo = await TryResolveFromFeedAsync(packageName, feedName, repo);

				if (foundInfo != null)
				{
					break;
				}
			}

			foundInfo ??= CreateNotFoundPackageInfo(packageName);

			_packageCache.TryAdd(packageName, foundInfo);
			return foundInfo;
		}

		/// <summary>
		/// Resolve multiple packages with concurrency
		/// </summary>
		public async Task<Dictionary<string, PackageInfo>> ResolvePackagesAsync(
			IEnumerable<string> packageNames,
			IEnumerable<PackageReferenceExtractor.PackageReference>? allPackageReferences = null,
			int maxConcurrency = 5)
		{
			var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
			var tasks = packageNames.Distinct().Select(async name =>
			{
				await semaphore.WaitAsync();
				try { return new { Name = name, Info = await ResolvePackageAsync(name) }; }
				finally { semaphore.Release(); }
			});
			var results = await Task.WhenAll(tasks);
			var dict = results.ToDictionary(r => r.Name, r => r.Info);

			if (allPackageReferences != null)
			{
				await CrossReferenceSourceProjectsAsync(dict, allPackageReferences);
				MarkOutdatedPackages(dict, allPackageReferences);
			}
			return dict;
		}

		/// <summary>
		/// Marks packages as outdated by comparing the latest available version with versions currently in use across projects
		/// </summary>
		/// <param name="dict">Dictionary of resolved package information</param>
		/// <param name="refs">All package references from scanned projects</param>
		private void MarkOutdatedPackages(Dictionary<string, PackageInfo> dict, IEnumerable<PackageReferenceExtractor.PackageReference> refs)
		{
			var grouped = refs.GroupBy(r => r.PackageName).ToDictionary(g => g.Key, g => g.Select(r => r.Version).Where(v => !string.IsNullOrEmpty(v)).Distinct().ToList());

			foreach (var kvp in dict)
			{
				var info = kvp.Value;

				if (string.IsNullOrEmpty(info.LatestVersion))
				{
					continue;
				}

				if (!grouped.TryGetValue(kvp.Key, out var used) || !used.Any())
				{
					continue;
				}

				try
				{
					var latest = NuGetVersion.Parse(info.LatestVersion);

					// Parse all used versions, find the oldest one that is strictly less than latest
					var oldestOutdated = used
						.Select(v => { try { return NuGetVersion.Parse(v!); } catch { return null; } })
						.Where(v => v != null && v < latest)
						.OrderBy(v => v)
						.FirstOrDefault();

					if (oldestOutdated != null)
					{
						dict[kvp.Key] = info with { IsOutdated = true, ResolvedVersion = oldestOutdated.ToNormalizedString() };
					}
				}
				catch 
				{
					// just catch and skip if version parsing fails for any reason, we don't want this to break the entire resolution process
				}
			}
		}

		/// <summary>
		/// Cross-references packages not found on NuGet.org with potential source projects in the codebase.
		/// This includes packages found on alternate feeds (e.g. private Azure DevOps feeds),
		/// which are typically internal packages that may have corresponding source repositories.
		/// </summary>
		/// <param name="dict">Dictionary of resolved package information to update with source project references</param>
		/// <param name="allRefs">All package references from scanned projects</param>
		private async Task CrossReferenceSourceProjectsAsync(Dictionary<string, PackageInfo> dict, IEnumerable<PackageReferenceExtractor.PackageReference> allRefs)
		{
			Logger.Debug("Cross-referencing packages not found on feeds with potential source projects");
			var allProjects = allRefs.GroupBy(pr => new { pr.RepositoryName, pr.ProjectPath, pr.ProjectName })
				.Select(g => new { g.Key.RepositoryName, g.Key.ProjectPath, g.Key.ProjectName, PotentialPackageName = ExtractPackageNameFromProject(g.Key.ProjectPath, g.Key.ProjectName) })
				.Where(p => !string.IsNullOrEmpty(p.PotentialPackageName)).ToList();

			var candidates = dict.Where(k => !k.Value.ExistsOnNuGetOrg)
				.ToList();

			foreach (var entry in candidates)
			{
				var sources = FindPotentialSourceProjects(entry.Key, allProjects);

				if (sources.Any())
				{
					dict[entry.Key] = entry.Value with { SourceProjects = sources };
				}
			}
		}

		/// <summary>
		/// Extracts a potential package name from a project path and project name
		/// </summary>
		/// <param name="projectPath">Full path to the project file</param>
		/// <param name="projectName">Name of the project</param>
		/// <returns>The extracted package name, or null if no suitable name could be determined</returns>
		private static string? ExtractPackageNameFromProject(string projectPath, string projectName)
		{
			var fileName = Path.GetFileNameWithoutExtension(projectPath);

			if (!string.IsNullOrEmpty(fileName) && fileName != projectName) return fileName;

			if (!string.IsNullOrEmpty(projectName) && projectName != "Unknown") return projectName;

			var segments = projectPath.Split('/', '\\', StringSplitOptions.RemoveEmptyEntries);

			if (segments.Length >= 2)
			{
				var potential = segments[segments.Length - 2];

				if (!string.IsNullOrEmpty(potential) && potential != "src" && potential != "lib") return potential;
			}

			return null;
		}

		/// <summary>
		/// Finds potential source projects that match a given package name
		/// </summary>
		/// <param name="packageName">The package name to match against</param>
		/// <param name="allProjects">Collection of all available projects with their metadata</param>
		/// <returns>List of potential source projects ordered by match confidence, limited to the top match</returns>
		private static List<ProjectReference> FindPotentialSourceProjects(string packageName, IEnumerable<dynamic> allProjects)
		{
			var list = new List<ProjectReference>();

			foreach (var p in allProjects)
			{
				var conf = CalculateMatchConfidence(packageName, p.PotentialPackageName);

				if (conf > 0.7)
					list.Add(new ProjectReference(p.PotentialPackageName, p.RepositoryName, p.ProjectPath, conf));
			}

			return list.OrderByDescending(s => s.MatchConfidence).Take(1).ToList();
		}

		/// <summary>
		/// Calculates the confidence level (0.0 to 1.0) that a project name matches a package name
		/// </summary>
		/// <param name="packageName">The package name to match</param>
		/// <param name="projectName">The project name to compare against</param>
		/// <returns>A confidence score between 0.0 (no match) and 1.0 (exact match)</returns>
		private static double CalculateMatchConfidence(string packageName, string? projectName)
		{
			if (string.IsNullOrEmpty(projectName)) return 0.0;

			if (string.Equals(packageName, projectName, StringComparison.OrdinalIgnoreCase)) return 1.0;

			if (packageName.StartsWith(projectName, StringComparison.OrdinalIgnoreCase) || projectName.StartsWith(packageName, StringComparison.OrdinalIgnoreCase))
			{
				var shorter = Math.Min(packageName.Length, projectName.Length);
				var longer = Math.Max(packageName.Length, projectName.Length);

				return (double)shorter / longer * 0.9;
			}

			if (packageName.Contains(projectName, StringComparison.OrdinalIgnoreCase) || projectName.Contains(packageName, StringComparison.OrdinalIgnoreCase)) return 0.8;

			var dist = CalculateLevenshteinDistance(packageName.ToLower(), projectName.ToLower());
			var maxLen = Math.Max(packageName.Length, projectName.Length);
			var sim = 1.0 - (double)dist / maxLen;

			return sim > 0.7 ? sim : 0.0;
		}

		/// <summary>
		/// Calculates the Levenshtein distance between two strings (minimum number of single-character edits required to transform one string into another)
		/// </summary>
		/// <param name="source">The source string</param>
		/// <param name="target">The target string</param>
		/// <returns>The Levenshtein distance as an integer</returns>
		private static int CalculateLevenshteinDistance(string source, string target)
		{
			if (string.IsNullOrEmpty(source)) return string.IsNullOrEmpty(target) ? 0 : target.Length;

			if (string.IsNullOrEmpty(target)) return source.Length;

			var d = new int[source.Length + 1, target.Length + 1];

			for (int i = 0; i <= source.Length; i++) d[i, 0] = i;

			for (int j = 0; j <= target.Length; j++) d[0, j] = j;

			for (int i = 1; i <= source.Length; i++)
				for (int j = 1; j <= target.Length; j++)
				{
					var cost = (target[j - 1] == source[i - 1]) ? 0 : 1;
					d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
				}

			return d[source.Length, target.Length];
		}

		/// <summary>
		/// Compares two versions at the specified granularity level.
		/// Each level detects only differences at that specific scope:
		/// Major detects only major diffs, Minor detects minor diffs within the same major,
		/// and Patch detects patch diffs within the same major.minor.
		/// </summary>
		public static bool VersionsDiffer(string? version1, string? version2, VersionWarningLevel level)
		{
			if (string.IsNullOrEmpty(version1) || string.IsNullOrEmpty(version2))
				return false;

			try
			{
				var v1 = NuGetVersion.Parse(version1);
				var v2 = NuGetVersion.Parse(version2);

				return level switch
				{
					VersionWarningLevel.Major => v1.Major != v2.Major,
					VersionWarningLevel.Minor => v1.Major == v2.Major && v1.Minor != v2.Minor,
					VersionWarningLevel.Patch => v1.Major == v2.Major && v1.Minor == v2.Minor && v1.Patch != v2.Patch,
					_ => false
				};
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Finds the latest available version that falls within the scope constraint relative to <paramref name="currentVersion"/>.
		/// For <see cref="VersionWarningLevel.Major"/> scope any version qualifies.
		/// For <see cref="VersionWarningLevel.Minor"/> scope only versions with the same major component qualify.
		/// For <see cref="VersionWarningLevel.Patch"/> scope only versions with the same major.minor qualify.
		/// Returns <c>null</c> when no in-scope upgrade exists or when the in-scope latest equals <paramref name="currentVersion"/>.
		/// </summary>
		public static string? FindLatestInScope(
			string? currentVersion,
			IReadOnlyList<string>? availableVersions,
			VersionWarningLevel level)
		{
			if (string.IsNullOrEmpty(currentVersion) || availableVersions == null || availableVersions.Count == 0)
			{
				return null;
			}

			if (!NuGetVersion.TryParse(currentVersion, out var current))
			{
				return null;
			}

			NuGetVersion? best = null;

			foreach (var vStr in availableVersions)
			{
				if (!NuGetVersion.TryParse(vStr, out var candidate) || candidate <= current)
				{
					continue;
				}

				bool inScope = level switch
				{
					VersionWarningLevel.Patch => candidate.Major == current.Major && candidate.Minor == current.Minor,
					VersionWarningLevel.Minor => candidate.Major == current.Major,
					VersionWarningLevel.Major => true,
					_ => false
				};

				if (inScope && (best == null || candidate > best))
				{
					best = candidate;
				}
			}

			return best?.ToNormalizedString();
		}

		/// <summary>
		/// Finds the latest in-scope version for each distinct major version band represented in <paramref name="usedVersions"/>.
		/// This is useful when a package is referenced across multiple major versions (e.g., 8.x, 9.x, 10.x) and each band
		/// should show its own latest in-scope upgrade.
		/// When only one major band is present, the result excludes any version that equals <paramref name="latestVersion"/>
		/// (since it is already shown in the "Latest:" label). When multiple bands are present, all in-scope versions are
		/// included so the user sees the complete picture across all bands.
		/// </summary>
		public static List<string> FindAllInScopeVersions(
			IEnumerable<string> usedVersions,
			IReadOnlyList<string>? availableVersions,
			VersionWarningLevel level,
			string? latestVersion)
		{
			if (availableVersions == null || availableVersions.Count == 0)
			{
				return [];
			}

			// Group used versions by major version band and pick the lowest in each band
			var representativeByMajor = usedVersions
				.Select(v => NuGetVersion.TryParse(v, out var nv) ? nv : null)
				.Where(v => v != null)
				.GroupBy(v => v!.Major)
				.Select(g => g.OrderBy(v => v).First()!)
				.ToList();

			var inScopeSet = new SortedSet<NuGetVersion>();

			foreach (var representative in representativeByMajor)
			{
				var inScope = FindLatestInScope(representative.ToNormalizedString(), availableVersions, level);

				if (inScope != null && NuGetVersion.TryParse(inScope, out var parsed))
				{
					inScopeSet.Add(parsed);
				}
			}

			// When only one band is present, omit the in-scope version if it equals latest (already shown)
			if (representativeByMajor.Count <= 1 && latestVersion != null)
			{
				if (NuGetVersion.TryParse(latestVersion, out var latest))
				{
					inScopeSet.Remove(latest);
				}
			}

			return inScopeSet.Select(v => v.ToNormalizedString()).ToList();
		}

		/// <summary>
		/// Gets the difference description between two versions
		/// </summary>
		public static string GetVersionDifference(string? version1, string? version2)
		{
			if (string.IsNullOrEmpty(version1) || string.IsNullOrEmpty(version2))
				return "unknown";

			try
			{
				var v1 = NuGetVersion.Parse(version1);
				var v2 = NuGetVersion.Parse(version2);

				if (v1.Major != v2.Major)
					return "major";
				if (v1.Minor != v2.Minor)
					return "minor";
				if (v1.Patch != v2.Patch)
					return "patch";

				return "none";
			}
			catch
			{
				return "unknown";
			}
		}

		/// <summary>
		/// Attempts to resolve package metadata from a single feed.
		/// </summary>
		private async Task<PackageInfo?> TryResolveFromFeedAsync(string packageName, string feedName, SourceRepository repo)
		{
			try
			{
				var metadataResource = await repo.GetResourceAsync<PackageMetadataResource>();
				var metadata = await metadataResource.GetMetadataAsync(
					packageName,
					includePrerelease: false,
					includeUnlisted: false,
					sourceCacheContext: new SourceCacheContext(),
					log: _logger,
					token: CancellationToken.None);

				var stableVersions = metadata.Where(m => !m.Identity.Version.IsPrerelease)
										  .OrderByDescending(m => m.Identity.Version)
										  .ToList();

					var stable = stableVersions.FirstOrDefault();

					if (stable == null)
					{
						return null;
					}

					var allVersionStrings = stableVersions
						.Select(m => m.Identity.Version.ToNormalizedString())
						.ToList();

					var latestVersion = stable.Identity.Version;
				var published     = stable.Published;
				var vulnerabilities = DetectVulnerabilities(stable);
				var isVulnerable    = vulnerabilities.Count > 0;

				var url = repo.PackageSource.Source.Contains("nuget.org")
					? $"https://www.nuget.org/packages/{packageName}" : repo.PackageSource.Source;

				var description = stable.Description?.Length > 200
					? stable.Description.Substring(0, 200) + "..."
					: stable.Description;

				return new PackageInfo(
					PackageName: packageName,
					ExistsOnNuGetOrg: repo.PackageSource.Source.Contains("nuget.org"),
					PackageUrl: url,
					Description: description,
					Authors: stable.Authors,
					ProjectUrl: stable.ProjectUrl?.ToString(),
					Published: published,
					DownloadCount: 0,
					LicenseUrl: stable.LicenseUrl?.ToString(),
					IconUrl: stable.IconUrl?.ToString(),
					Tags: stable.Tags?.Split(' ', ',', ';').Where(t => !string.IsNullOrWhiteSpace(t)).ToList() ?? new List<string>(),
					IsDeprecated: stable.IsListed == false,
					DeprecationMessage: stable.IsListed == false ? "Package is unlisted" : null,
					SourceProjects: new List<ProjectReference>(),
					LatestVersion: latestVersion.ToNormalizedString(),
					ResolvedVersion: latestVersion.ToNormalizedString(),
					IsOutdated: false,
					IsVulnerable: isVulnerable,
					Vulnerabilities: isVulnerable ? vulnerabilities : null,
					FeedName: feedName,
					AvailableVersions: allVersionStrings);
			}
			catch (Exception ex)
			{
				LogFeedLookupError(feedName, packageName, ex);

				return null;
			}
		}

		/// <summary>
		/// Detects potential vulnerabilities in a package based on description keywords and publish date.
		/// </summary>
		private static List<string> DetectVulnerabilities(IPackageSearchMetadata stable)
		{
			var vulnerabilities = new List<string>();

			if (!string.IsNullOrEmpty(stable.Description))
			{
				var desc = stable.Description.ToLowerInvariant();
				string[] vulnKeywords = { "vulnerability", "security issue", "cve", "xss", "sql injection" };

				foreach (var kw in vulnKeywords)
				{
					if (desc.Contains(kw))
					{
						vulnerabilities.Add($"Keyword detected: {kw}");
					}
				}
			}

			if (stable.Published.HasValue && stable.Published.Value < DateTimeOffset.UtcNow.AddYears(-3))
			{
				vulnerabilities.Add("Package publish date older than 3 years (potentially outdated)");
			}

			return vulnerabilities;
		}

		/// <summary>
		/// Logs a feed lookup error, distinguishing authentication failures from general errors.
		/// </summary>
		private static void LogFeedLookupError(string feedName, string packageName, Exception ex)
		{
			if (ex is FatalProtocolException && ex.Message.Contains("401"))
			{
				Logger.Warning($"Authentication failed for feed '{feedName}' while resolving {packageName}: {ex.Message}");
			}
			else
			{
				Logger.Debug($"Feed '{feedName}' lookup failed for {packageName}: {ex.Message}");
			}
		}

		private static PackageInfo CreateNotFoundPackageInfo(string packageName)
		{
			return new PackageInfo(
				PackageName: packageName,
				ExistsOnNuGetOrg: false,
				PackageUrl: null,
				Description: null,
				Authors: null,
				ProjectUrl: null,
				Published: null,
				DownloadCount: null,
				LicenseUrl: null,
				IconUrl: null,
				Tags: new List<string>(),
				IsDeprecated: false,
				DeprecationMessage: null,
				SourceProjects: new List<ProjectReference>(),
				LatestVersion: null,
				ResolvedVersion: null,
				IsOutdated: false,
				IsVulnerable: false,
				Vulnerabilities: null,
				FeedName: null);
		}

		/// <summary>
		/// Clears the package information cache
		/// </summary>
		public void ClearCache()
		{
			_packageCache.Clear();
			Logger.Debug("Package metadata cache cleared");
		}

		/// <summary>
		/// Gets cache statistics
		/// </summary>
		public (int CachedPackages, int FoundOnNuGet, int NotFound) GetCacheStats()
		{
			var cached = _packageCache.Count;
			var found = _packageCache.Values.Count(p => p.ExistsOnNuGetOrg);
			var notFound = cached - found;

			return (cached, found, notFound);
		}
	}
}