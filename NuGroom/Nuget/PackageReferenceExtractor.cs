using System.Text.RegularExpressions;
using System.Xml;

using NuGroom.Configuration;

namespace NuGroom.Nuget
{
	/// <summary>
	/// Identifies the source format from which a package reference was extracted.
	/// </summary>
	public enum PackageSourceKind
	{
		/// <summary>SDK-style project file with inline <c>&lt;PackageReference&gt;</c> elements.</summary>
		ProjectFile,

		/// <summary>Central Package Management via <c>Directory.Packages.props</c>.</summary>
		CentralPackageManagement,

		/// <summary>Legacy <c>packages.config</c> file.</summary>
		PackagesConfig
	}

	/// <summary>
	/// Extracts, filters and formats <c>PackageReference</c> entries from project files (.csproj, .vbproj, .fsproj), including optional NuGet metadata resolution.
	/// </summary>
	public class PackageReferenceExtractor
	{
		/// <summary>
		/// Regex used as a fallback mechanism to locate PackageReference Include attributes when XML parsing fails.
		/// </summary>
		private static readonly Regex PackageReferenceRegex = new(
			@"<PackageReference\s+[^>]*Include\s*=\s*[""']([^""']+)[""'][^>]*>",
			RegexOptions.IgnoreCase | RegexOptions.Compiled);

		/// <summary>
		/// Default exclusion patterns - packages that start with these prefixes will be excluded.
		/// </summary>
		public static readonly string[] DefaultExclusionPrefixes = [];

		/// <summary>
		/// Package exclusion configuration.
		/// </summary>
		public class ExclusionList
		{
			/// <summary>
			/// Package name prefixes to exclude (e.g., "Microsoft.", "System.").
			/// </summary>
			public List<string> ExcludedPrefixes { get; set; } = new(DefaultExclusionPrefixes);

			/// <summary>
			/// Exact package names to exclude (e.g., "Newtonsoft.Json", "Serilog").
			/// </summary>
			public List<string> ExcludedPackages { get; set; } = new();

			/// <summary>
			/// Regular expression patterns to exclude packages (e.g., <c>@"^.*\.Test$"</c> for test packages).
			/// </summary>
			public List<string> ExcludedPatterns { get; set; } = new();

			/// <summary>
			/// Whether to use case-sensitive matching (default: false).
			/// </summary>
			public bool CaseSensitive { get; set; } = false;

			/// <summary>
			/// Adds a prefix exclusion (e.g., "MyCompany." to exclude all MyCompany.* packages).
			/// </summary>
			/// <param name="prefix">The prefix to exclude.</param>
			public void AddPrefixExclusion(string prefix)
			{
				if (!string.IsNullOrWhiteSpace(prefix) && !ExcludedPrefixes.Contains(prefix))
				{
					ExcludedPrefixes.Add(prefix);
				}
			}

			/// <summary>
			/// Adds an exact package name exclusion.
			/// </summary>
			/// <param name="packageName">The package name to exclude.</param>
			public void AddPackageExclusion(string packageName)
			{
				if (!string.IsNullOrWhiteSpace(packageName) && !ExcludedPackages.Contains(packageName))
				{
					ExcludedPackages.Add(packageName);
				}
			}

			/// <summary>
			/// Adds a regex pattern exclusion.
			/// </summary>
			/// <param name="pattern">The regex pattern used to match packages to exclude.</param>
			public void AddPatternExclusion(string pattern)
			{
				if (!string.IsNullOrWhiteSpace(pattern) && !ExcludedPatterns.Contains(pattern))
				{
					ExcludedPatterns.Add(pattern);
				}
			}

			/// <summary>
			/// Checks if a package should be excluded based on the configured rules.
			/// </summary>
			/// <param name="packageName">The package name to evaluate.</param>
			/// <returns><c>true</c> if the package matches any exclusion rule; otherwise <c>false</c>.</returns>
			public bool ShouldExclude(string packageName)
			{
				if (string.IsNullOrWhiteSpace(packageName))
					return true;

				var comparison = CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

				return IsExcludedByPrefix(packageName, comparison) ||
				       IsExcludedByExactMatch(packageName, comparison) ||
				       IsExcludedByPattern(packageName);
			}

			/// <summary>
			/// Checks if a package is excluded by prefix matching.
			/// </summary>
			private bool IsExcludedByPrefix(string packageName, StringComparison comparison)
			{
				foreach (var prefix in ExcludedPrefixes)
				{
					if (packageName.StartsWith(prefix, comparison))
						return true;
				}

				return false;
			}

			/// <summary>
			/// Checks if a package is excluded by exact name matching.
			/// </summary>
			private bool IsExcludedByExactMatch(string packageName, StringComparison comparison)
			{
				foreach (var excludedPackage in ExcludedPackages)
				{
					if (packageName.Equals(excludedPackage, comparison))
						return true;
				}

				return false;
			}

			/// <summary>
			/// Checks if a package is excluded by regex pattern matching.
			/// </summary>
			private bool IsExcludedByPattern(string packageName)
			{
				var regexOptions = CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;

				foreach (var pattern in ExcludedPatterns)
				{
					try
					{
						if (Regex.IsMatch(packageName, pattern, regexOptions))
							return true;
					}
					catch (Exception ex)
					{
						Logger.Warning($"Invalid regex pattern '{pattern}': {ex.Message}");
					}
				}

				return false;
			}

			/// <summary>
			/// Creates a default exclusion list with Microsoft.*, System.* and coverlet.* prefixes.
			/// </summary>
			/// <returns>Initialized <see cref="ExclusionList"/> instance with defaults.</returns>
			public static ExclusionList CreateDefault()
			{
				return new ExclusionList();
			}

			/// <summary>
			/// Creates an exclusion list with no default exclusions (include everything).
			/// </summary>
			/// <returns>Initialized <see cref="ExclusionList"/> instance with empty rules.</returns>
			public static ExclusionList CreateEmpty()
			{
				return new ExclusionList
				{
					ExcludedPrefixes  = new List<string>(),
					ExcludedPackages  = new List<string>(),
					ExcludedPatterns  = new List<string>()
				};
			}
		}

		/// <summary>
		/// Currently configured exclusion list.
		/// </summary>
		private readonly ExclusionList _exclusionList;

		/// <summary>
		/// Initializes a new instance with default exclusions (Microsoft.*, System.*).
		/// </summary>
		public PackageReferenceExtractor() : this(ExclusionList.CreateDefault())
		{
		}

