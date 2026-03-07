using System.Text.RegularExpressions;

using NuGroom.Configuration;
using NuGroom.Nuget;
using NuGroom.Vulnerability;

namespace NuGroom.Workflows
{
	/// <summary>
	/// Scans local files and directories for project files (.csproj, .vbproj, .fsproj),
	/// extracts package references, optionally resolves NuGet metadata, and prints results.
	/// No Azure DevOps connectivity is required.
	/// </summary>
	internal static class LocalScanWorkflow
	{
		/// <summary>
		/// Supported project file extensions (.csproj, .vbproj, .fsproj).
		/// </summary>
		private static readonly string[] SupportedProjectExtensions = [".csproj", ".vbproj", ".fsproj"];

		/// <summary>
		/// Executes the full local scan pipeline: discover project files on disk, extract
		/// package references, resolve NuGet metadata, and print summary output.
		/// </summary>
		/// <param name="parseResult">Validated parse result whose <see cref="ParseResult.LocalPaths"/> is not null or empty.</param>
		/// <returns>Scan result containing discovered package references.</returns>
		public static async Task<ScanResult> ExecuteAsync(ParseResult parseResult)
		{
			ArgumentNullException.ThrowIfNull(parseResult);

			var localPaths = parseResult.LocalPaths!;
			var exclusionList = parseResult.ExclusionList;
			var extractor = new PackageReferenceExtractor(exclusionList);
			var nugetResolver = parseResult.ResolveNuGet ? new NuGetPackageResolver(parseResult.Feeds, parseResult.FeedAuth) : null;

			ConsoleWriter.Out.WriteLine("Local scan mode");
			ConsoleWriter.Out.WriteLine($"Scanning {localPaths.Count} path(s)...").WriteLine();

			var excludeProjectRegexes = BuildProjectExclusionRegexes();

			var projectFiles = DiscoverProjectFiles(localPaths, excludeProjectRegexes, parseResult.IncludePackagesConfig);
			var allFiles = projectFiles.ProjectFiles;
			var managementFiles = projectFiles.ManagementFiles;

			ConsoleWriter.Out.WriteLine($"Found {allFiles.Count} project file(s).").WriteLine();

			var tempReferences = new List<PackageReferenceExtractor.PackageReference>();
			var projectFilePaths = allFiles.Select(f => f.FullName).ToList();

			var cpmLookup = BuildCpmLookup(managementFiles);

			foreach (var projectFile in allFiles)
			{
				try
				{
					ConsoleWriter.Out.WriteLine($"  Processing: {projectFile.FullName}");
					var content = await File.ReadAllTextAsync(projectFile.FullName);

					if (string.IsNullOrWhiteSpace(content))
					{
						continue;
					}

					var repoName = GetRootLabel(projectFile, localPaths);
					var refs = extractor.ExtractPackageReferences(
						content,
						repoName,
						projectFile.FullName,
						projectName: null);

					var cpmResult = FindNearestCpmResult(projectFile.FullName, cpmLookup);

					if (cpmResult != null && cpmResult.ManagePackageVersionsCentrally)
					{
						refs = CpmPackageExtractor.MergeCpmVersions(refs, cpmResult.PackageVersions, cpmResult.FilePath);
					}

					tempReferences.AddRange(refs);

					if (refs.Count > 0)
					{
						ConsoleWriter.Out.WriteLine($"    Found {refs.Count} package reference(s)");
					}
				}
				catch (Exception ex)
				{
					ConsoleWriter.Out.WriteLine($"    Warning: Failed to process {projectFile.FullName}: {ex.Message}");
				}
			}

			// Process packages.config files (opt-in via --include-packages-config)
			if (parseResult.IncludePackagesConfig)
			{
				ProcessPackagesConfigFiles(managementFiles, projectFilePaths, extractor, tempReferences);
			}

			var allPackageReferences = await EnrichWithNuGetInfoAsync(tempReferences, nugetResolver, parseResult);

			PackageReferenceExtractor.PrintPackageReferences(
				allPackageReferences,
				exclusionList,
				parseResult.ResolveNuGet,
				parseResult.ShowDetailedInfo,
				GetPinnedLookup(parseResult),
				parseResult.VersionWarningConfig);

			PackageReferenceExtractor.PrintPackageSummary(
				allPackageReferences,
				parseResult.ResolveNuGet,
				parseResult.ShowDetailedInfo,
				GetPinnedLookup(parseResult),
				parseResult.VersionWarningConfig);

			return new ScanResult(allPackageReferences, new Dictionary<string, RenovateOverrides>());
		}

		/// <summary>
		/// Builds a list of precompiled exclusion regexes for project file paths.
		/// Currently returns an empty list; project-file exclusion patterns are not supported in local mode.
		/// </summary>
		private static List<Regex> BuildProjectExclusionRegexes()
		{
			return new List<Regex>();
		}

		/// <summary>
		/// Discovers all project files and package management files reachable from the given paths.
		/// Directories are searched recursively; individual files are used directly when they match.
		/// </summary>
		private static LocalFiles DiscoverProjectFiles(
			List<string> paths,
			List<Regex> excludeProjectRegexes,
			bool includePackagesConfig)
		{
			var projectFiles = new List<FileInfo>();
			var managementFiles = new List<FileInfo>();
			var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (var rawPath in paths)
			{
				var expandedPath = Environment.ExpandEnvironmentVariables(rawPath);
				var fullPath = Path.GetFullPath(expandedPath);

				if (Directory.Exists(fullPath))
				{
					CollectFilesFromDirectory(fullPath, projectFiles, managementFiles, excludeProjectRegexes, includePackagesConfig, visited);
				}
				else if (File.Exists(fullPath))
				{
					AddSingleFile(new FileInfo(fullPath), projectFiles, managementFiles, excludeProjectRegexes, includePackagesConfig, visited);
				}
				else
				{
					ConsoleWriter.Out.Yellow().WriteLine($"Warning: Path not found: {fullPath}").ResetColor();
				}
			}

			return new LocalFiles(projectFiles, managementFiles);
		}

		/// <summary>
		/// Recursively collects project and management files from a directory.
		/// </summary>
		private static void CollectFilesFromDirectory(
			string directory,
			List<FileInfo> projectFiles,
			List<FileInfo> managementFiles,
			List<Regex> excludeProjectRegexes,
			bool includePackagesConfig,
			HashSet<string> visited)
		{
			try
			{
				foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
				{
					var info = new FileInfo(file);
					AddSingleFile(info, projectFiles, managementFiles, excludeProjectRegexes, includePackagesConfig, visited);
				}
			}
			catch (Exception ex)
			{
				ConsoleWriter.Out.Yellow().WriteLine($"Warning: Cannot enumerate directory {directory}: {ex.Message}").ResetColor();
			}
		}

		/// <summary>
		/// Classifies a single file as a project file, a management file, or neither, and adds it to the appropriate list.
		/// </summary>
		private static void AddSingleFile(
			FileInfo file,
			List<FileInfo> projectFiles,
			List<FileInfo> managementFiles,
			List<Regex> excludeProjectRegexes,
			bool includePackagesConfig,
			HashSet<string> visited)
		{
			if (!visited.Add(file.FullName))
			{
				return;
			}

			if (IsProjectFile(file.Name) && !IsExcludedProject(file.Name, excludeProjectRegexes))
			{
				projectFiles.Add(file);
			}
			else if (IsPackageManagementFile(file.Name, includePackagesConfig))
			{
				managementFiles.Add(file);
			}
		}

		/// <summary>
		/// Reads all <c>Directory.Packages.props</c> files and builds a lookup used to resolve CPM versions.
		/// </summary>
		private static List<CpmPackageExtractor.CpmParseResult> BuildCpmLookup(List<FileInfo> managementFiles)
		{
			var cpmFiles = managementFiles
				.Where(f => f.Name.Equals("Directory.Packages.props", StringComparison.OrdinalIgnoreCase))
				.ToList();

			if (cpmFiles.Count == 0)
			{
				return [];
			}

			var results = new List<CpmPackageExtractor.CpmParseResult>();

			foreach (var cpmFile in cpmFiles)
			{
				try
				{
					var content = File.ReadAllText(cpmFile.FullName);

					if (string.IsNullOrWhiteSpace(content))
					{
						continue;
					}

					var result = CpmPackageExtractor.Parse(content);
					results.Add(result with { FilePath = cpmFile.FullName });
				}
				catch (Exception ex)
				{
					ConsoleWriter.Out.Yellow().WriteLine($"Warning: Failed to read {cpmFile.FullName}: {ex.Message}").ResetColor();
				}
			}

			var activeCpmCount = results.Count(r => r.ManagePackageVersionsCentrally);

			if (activeCpmCount > 0)
			{
				var totalPackages = results.Where(r => r.ManagePackageVersionsCentrally).Sum(r => r.PackageVersions.Count);
				ConsoleWriter.Out.WriteLine($"CPM detected ({activeCpmCount} active file(s), {totalPackages} centrally managed package(s))");
			}

			return results;
		}