		/// <summary>
		/// Initializes a new instance with a custom exclusion list.
		/// </summary>
		/// <param name="exclusionList">The exclusion list to apply (falls back to defaults if null).</param>
		public PackageReferenceExtractor(ExclusionList exclusionList)
		{
			_exclusionList = exclusionList ?? ExclusionList.CreateDefault();
		}

		/// <summary>
		/// Represents a package reference found in a project file.
		/// </summary>
		/// <param name="PackageName">Package identifier.</param>
		/// <param name="Version">Referenced version (may be null or unspecified).</param>
		/// <param name="ProjectPath">Full path to the project file.</param>
		/// <param name="RepositoryName">Repository the project belongs to.</param>
		/// <param name="ProjectName">Logical project name.</param>
		/// <param name="LineNumber">Approximate line number where the reference appears.</param>
		/// <param name="SourceKind">Identifies the format from which this reference was extracted.</param>
		/// <param name="NuGetInfo">Resolved NuGet metadata (optional).</param>
		public record PackageReference(
			string PackageName,
			string? Version,
			string ProjectPath,
			string RepositoryName,
			string ProjectName,
			int LineNumber,
			PackageSourceKind SourceKind = PackageSourceKind.ProjectFile,
			NuGetPackageResolver.PackageInfo? NuGetInfo = null);

		/// <summary>
		/// Extracts package references from project file content.
		/// </summary>
		/// <param name="csprojContent">Raw XML content of the project file.</param>
		/// <param name="repositoryName">Repository label for provenance.</param>
		/// <param name="projectPath">Full path to the project file.</param>
		/// <param name="projectName">Optional project name override.</param>
		/// <returns>List of discovered <see cref="PackageReference"/> entries after applying exclusions.</returns>
		/// <exception cref="PackageExtractionException">Thrown when both XML and regex parsing fail unexpectedly.</exception>
		public List<PackageReference> ExtractPackageReferences(
			string csprojContent,
			string repositoryName,
			string projectPath,
			string? projectName = null)
		{
			var packageReferences = new List<PackageReference>();

			if (string.IsNullOrWhiteSpace(csprojContent))
			{
				Logger.Debug($"Empty content for {projectPath}");
				return packageReferences;
			}

			try
			{
				Logger.Debug($"Extracting package references from {projectPath} using XML parsing");
				packageReferences.AddRange(ExtractUsingXmlParsing(csprojContent, repositoryName, projectPath, projectName));
			}
			catch (XmlException ex)
			{
				Logger.Warning($"XML parsing failed for {projectPath}: {ex.Message}, using regex fallback");

				try
				{
					packageReferences.AddRange(ExtractUsingRegex(csprojContent, repositoryName, projectPath, projectName));
				}
				catch (Exception regexEx)
				{
					throw new PackageExtractionException($"Both XML and regex parsing failed for {projectPath}", regexEx);
				}
			}
			catch (Exception ex)
			{
				throw new PackageExtractionException($"Failed to extract package references from {projectPath}: {ex.Message}", ex);
			}

			// Filter out excluded packages using the configured exclusion list
			var filteredReferences = packageReferences
				.Where(pr => !_exclusionList.ShouldExclude(pr.PackageName))
				.ToList();

			var excludedCount = packageReferences.Count - filteredReferences.Count;

			// Deduplicate entries that appear multiple times in the same csproj (e.g. in different ItemGroups)
			var deduplicatedReferences = filteredReferences
				.GroupBy(pr => pr.PackageName, StringComparer.OrdinalIgnoreCase)
				.Select(g => g.First())
				.ToList();

			var duplicateCount = filteredReferences.Count - deduplicatedReferences.Count;

			if (duplicateCount > 0)
			{
				Logger.Warning($"Removed {duplicateCount} duplicate PackageReference(s) in {projectPath}");
			}

			Logger.Debug($"Extracted {packageReferences.Count} total package references, {deduplicatedReferences.Count} after filtering ({excludedCount} excluded, {duplicateCount} duplicate) from {projectPath}");

			return deduplicatedReferences;
		}

		/// <summary>
		/// Enhanced extraction method that includes NuGet package resolution with source project cross-referencing.
		/// </summary>
		/// <param name="csprojContent">Raw XML content of the project file.</param>
		/// <param name="repositoryName">Repository label for provenance.</param>
		/// <param name="projectPath">Full path to the project file.</param>
		/// <param name="nugetResolver">Resolver used to fetch NuGet metadata (optional).</param>
		/// <param name="projectName">Optional project name override.</param>
		/// <param name="allExistingReferences">Collection of all previously discovered references for cross-referencing internal packages.</param>
		/// <returns>List of <see cref="PackageReference"/> enriched with NuGet metadata when available.</returns>
		public async Task<List<PackageReference>> ExtractPackageReferencesAsync(
			string csprojContent,
			string repositoryName,
			string projectPath,
			NuGetPackageResolver? nugetResolver = null,
			string? projectName = null,
			List<PackageReference>? allExistingReferences = null)
		{
			// First extract basic package references
			var basicReferences = ExtractPackageReferences(csprojContent, repositoryName, projectPath, projectName);

			// If no NuGet resolver provided, return basic references
			if (nugetResolver == null)
			{
				return basicReferences;
			}

			// Resolve NuGet information for all packages with cross-referencing
			var packageNames = basicReferences.Select(pr => pr.PackageName).Distinct().ToList();
			Logger.Debug($"Resolving NuGet metadata for {packageNames.Count} unique packages from {projectPath}...");

			// Combine with existing references for cross-referencing
			var allReferencesForCrossRef = allExistingReferences?.Concat(basicReferences) ?? basicReferences;

			var nugetInfos = await nugetResolver.ResolvePackagesAsync(packageNames, allReferencesForCrossRef);

			// Create enhanced references with NuGet information
			var enhancedReferences = basicReferences.Select(pr =>
			{
				var nugetInfo = nugetInfos.TryGetValue(pr.PackageName, out var info) ? info : null;
				return pr with { NuGetInfo = nugetInfo };
			}).ToList();

			var foundSourceProjects = nugetInfos.Values.Count(info => !info.ExistsOnNuGetOrg && info.SourceProjects?.Count > 0);

			if (foundSourceProjects > 0)
			{
				Logger.Debug($"Cross-referenced {foundSourceProjects} internal packages with potential source projects");
			}

			return enhancedReferences;
		}

		/// <summary>
		/// Gets the current exclusion list configuration.
		/// </summary>
		/// <returns>The active <see cref="ExclusionList"/> instance.</returns>
		public ExclusionList GetExclusionList()
		{
			return _exclusionList;
		}