		/// <summary>
		/// Finds the nearest <c>Directory.Packages.props</c> for a project file by walking up the directory tree.
		/// </summary>
		private static CpmPackageExtractor.CpmParseResult? FindNearestCpmResult(
			string projectPath,
			List<CpmPackageExtractor.CpmParseResult> cpmResults)
		{
			if (cpmResults.Count == 0)
			{
				return null;
			}

			var dirLookup = new Dictionary<string, CpmPackageExtractor.CpmParseResult>(StringComparer.OrdinalIgnoreCase);

			foreach (var cpm in cpmResults)
			{
				if (cpm.FilePath == null)
				{
					continue;
				}

				var dir = Path.GetDirectoryName(cpm.FilePath);

				if (dir != null)
				{
					dirLookup[dir] = cpm;
				}
			}

			var current = Path.GetDirectoryName(projectPath);

			while (!string.IsNullOrEmpty(current))
			{
				if (dirLookup.TryGetValue(current, out var match))
				{
					return match;
				}

				var parent = Path.GetDirectoryName(current);

				if (parent == current)
				{
					break;
				}

				current = parent;
			}

			return null;
		}

		/// <summary>
		/// Processes <c>packages.config</c> files from the management files list,
		/// associating each with its co-located project file.
		/// </summary>
		private static void ProcessPackagesConfigFiles(
			List<FileInfo> managementFiles,
			List<string> projectFilePaths,
			PackageReferenceExtractor extractor,
			List<PackageReferenceExtractor.PackageReference> tempReferences)
		{
			var pkgConfigFiles = managementFiles
				.Where(f => f.Name.Equals("packages.config", StringComparison.OrdinalIgnoreCase))
				.ToList();

			foreach (var pkgConfigFile in pkgConfigFiles)
			{
				try
				{
					var associatedProject = PackagesConfigExtractor.FindColocatedProjectFile(
						pkgConfigFile.FullName,
						projectFilePaths);

					if (associatedProject == null)
					{
						Logger.Debug($"packages.config at {pkgConfigFile.FullName} has no co-located project file, skipping");
						continue;
					}

					var content = File.ReadAllText(pkgConfigFile.FullName);
					var repoName = Path.GetFileName(Path.GetDirectoryName(pkgConfigFile.FullName)) ?? "local";
					var refs = PackagesConfigExtractor.Extract(
						content,
						repoName,
						associatedProject,
						projectName: "local",
						extractor.GetExclusionList());

					var existingPackages = tempReferences
						.Where(r => string.Equals(r.ProjectPath, associatedProject, StringComparison.OrdinalIgnoreCase))
						.Select(r => r.PackageName)
						.ToHashSet(StringComparer.OrdinalIgnoreCase);

					var newRefs = refs
						.Where(r => !existingPackages.Contains(r.PackageName))
						.Select(r => r with { PackagesConfigPath = pkgConfigFile.FullName })
						.ToList();

					if (newRefs.Count > 0)
					{
						tempReferences.AddRange(newRefs);
						ConsoleWriter.Out.WriteLine($"  packages.config: {newRefs.Count} additional package(s) for {associatedProject}");
					}
				}
				catch (Exception ex)
				{
					ConsoleWriter.Out.WriteLine($"  Warning: Failed to process {pkgConfigFile.FullName}: {ex.Message}");
				}
			}
		}

		/// <summary>
		/// Enriches package references with NuGet metadata when resolution is enabled.
		/// </summary>
		private static async Task<List<PackageReferenceExtractor.PackageReference>> EnrichWithNuGetInfoAsync(
			List<PackageReferenceExtractor.PackageReference> tempReferences,
			NuGetPackageResolver? nugetResolver,
			ParseResult parseResult)
		{
			if (!parseResult.ResolveNuGet || nugetResolver == null || !tempReferences.Any())
			{
				return new List<PackageReferenceExtractor.PackageReference>(tempReferences);
			}

			ConsoleWriter.Out.WriteLine("Resolving NuGet package information...");
			var uniquePackages = tempReferences
				.Select(pr => pr.PackageName)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			var nugetInfos = await nugetResolver.ResolvePackagesAsync(uniquePackages, tempReferences);

			var aggregator = BuildVulnerabilityAggregator(parseResult.VulnerabilityConfig);

			if (aggregator != null)
			{
				using (aggregator)
				{
					await aggregator.EnrichPackageInfosAsync(nugetInfos, tempReferences);
				}
			}

			var result = new List<PackageReferenceExtractor.PackageReference>();

			foreach (var packageRef in tempReferences)
			{
				if (nugetInfos.TryGetValue(packageRef.PackageName, out var nugetInfo))
				{
					result.Add(packageRef with { NuGetInfo = nugetInfo });
				}
				else
				{
					result.Add(packageRef);
				}
			}

			return result;
		}