		/// <summary>
		/// Parses the project XML to extract PackageReference entries using DOM selection.
		/// </summary>
		/// <param name="csprojContent">XML content of the project file.</param>
		/// <param name="repositoryName">Repository name used for attribution.</param>
		/// <param name="projectPath">Path to the project file.</param>
		/// <param name="projectName">Optional project name.</param>
		/// <returns>List of extracted <see cref="PackageReference"/> entries.</returns>
		/// <exception cref="XmlException">Propagated when XML cannot be loaded.</exception>
		private List<PackageReference> ExtractUsingXmlParsing(
			string csprojContent,
			string repositoryName,
			string projectPath,
			string? projectName)
		{
			var packageReferences = new List<PackageReference>();

			try
			{
				var doc = new XmlDocument();
				doc.LoadXml(csprojContent);

				var packageNodes = doc.SelectNodes("//PackageReference[@Include]");

				if (packageNodes != null)
				{
					Logger.Debug($"Found {packageNodes.Count} PackageReference nodes in {projectPath}");

					foreach (XmlNode node in packageNodes)
					{
						var includeAttr = node.Attributes?["Include"];
						if (includeAttr != null)
						{
							var packageName = includeAttr.Value;
							var version = node.Attributes?["Version"]?.Value;

							// Try to get line number (this is approximate)
							var lineNumber = GetLineNumber(csprojContent, packageName);

							packageReferences.Add(new PackageReference(
								packageName,
								version,
								projectPath,
								repositoryName,
								projectName ?? "Unknown",
								lineNumber));

							Logger.Debug($"Found package reference: {packageName} v{version ?? "unspecified"}");
						}
					}
				}
				else
				{
					Logger.Debug($"No PackageReference nodes found in {projectPath}");
				}
			}
			catch (Exception ex)
			{
				Logger.Error($"Error parsing XML for {projectPath}: {ex.Message}");
				throw;
			}

			return packageReferences;
		}

		/// <summary>
		/// Uses a regex line-by-line fallback to extract PackageReference entries when XML parsing fails.
		/// </summary>
		/// <param name="csprojContent">Raw project file content.</param>
		/// <param name="repositoryName">Repository name for attribution.</param>
		/// <param name="projectPath">Path to the project file.</param>
		/// <param name="projectName">Optional project name override.</param>
		/// <returns>List of extracted <see cref="PackageReference"/> entries.</returns>
		private List<PackageReference> ExtractUsingRegex(
			string csprojContent,
			string repositoryName,
			string projectPath,
			string? projectName)
		{
			var packageReferences = new List<PackageReference>();
			var lines = csprojContent.Split('\n', StringSplitOptions.None);

			Logger.Debug($"Using regex parsing for {projectPath}, processing {lines.Length} lines");

			for (int i = 0; i < lines.Length; i++)
			{
				var line = lines[i];
				var matches = PackageReferenceRegex.Matches(line);

				foreach (Match match in matches)
				{
					if (match.Success && match.Groups.Count > 1)
					{
						var packageName = match.Groups[1].Value;
						var version = ExtractVersionFromLine(line);

						packageReferences.Add(new PackageReference(
							packageName,
							version,
							projectPath,
							repositoryName,
							projectName ?? "Unknown",
							i + 1)); // 1-based line numbering

						Logger.Debug($"Found package reference via regex: {packageName} v{version ?? "unspecified"}");
					}
				}
			}

			return packageReferences;
		}

		/// <summary>
		/// Extracts a Version attribute value from a single XML line, if present.
		/// </summary>
		/// <param name="line">The line of text to inspect.</param>
		/// <returns>The version string if found; otherwise <c>null</c>.</returns>
		private static string? ExtractVersionFromLine(string line)
		{
			var versionMatch = Regex.Match(line, @"Version\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
			return versionMatch.Success ? versionMatch.Groups[1].Value : null;
		}

		/// <summary>
		/// Attempts to locate the line number containing a PackageReference Include attribute.
		/// </summary>
		/// <param name="content">Full project file content.</param>
		/// <param name="packageName">Package name to find.</param>
		/// <returns>1-based line number if found; otherwise 0.</returns>
		private static int GetLineNumber(string content, string packageName)
		{
			var lines = content.Split('\n');
			for (int i = 0; i < lines.Length; i++)
			{
				if (lines[i].Contains($"Include=\"{packageName}\"") ||
					lines[i].Contains($"Include='{packageName}'"))
				{
					return i + 1; // 1-based line numbering
				}
			}

			return 0;
		}

		/// <summary>
		/// Formats package references for console output with NuGet information and source projects.
		/// </summary>
		/// <param name="packageReferences">References to display.</param>
		/// <param name="exclusionList">Optional exclusion list used (for summary output).</param>
		/// <param name="showNuGetDetails">If true, include NuGet metadata where available.</param>
		/// <param name="showDetailedInfo">If true, include extended metadata (authors, description, etc.).</param>
		public static void PrintPackageReferences(List<PackageReference> packageReferences, ExclusionList? exclusionList = null, bool showNuGetDetails = true, bool showDetailedInfo = false, Dictionary<string, string?>? pinnedPackages = null, VersionWarningConfig? warningConfig = null)
		{
			if (!packageReferences.Any())
			{
				Logger.Info("No package references found after applying exclusion filters.");
				return;
			}

			var exclusionInfo = exclusionList != null ? GetExclusionSummary(exclusionList) : "Microsoft.* and System.* packages";
			Logger.Info($"Found {packageReferences.Count} package references (excluding {exclusionInfo}):");

			ConsoleWriter.Out.WriteLine();

			var groupedByRepo = packageReferences
				.GroupBy(pr => pr.RepositoryName)
				.OrderBy(g => g.Key);

			foreach (var repoGroup in groupedByRepo)
			{
				ConsoleWriter.Out.WriteLine($"Repository: {repoGroup.Key}").WriteLine(new string('-', 50));

				var groupedByProject = repoGroup
					.GroupBy(pr => pr.ProjectPath)
					.OrderBy(g => g.Key);

				foreach (var projectGroup in groupedByProject)
				{
					ConsoleWriter.Out.WriteLine($"  Project: {projectGroup.Key}");

					foreach (var packageRef in projectGroup.OrderBy(pr => pr.PackageName))
					{
						var isPinned = pinnedPackages?.ContainsKey(packageRef.PackageName) == true;
						PrintSinglePackageReference(packageRef, showNuGetDetails, showDetailedInfo, isPinned, warningConfig);
					}

					ConsoleWriter.Out.WriteLine();
				}
			}
		}

		/// <summary>
		/// Prints a single package reference with optional NuGet details
		/// </summary>
		private static void PrintSinglePackageReference(PackageReference packageRef, bool showNuGetDetails, bool showDetailedInfo, bool isPinned, VersionWarningConfig? warningConfig = null)
		{
			var versionInfo = !string.IsNullOrEmpty(packageRef.Version)
				? $" (Version: {packageRef.Version})"
				: " (Version: Not specified)";
			ConsoleWriter.Out.Write($"    - {packageRef.PackageName}{versionInfo}");

			if (isPinned)
			{
				ConsoleWriter.Out.WriteColored(ConsoleColor.DarkYellow, " [PINNED]");
			}

			if (showNuGetDetails && packageRef.NuGetInfo != null)
			{
				var level = warningConfig?.GetLevelForPackage(packageRef.PackageName) ?? VersionWarningLevel.None;
				PrintNuGetDetails(packageRef.NuGetInfo, showDetailedInfo, isPinned, packageRef.Version, level);
			}

			ConsoleWriter.Out.WriteLine();
		}

		/// <summary>
		/// Prints NuGet package details including URL, deprecation, vulnerabilities, and source information
		/// </summary>
		private static void PrintNuGetDetails(NuGetPackageResolver.PackageInfo nugetInfo, bool showDetailedInfo, bool isPinned, string? currentVersion = null, VersionWarningLevel level = VersionWarningLevel.None)
		{
			if (nugetInfo.ExistsOnNuGetOrg)
			{
				PrintNuGetOrgPackageDetails(nugetInfo, showDetailedInfo, isPinned, currentVersion, level);
			}
			else
			{
				PrintNonNuGetOrgPackageDetails(nugetInfo);
			}
		}

		/// <summary>
		/// Prints details for packages found on NuGet.org including URL, description, authors, deprecation, and vulnerabilities
		/// </summary>
		private static void PrintNuGetOrgPackageDetails(NuGetPackageResolver.PackageInfo nugetInfo, bool showDetailedInfo, bool isPinned, string? currentVersion = null, VersionWarningLevel level = VersionWarningLevel.None)
		{
			ConsoleWriter.Out.WriteColored(ConsoleColor.Cyan, $" [{nugetInfo.PackageUrl}]");

			if (showDetailedInfo)
			{
				PrintDetailedPackageInfo(nugetInfo);
			}

			PrintPackageWarnings(nugetInfo, isPinned, currentVersion, level);
		}

		/// <summary>
		/// Prints detailed package information including description and authors
		/// </summary>
		private static void PrintDetailedPackageInfo(NuGetPackageResolver.PackageInfo nugetInfo)
		{
			if (!string.IsNullOrEmpty(nugetInfo.Description))
			{
				ConsoleWriter.Out.WriteLine()
				 .WriteLineColored(ConsoleColor.Gray, $"      \U0001F4DD {nugetInfo.Description}");
			}

			if (!string.IsNullOrEmpty(nugetInfo.Authors))
			{
				ConsoleWriter.Out.WriteLineColored(ConsoleColor.Gray, $"      \U0001F464 Authors: {nugetInfo.Authors}");
			}
		}

		/// <summary>
		/// Prints package warnings including deprecation, outdated versions, and vulnerabilities
		/// </summary>
		private static void PrintPackageWarnings(NuGetPackageResolver.PackageInfo nugetInfo, bool isPinned = false, string? currentVersion = null, VersionWarningLevel level = VersionWarningLevel.None)
		{
			if (nugetInfo.IsDeprecated)
			{
				ConsoleWriter.Out.Red()
				 .WriteLine()
				 .WriteLine($"      \U000026A0\uFE0F DEPRECATED: {nugetInfo.DeprecationMessage}")
				 .ResetColor();
			}

			if (nugetInfo.IsOutdated && !isPinned
				&& !string.IsNullOrEmpty(nugetInfo.LatestVersion)
				&& !string.Equals(currentVersion, nugetInfo.LatestVersion, StringComparison.OrdinalIgnoreCase))
			{
				var inScope = level != VersionWarningLevel.None
					? NuGetPackageResolver.FindLatestInScope(currentVersion, nugetInfo.AvailableVersions, level)
					: null;

				ConsoleWriter.Out.WriteColored(ConsoleColor.Yellow, $" \U000023F3 Latest: {nugetInfo.LatestVersion}");

				if (inScope != null && !string.Equals(inScope, nugetInfo.LatestVersion, StringComparison.OrdinalIgnoreCase))
				{
					ConsoleWriter.Out.WriteColored(ConsoleColor.Green, $", In-Scope: {inScope}");
				}
			}

			if (nugetInfo.IsVulnerable)
			{
				ConsoleWriter.Out.Red();

				if (nugetInfo.Vulnerabilities == null || !nugetInfo.Vulnerabilities.Any())
				{
					ConsoleWriter.Out.WriteLine($"      \U0001F6A8 Vulnerable package - no specific vulnerabilities listed");
				}
				else
				{
					foreach (var v in nugetInfo.Vulnerabilities)
					{
						ConsoleWriter.Out.WriteLine($"      \U0001F6A8 Vulnerability: {v}");
					}
				}

				ConsoleWriter.Out.ResetColor();
			}
		}

		/// <summary>
		/// Prints details for packages not found on NuGet.org (alternative feeds or internal packages)
		/// </summary>
		private static void PrintNonNuGetOrgPackageDetails(NuGetPackageResolver.PackageInfo nugetInfo)
		{
			if (!string.IsNullOrEmpty(nugetInfo.FeedName))
			{
				PrintAlternateFeedInfo(nugetInfo.FeedName);
			}
			else if (nugetInfo.SourceProjects?.Count > 0)
			{
				PrintSourceProjectInfo(nugetInfo.SourceProjects.First());
			}
			else
			{
				PrintNotFoundWarning();
			}
		}

		/// <summary>
		/// Prints information about a package found on an alternative feed
		/// </summary>
		private static void PrintAlternateFeedInfo(string feedName)
		{
			ConsoleWriter.Out.WriteColored(ConsoleColor.Cyan, $" [Feed: {feedName}]");
		}

		/// <summary>
		/// Prints information about a package found through source project cross-referencing
		/// </summary>
		private static void PrintSourceProjectInfo(NuGetPackageResolver.ProjectReference sourceProject)
		{
			ConsoleWriter.Out.WriteColored(ConsoleColor.Green, $" [\U0001F50D Source: {sourceProject.ProjectName} in {sourceProject.RepositoryName} ]");
		}

		/// <summary>
		/// Prints a warning for packages not found in any feed
		/// </summary>
		private static void PrintNotFoundWarning()
		{
			ConsoleWriter.Out.WriteColored(ConsoleColor.Yellow, " [Not found in any feed]");
		}

		/// <summary>
		/// Prints a detailed package summary showing usage count, version information, NuGet metadata, and source projects.
		/// </summary>
		/// <param name="packageReferences">Packages to summarize.</param>
		/// <param name="showNuGetDetails">If true, include NuGet metadata indicators.</param>
		/// <param name="showDetailedInfo">If true, include extended metadata (authors, published date, tags).</param>
		public static void PrintPackageSummary(List<PackageReference> packageReferences, bool showNuGetDetails = true, bool showDetailedInfo = false, Dictionary<string, string?>? pinnedPackages = null, VersionWarningConfig? warningConfig = null)
		{
			if (!packageReferences.Any())
			{
				return;
			}

			var w = ConsoleWriter.Out;

			ConsoleWriter.Out.WriteLine("\n" + new string('-', 80))
			 .WriteLine("PACKAGE SUMMARY")
			 .WriteLine(new string('-', 80));

			var packageSummary = GroupPackagesByName(packageReferences);

			PrintPackageList(packageSummary, showNuGetDetails, showDetailedInfo, pinnedPackages, warningConfig);

			PrintSummaryStatistics(packageSummary, showNuGetDetails, pinnedPackages, warningConfig);

			// Detailed sections for each category
			if (showNuGetDetails)
			{
				PrintCategorySection("DEPRECATED PACKAGES", packageSummary.Where(p => p.NuGetInfo?.IsDeprecated == true), warningConfig);
				PrintCategorySection("OUTDATED PACKAGES", packageSummary.Where(p => p.NuGetInfo?.IsOutdated == true && pinnedPackages?.ContainsKey(p.PackageName) != true && !IsUpdateableOnly(p.NuGetInfo, (string)p.PackageName, warningConfig)), warningConfig);
				PrintCategorySection("UPDATEABLE PACKAGES", packageSummary.Where(p => p.NuGetInfo?.IsOutdated == true && pinnedPackages?.ContainsKey(p.PackageName) != true && IsUpdateableOnly(p.NuGetInfo, (string)p.PackageName, warningConfig)), warningConfig);
				PrintCategorySection("VULNERABLE PACKAGES", packageSummary.Where(p => p.NuGetInfo?.IsVulnerable == true), warningConfig);
			}
		}

		/// <summary>
		/// Groups package references by package name and creates summary information.
		/// </summary>
		private static IOrderedEnumerable<dynamic> GroupPackagesByName(List<PackageReference> packageReferences)
		{
			return packageReferences
				.GroupBy(pr => pr.PackageName)
				.Select(g => new
				{
					PackageName = g.Key,
					TotalReferences = g.Count(),
					Versions = g.Where(pr => !string.IsNullOrEmpty(pr.Version))
									   .GroupBy(pr => pr.Version!)
									   .Select(vg => new { Version = vg.Key, Count = vg.Count() })
									   .OrderByDescending(v => v.Count)
									   .ThenBy(v => v.Version)
									   .ToList(),
					UnspecifiedVersionCount = g.Count(pr => string.IsNullOrEmpty(pr.Version)),
					NuGetInfo = g.FirstOrDefault()?.NuGetInfo // All references of same package should have same NuGet info
				})
				.OrderByDescending(p => p.TotalReferences)
				.ThenBy(p => p.PackageName);
		}

		/// <summary>
		/// Prints the list of packages with their details.
		/// </summary>
		private static void PrintPackageList(IEnumerable<dynamic> packageSummary, bool showNuGetDetails, bool showDetailedInfo, Dictionary<string, string?>? pinnedPackages, VersionWarningConfig? warningConfig = null)
		{
			foreach (var package in packageSummary)
			{
				PrintSinglePackageSummary(package, showNuGetDetails, showDetailedInfo, pinnedPackages, warningConfig);
			}
		}

		/// <summary>
		/// Prints a single package summary entry.
		/// </summary>
		private static void PrintSinglePackageSummary(dynamic package, bool showNuGetDetails, bool showDetailedInfo, Dictionary<string, string?>? pinnedPackages, VersionWarningConfig? warningConfig = null)
		{
			var versionCount = CalculateVersionCount(package);
			var versionText = versionCount == 1 ? "version" : "versions";

			ConsoleWriter.Out.Write($"{package.PackageName}: {package.TotalReferences} reference(s) across {versionCount} {versionText}");

			PrintPackageStatusIndicators(package, showNuGetDetails, pinnedPackages, warningConfig);

			ConsoleWriter.Out.WriteLine();

			PrintVersionBreakdown(package, versionCount);

			PrintPackageNuGetDetails(package, showNuGetDetails, showDetailedInfo, pinnedPackages, warningConfig);

			ConsoleWriter.Out.WriteLine();
		}

		/// <summary>
		/// Calculates the total version count for a package including unspecified versions.
		/// </summary>
		private static int CalculateVersionCount(dynamic package)
		{
			var versionCount = package.Versions.Count;

			if (package.UnspecifiedVersionCount > 0)
				versionCount++;

			return versionCount;
		}

		/// <summary>
		/// Prints status indicators for a package (NuGet availability, deprecation, etc.).
		/// </summary>
		private static void PrintPackageStatusIndicators(dynamic package, bool showNuGetDetails, Dictionary<string, string?>? pinnedPackages, VersionWarningConfig? warningConfig = null)
		{
			if (!showNuGetDetails || package.NuGetInfo == null)
				return;

			var isPinned = pinnedPackages?.ContainsKey(package.PackageName) == true;

			if (package.NuGetInfo.ExistsOnNuGetOrg)
			{
				PrintNuGetOrgStatusIndicators(package.NuGetInfo, isPinned, pinnedPackages, package.PackageName, warningConfig);
			}
			else
			{
				PrintAlternateSourceIndicators(package.NuGetInfo);
			}
		}

		/// <summary>
		/// Prints status indicators for packages available on NuGet.org.
		/// </summary>
		private static void PrintNuGetOrgStatusIndicators(dynamic nugetInfo, bool isPinned, Dictionary<string, string?>? pinnedPackages, string packageName, VersionWarningConfig? warningConfig = null)
		{
			ConsoleWriter.Out.WriteColored(ConsoleColor.Green, " \u2713");

			if (nugetInfo.IsDeprecated)
			{
				ConsoleWriter.Out.WriteColored(ConsoleColor.Red, " [DEPRECATED]");
			}

			if (isPinned)
			{
				var pinnedVersion = pinnedPackages![packageName] ?? "current";
				ConsoleWriter.Out.WriteColored(ConsoleColor.DarkYellow, $" [PINNED: {pinnedVersion}]");
			}
			else if (nugetInfo.IsOutdated)
			{
				if (IsUpdateableOnly(nugetInfo, packageName, warningConfig))
				{
					ConsoleWriter.Out.WriteColored(ConsoleColor.Cyan, " [UPDATEABLE]");
				}
				else
				{
					ConsoleWriter.Out.WriteColored(ConsoleColor.Yellow, " [OUTDATED]");
				}
			}

			if (nugetInfo.IsVulnerable)
			{
				ConsoleWriter.Out.WriteColored(ConsoleColor.Red, " [VULNERABLE]");
			}
		}

		/// <summary>
		/// Determines whether an outdated package is only updateable to a higher major version
		/// (i.e., the used version is already the latest within the configured scope).
		/// </summary>
		private static bool IsUpdateableOnly(dynamic nugetInfo, string packageName, VersionWarningConfig? warningConfig)
		{
			var level = warningConfig?.GetLevelForPackage(packageName) ?? VersionWarningLevel.None;

			if (level == VersionWarningLevel.None || level == VersionWarningLevel.Major)
			{
				return false;
			}

			string? resolvedVersion = nugetInfo.ResolvedVersion;

			if (string.IsNullOrEmpty(resolvedVersion))
			{
				return false;
			}

			var inScopeUpdate = NuGetPackageResolver.FindLatestInScope(resolvedVersion, nugetInfo.AvailableVersions, level);

			return inScopeUpdate == null;
		}

		/// <summary>
		/// Prints status indicators for packages from alternate sources (not NuGet.org).
		/// </summary>
		private static void PrintAlternateSourceIndicators(dynamic nugetInfo)
		{
			if (!string.IsNullOrEmpty(nugetInfo.FeedName))
			{
				ConsoleWriter.Out.WriteColored(ConsoleColor.Cyan, $" [Feed: {nugetInfo.FeedName}]");
			}
			else if (nugetInfo.SourceProjects != null && nugetInfo.SourceProjects.Count > 0)
			{
				ConsoleWriter.Out.WriteColored(ConsoleColor.Blue, " [INTERNAL]");
			}
			else
			{
				ConsoleWriter.Out.WriteColored(ConsoleColor.Yellow, " \U000026A0\uFE0F");
			}
		}

		/// <summary>
		/// Prints version breakdown for packages with multiple versions.
		/// </summary>
		private static void PrintVersionBreakdown(dynamic package, int versionCount)
		{
			if (versionCount <= 1 && package.Versions.Count == 0)
				return;

			// Show specified versions
			foreach (var version in package.Versions)
			{
				ConsoleWriter.Out.WriteLine($"  └─ v{version.Version}: {version.Count} reference(s)");
			}

			// Show unspecified version count
			if (package.UnspecifiedVersionCount > 0)
			{
				ConsoleWriter.Out.WriteLine($"  └─ (unspecified): {package.UnspecifiedVersionCount} reference(s)");
			}

			// Highlight version inconsistencies
			if (versionCount > 1)
			{
				ConsoleWriter.Out.WriteLineColored(ConsoleColor.Yellow, $"  \U000026A0\uFE0F Multiple versions detected for {package.PackageName}");
			}
		}

		/// <summary>
		/// Prints detailed NuGet information for a package.
		/// </summary>
		private static void PrintPackageNuGetDetails(dynamic package, bool showNuGetDetails, bool showDetailedInfo, Dictionary<string, string?>? pinnedPackages, VersionWarningConfig? warningConfig = null)
		{
			if (!showNuGetDetails || package.NuGetInfo == null)
				return;

			var isPinnedDetail = pinnedPackages?.ContainsKey(package.PackageName) == true;

			if (package.NuGetInfo.ExistsOnNuGetOrg)
			{
				var level = warningConfig?.GetLevelForPackage(package.PackageName) ?? VersionWarningLevel.None;
				var usedVersions = ((IEnumerable<dynamic>)package.Versions).Select(v => (string)v.Version).ToList();
				PrintNuGetOrgDetails(package.NuGetInfo, isPinnedDetail, pinnedPackages, package.PackageName, showDetailedInfo, level, usedVersions);
			}
			else
			{
				PrintAlternateSourceDetails(package.NuGetInfo);
			}
		}

		/// <summary>
		/// Prints detailed information for packages on NuGet.org.
		/// </summary>
		private static void PrintNuGetOrgDetails(dynamic nugetInfo, bool isPinned, Dictionary<string, string?>? pinnedPackages, string packageName, bool showDetailedInfo, VersionWarningLevel level = VersionWarningLevel.None, List<string>? usedVersions = null)
		{
			ConsoleWriter.Out.WriteLineColored(ConsoleColor.Cyan, $"  \U0001F517 {nugetInfo.PackageUrl}");

			if (isPinned)
			{
				var pinnedVer = pinnedPackages![packageName] ?? "current version";
				ConsoleWriter.Out.WriteLineColored(ConsoleColor.DarkYellow, $"  \U0001F4CC Pinned to {pinnedVer}");
			}
			else if (nugetInfo.IsOutdated && !string.IsNullOrEmpty(nugetInfo.LatestVersion))
			{
				List<string> allInScope = (level != VersionWarningLevel.None && usedVersions != null && usedVersions.Count > 0)
					? NuGetPackageResolver.FindAllInScopeVersions(usedVersions, nugetInfo.AvailableVersions, level, (string?)nugetInfo.LatestVersion)
					: [];

				if (allInScope.Count > 0)
				{
					ConsoleWriter.Out
						.Yellow().Write($"  \U000023F3 Latest: {nugetInfo.LatestVersion}")
						.ResetColor().Write(", ")
						.Green().WriteLine($"In-Scope: {string.Join(", ", allInScope)}")
						.ResetColor();
				}
				else
				{
					ConsoleWriter.Out.WriteLineColored(ConsoleColor.Yellow, $"  \U000023F3 Latest: {nugetInfo.LatestVersion}");
				}
			}

			PrintVulnerabilityDetails(nugetInfo);

			if (showDetailedInfo)
			{
				PrintExtendedPackageMetadata(nugetInfo);
			}
		}

		/// <summary>
		/// Prints vulnerability details for a package.
		/// </summary>
		private static void PrintVulnerabilityDetails(dynamic nugetInfo)
		{
			if (!nugetInfo.IsVulnerable || nugetInfo.Vulnerabilities == null || nugetInfo.Vulnerabilities.Count == 0)
				return;

			ConsoleWriter.Out.Red();

			foreach (var v in nugetInfo.Vulnerabilities)
			{
				ConsoleWriter.Out.WriteLine($"  \U0001F6A8 Vulnerability: {v}");
			}

			ConsoleWriter.Out.ResetColor();
		}

		/// <summary>
		/// Prints extended metadata for a package (authors, publish date, project URL, tags).
		/// </summary>
		private static void PrintExtendedPackageMetadata(dynamic nugetInfo)
		{
			if (!string.IsNullOrEmpty(nugetInfo.Authors))
			{
				ConsoleWriter.Out.WriteLineColored(ConsoleColor.Gray, $"  \U0001F464 Authors: {nugetInfo.Authors}");
			}

			if (nugetInfo.Published.HasValue)
			{
				ConsoleWriter.Out.WriteLineColored(ConsoleColor.Gray, $"  \U0001F4C5 Published: {nugetInfo.Published.Value:yyyy-MM-dd}");
			}

			if (!string.IsNullOrEmpty(nugetInfo.ProjectUrl))
			{
				ConsoleWriter.Out.WriteLineColored(ConsoleColor.Gray, $"  \U0001F3E0 Project: {nugetInfo.ProjectUrl}");
			}

			if (nugetInfo.Tags.Count > 0)
			{
				ConsoleWriter.Out.WriteLineColored(ConsoleColor.Gray, $"  \U0001F3F7\uFE0F Tags: {string.Join(", ", nugetInfo.Tags.Take(5))}");
			}
		}

		/// <summary>
		/// Prints detailed information for packages from alternate sources.
		/// </summary>
		private static void PrintAlternateSourceDetails(dynamic nugetInfo)
		{
			if (!string.IsNullOrEmpty(nugetInfo.FeedName))
			{
				PrintAlternateFeedDetails(nugetInfo);
			}
			else if (nugetInfo.SourceProjects != null && nugetInfo.SourceProjects.Count > 0)
			{
				PrintSourceProjectDetails(nugetInfo);
			}
		}

		/// <summary>
		/// Prints details for packages found on alternate feeds.
		/// </summary>
		private static void PrintAlternateFeedDetails(dynamic nugetInfo)
		{
			ConsoleWriter.Out.Cyan()
			 .WriteLine($"  \U0001F517 Feed: {nugetInfo.FeedName}");

			if (!string.IsNullOrEmpty(nugetInfo.PackageUrl))
			{
				ConsoleWriter.Out.WriteLine($"  \U0001F517 {nugetInfo.PackageUrl}");
			}

			ConsoleWriter.Out.ResetColor();
		}

		/// <summary>
		/// Prints details for packages found through source project cross-referencing.
		/// </summary>
		private static void PrintSourceProjectDetails(dynamic nugetInfo)
		{
			var topMatch = nugetInfo.SourceProjects.First();
			var confidencePercentage = (int)(topMatch.MatchConfidence * 100);
			ConsoleWriter.Out.WriteLineColored(ConsoleColor.Green, $"  \U0001F50D Source: {topMatch.ProjectName} in {topMatch.RepositoryName} ({confidencePercentage}% match)");
		}

		/// <summary>
		/// Prints summary statistics for all packages.
		/// </summary>
		private static void PrintSummaryStatistics(IEnumerable<dynamic> packageSummary, bool showNuGetDetails, Dictionary<string, string?>? pinnedPackages, VersionWarningConfig? warningConfig = null)
		{
			var stats = CalculateSummaryStatistics(packageSummary, showNuGetDetails, pinnedPackages, warningConfig);

			PrintStatisticsHeader(stats);

			if (showNuGetDetails)
			{
				PrintNuGetStatistics(stats);
			}

			PrintVersionConflictStatistics(stats);
		}

		/// <summary>
		/// Calculates summary statistics for packages.
		/// </summary>
		private static dynamic CalculateSummaryStatistics(IEnumerable<dynamic> packageSummary, bool showNuGetDetails, Dictionary<string, string?>? pinnedPackages, VersionWarningConfig? warningConfig = null)
		{
			var totalPackages = packageSummary.Count();
			var packagesWithMultipleVersions = packageSummary.Count(p => p.Versions.Count + (p.UnspecifiedVersionCount > 0 ? 1 : 0) > 1);
			var allOutdatedNonPinned = showNuGetDetails ? packageSummary.Where(p => p.NuGetInfo?.IsOutdated == true && pinnedPackages?.ContainsKey(p.PackageName) != true).ToList() : [];
			var updateableCount = showNuGetDetails ? allOutdatedNonPinned.Count(p => IsUpdateableOnly(p.NuGetInfo, (string)p.PackageName, warningConfig)) : 0;

			return new
			{
				TotalPackages = totalPackages,
				PackagesWithMultipleVersions = packagesWithMultipleVersions,
				PackagesOnNuGet = showNuGetDetails ? packageSummary.Count(p => p.NuGetInfo?.ExistsOnNuGetOrg == true) : 0,
				PackagesOnOtherFeeds = showNuGetDetails ? packageSummary.Count(p => p.NuGetInfo?.ExistsOnNuGetOrg == false && !string.IsNullOrEmpty(p.NuGetInfo?.FeedName)) : 0,
				DeprecatedPackages = showNuGetDetails ? packageSummary.Count(p => p.NuGetInfo?.IsDeprecated == true) : 0,
				InternalPackages = showNuGetDetails ? packageSummary.Count(p => p.NuGetInfo?.ExistsOnNuGetOrg == false && string.IsNullOrEmpty(p.NuGetInfo?.FeedName) && p.NuGetInfo?.SourceProjects?.Count > 0) : 0,
				PinnedCount = pinnedPackages?.Count ?? 0,
				OutdatedPackages = showNuGetDetails ? allOutdatedNonPinned.Count - updateableCount : 0,
				UpdateablePackages = updateableCount,
				VulnerablePackages = showNuGetDetails ? packageSummary.Count(p => p.NuGetInfo?.IsVulnerable == true) : 0,
				UnknownPackages = 0 // Calculated after other counts
			};
		}

		/// <summary>
		/// Prints the statistics header with total package count.
		/// </summary>
		private static void PrintStatisticsHeader(dynamic stats)
		{
			ConsoleWriter.Out.WriteLine(new string('-', 40))
			 .WriteLine($"Total unique packages: {stats.TotalPackages}");
		}

		/// <summary>
		/// Prints NuGet-related statistics.
		/// </summary>
		private static void PrintNuGetStatistics(dynamic stats)
		{
			ConsoleWriter.Out.WriteLineColored(ConsoleColor.Green, $"Available on NuGet.org: {stats.PackagesOnNuGet}");

			if (stats.PackagesOnOtherFeeds > 0)
			{
				ConsoleWriter.Out.WriteLineColored(ConsoleColor.Cyan, $"Available on other feeds: {stats.PackagesOnOtherFeeds}");
			}

			if (stats.InternalPackages > 0)
			{
				ConsoleWriter.Out.WriteLineColored(ConsoleColor.Blue, $"Internal packages (with source): {stats.InternalPackages}");
			}

			var unknownPackages = stats.TotalPackages - stats.PackagesOnNuGet - stats.PackagesOnOtherFeeds - stats.InternalPackages;

			if (unknownPackages > 0)
			{
				ConsoleWriter.Out.WriteLineColored(ConsoleColor.Yellow, $"Unknown packages (no source found): {unknownPackages}");
			}

			if (stats.PinnedCount > 0)
			{
				ConsoleWriter.Out.WriteLineColored(ConsoleColor.DarkYellow, $"Pinned packages: {stats.PinnedCount}");
			}

			if (stats.DeprecatedPackages > 0)
			{
				ConsoleWriter.Out.WriteLineColored(ConsoleColor.Red, $"Deprecated packages: {stats.DeprecatedPackages}");
			}

			if (stats.OutdatedPackages > 0)
			{
				ConsoleWriter.Out.WriteLineColored(ConsoleColor.Yellow, $"Outdated packages: {stats.OutdatedPackages}");
			}

			if (stats.UpdateablePackages > 0)
			{
				ConsoleWriter.Out.WriteLineColored(ConsoleColor.Cyan, $"Updateable packages: {stats.UpdateablePackages}");
			}

			if (stats.VulnerablePackages > 0)
			{
				ConsoleWriter.Out.WriteLineColored(ConsoleColor.Red, $"Potentially vulnerable packages: {stats.VulnerablePackages}");
			}
		}

		/// <summary>
		/// Prints version conflict statistics.
		/// </summary>
		private static void PrintVersionConflictStatistics(dynamic stats)
		{
			if (stats.PackagesWithMultipleVersions > 0)
			{
				ConsoleWriter.Out.WriteLineColored(ConsoleColor.Yellow, $"Packages with version conflicts: {stats.PackagesWithMultipleVersions}");
			}
			else
			{
				ConsoleWriter.Out.WriteLineColored(ConsoleColor.Green, "No version conflicts detected");
			}
		}

		/// <summary>
		/// Prints a category section (e.g., deprecated) with package details and related metadata.
		/// </summary>
		/// <param name="title">Section title to display.</param>
		/// <param name="packages">Sequence of package projection objects containing summary metadata.</param>
		private static void PrintCategorySection(string title, IEnumerable<dynamic> packages, VersionWarningConfig? warningConfig = null)
		{
			var list = packages.ToList();
			if (!list.Any()) return;

			ConsoleWriter.Out.WriteLine()
			 .WriteLine(title)
			 .WriteLine(new string('-', title.Length));

			foreach (var pkg in list)
			{
				ConsoleWriter.Out.WriteLine($" - {pkg.PackageName} (refs: {pkg.TotalReferences})");
				var level = warningConfig?.GetLevelForPackage(pkg.PackageName) ?? VersionWarningLevel.None;
				var usedVersions = ((IEnumerable<dynamic>)pkg.Versions).Select(v => (string)v.Version).ToList();
				PrintPackageDetails(pkg.NuGetInfo, level, usedVersions);
			}
		}

		/// <summary>
		/// Prints detail lines (outdated, deprecated, vulnerable) for a single package's NuGet info.
		/// </summary>
		/// <param name="info">The NuGet metadata for a package, or null if unavailable.</param>
		private static void PrintPackageDetails(dynamic info, VersionWarningLevel level = VersionWarningLevel.None, List<string>? usedVersions = null)
		{
			if (info == null)
			{
				return;
			}

			if (info.IsOutdated && !string.IsNullOrEmpty(info.LatestVersion))
			{
				string? resolvedVersion = info.ResolvedVersion;
				List<string> allInScope = (level != VersionWarningLevel.None && usedVersions != null && usedVersions.Count > 0)
					? NuGetPackageResolver.FindAllInScopeVersions(usedVersions, info.AvailableVersions, level, (string?)info.LatestVersion)
					: [];

				if (allInScope.Count > 0)
				{
					ConsoleWriter.Out.WriteLine($"    Latest: {info.LatestVersion} In-Scope: {string.Join(", ", allInScope)} Used: {resolvedVersion ?? "unknown"}");
				}
				else
				{
					ConsoleWriter.Out.WriteLine($"    Latest: {info.LatestVersion} Used: {resolvedVersion ?? "unknown"}");
				}
			}

			if (info.IsDeprecated && !string.IsNullOrEmpty(info.DeprecationMessage))
			{
				ConsoleWriter.Out.WriteLine($"    Reason: {info.DeprecationMessage}");
			}

			if (info.IsVulnerable && info.Vulnerabilities != null && info.Vulnerabilities.Count > 0)
			{
				foreach (var v in info.Vulnerabilities)
				{
					ConsoleWriter.Out.WriteLine($"    Vulnerability: {v}");
				}
			}
		}

		/// <summary>
		/// Builds a human readable summary string describing the current exclusion configuration.
		/// </summary>
		/// <param name="exclusionList">The exclusion list to summarize.</param>
		/// <returns>Summary text of configured exclusions, or 'no exclusions'.</returns>
		private static string GetExclusionSummary(ExclusionList exclusionList)
		{
			var parts = new List<string>();

			if (exclusionList.ExcludedPrefixes.Any())
			{
				parts.Add($"prefixes: {string.Join(", ", exclusionList.ExcludedPrefixes)}");
			}

			if (exclusionList.ExcludedPackages.Any())
			{
				parts.Add($"packages: {string.Join(", ", exclusionList.ExcludedPackages)}");
			}

			if (exclusionList.ExcludedPatterns.Any())
			{
				parts.Add($"patterns: {exclusionList.ExcludedPatterns.Count} regex rule(s)");
			}

			return parts.Any() ? string.Join("; ", parts) : "no exclusions";
		}
	}
}