		/// <summary>
		/// Builds a <see cref="VulnerabilityAggregator"/> from configuration. Returns <c>null</c> when no sources are enabled.
		/// </summary>
		private static VulnerabilityAggregator? BuildVulnerabilityAggregator(VulnerabilityConfig? config)
		{
			config ??= new VulnerabilityConfig();

			var sources = new List<IVulnerabilitySource>();

			if (config.OsvEnabled)
			{
				HttpClient? httpClient = null;

				if (string.IsNullOrWhiteSpace(config.OsvBaseUrl))
				{
					httpClient = new HttpClient();
				}
				else if (Uri.TryCreate(config.OsvBaseUrl, UriKind.Absolute, out var baseUri))
				{
					httpClient = new HttpClient { BaseAddress = baseUri };
				}
				else
				{
					Console.Error.WriteLine($"[Warning] OSV is enabled but OsvBaseUrl '{config.OsvBaseUrl}' is not a valid absolute URI. OSV will be disabled for this run.");
				}

				if (httpClient != null)
				{
					sources.Add(new OsvVulnerabilitySource(httpClient));
				}
			}

			if (sources.Count == 0)
			{
				return null;
			}

			VulnerabilityCache? cache = null;

			if (config.CacheEnabled)
			{
				var ttl = TimeSpan.FromHours(Math.Max(1, config.CacheTtlHours));
				cache = new VulnerabilityCache(config.CachePath, ttl);
			}

			return new VulnerabilityAggregator(sources, cache);
		}

		/// <summary>
		/// Returns a human-readable label for the repository/root used in output.
		/// Uses the name of the common ancestor path among the scanned paths when possible.
		/// </summary>
		private static string GetRootLabel(FileInfo projectFile, List<string> scanPaths)
		{
			foreach (var path in scanPaths)
			{
				var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));

				if (projectFile.FullName.StartsWith(fullPath, StringComparison.OrdinalIgnoreCase))
				{
					return Path.GetFileName(fullPath) ?? fullPath;
				}
			}

			return Path.GetFileName(Path.GetDirectoryName(projectFile.FullName)) ?? "local";
		}

		/// <summary>
		/// Returns the pinned package lookup from the parse result's update configuration.
		/// </summary>
		private static Dictionary<string, string?> GetPinnedLookup(ParseResult parseResult)
		{
			var pinned = parseResult.UpdateConfig?.PinnedPackages;

			if (pinned == null || pinned.Count == 0)
			{
				return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
			}

			return pinned.ToDictionary(
				p => p.PackageName,
				p => p.Version,
				StringComparer.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Determines whether a file name has a supported project file extension.
		/// </summary>
		private static bool IsProjectFile(string fileName)
		{
			return SupportedProjectExtensions.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Determines whether a file should be excluded based on configured exclusion patterns.
		/// </summary>
		private static bool IsExcludedProject(string fileName, List<Regex> excludeRegexes)
		{
			foreach (var rx in excludeRegexes)
			{
				if (rx.IsMatch(fileName))
				{
					Logger.Debug($"Excluding {fileName} (matched pattern: {rx})");

					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Determines whether a file name is a known package management file.
		/// </summary>
		private static bool IsPackageManagementFile(string fileName, bool includePackagesConfig)
		{
			if (fileName.Equals("Directory.Packages.props", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (includePackagesConfig && fileName.Equals("packages.config", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			return false;
		}

		/// <summary>
		/// Holds the result of local file discovery, classified by type.
		/// </summary>
		/// <param name="ProjectFiles">Project files (.csproj, .vbproj, .fsproj).</param>
		/// <param name="ManagementFiles">Package management files (Directory.Packages.props, packages.config).</param>
		private sealed record LocalFiles(List<FileInfo> ProjectFiles, List<FileInfo> ManagementFiles);
	}
}
