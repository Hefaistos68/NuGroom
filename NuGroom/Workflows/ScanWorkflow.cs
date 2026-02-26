using Microsoft.TeamFoundation.SourceControl.WebApi;

using NuGroom.ADO;
using NuGroom.Configuration;
using NuGroom.Nuget;

namespace NuGroom.Workflows
{
	/// <summary>
	/// Options that control a repository scan, consolidated from parsed CLI arguments.
	/// Replaces the long parameter list previously passed to the scan method.
	/// </summary>
	internal sealed record ScanOptions(
		AzureDevOpsConfig Config,
		PackageReferenceExtractor.ExclusionList ExclusionList,
		bool ResolveNuGet,
		bool ShowDetailedInfo,
		List<Feed> Feeds,
		List<FeedAuth> FeedAuth,
		VersionWarningConfig? VersionWarningConfig,
		bool IgnoreRenovate,
		List<PinnedPackage>? PinnedPackages = null,
		bool IncludePackagesConfig = false)
	{
		/// <summary>
		/// Creates a <see cref="ScanOptions"/> from a validated <see cref="ParseResult"/>.
		/// </summary>
		/// <param name="result">A parse result whose <see cref="ParseResult.Config"/> is not null.</param>
		/// <returns>A new <see cref="ScanOptions"/> instance.</returns>
		public static ScanOptions FromParseResult(ParseResult result)
		{
			ArgumentNullException.ThrowIfNull(result.Config);

			return new ScanOptions(
				result.Config,
				result.ExclusionList,
				result.ResolveNuGet,
				result.ShowDetailedInfo,
				result.Feeds,
				result.FeedAuth,
				result.VersionWarningConfig,
				result.IgnoreRenovate,
				result.UpdateConfig?.PinnedPackages,
				result.IncludePackagesConfig);
		}

		/// <summary>
		/// Returns a lookup of pinned package names to their pinned versions.
		/// A null value means the package is pinned at whatever version is currently used.
		/// </summary>
		public Dictionary<string, string?> GetPinnedPackageLookup()
		{
			if (PinnedPackages == null || PinnedPackages.Count == 0)
			{
				Logger.Debug("No pinned packages configured");
				return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
			}

			Logger.Debug($"Building pinned package lookup with {PinnedPackages.Count} package(s)");

			return PinnedPackages.ToDictionary(
				p => p.PackageName,
				p => p.Version,
				StringComparer.OrdinalIgnoreCase);
		}
	}

	/// <summary>
	/// Result of scanning repositories, including package references and per-repository Renovate overrides
	/// </summary>
	internal sealed record ScanResult(
		List<PackageReferenceExtractor.PackageReference> References,
		Dictionary<string, RenovateOverrides> RenovateOverrides);

	/// <summary>
	/// Scans Azure DevOps repositories for project files (.csproj, .vbproj, .fsproj), extracts package references,
	/// optionally resolves NuGet metadata, and prints results and summary statistics.
	/// </summary>
	internal static class ScanWorkflow
	{
		/// <summary>
		/// Executes the full scan pipeline: enumerate repositories, extract package references,
		/// resolve NuGet metadata, and print summary output.
		/// </summary>
		/// <param name="options">Consolidated scan options.</param>
		/// <returns>Scan result containing discovered package references and per-repository Renovate overrides.</returns>
		public static async Task<ScanResult> ExecuteAsync(ScanOptions options)
		{
			PrintScanHeader(options);

			using var client = new AzureDevOpsClient(options.Config);
			var extractor = new PackageReferenceExtractor(options.ExclusionList);
			var nugetResolver = options.ResolveNuGet ? new NuGetPackageResolver(options.Feeds, options.FeedAuth) : null;
			var context = new ScanContext();

			try
			{
				ConsoleWriter.Out.WriteLine("Retrieving repositories...");
				var repositories = await client.GetRepositoriesAsync();
				ConsoleWriter.Out.WriteLine($"Found {repositories.Count} repositories to scan.").WriteLine();

				int repoIndex = 0;

				foreach (var repository in repositories)
				{
					repoIndex++;
					ConsoleWriter.Out.WriteLine($"[{repoIndex}/{repositories.Count}] Scanning repository: {repository.Name}");
					await ScanRepositoryAsync(client, extractor, repository, context, options);
					ConsoleWriter.Out.WriteLine();
				}

				var allPackageReferences = await EnrichWithNuGetInfoAsync(context.TempPackageReferences, nugetResolver, options);

				var pinnedLookup = options.GetPinnedPackageLookup();
				Logger.Debug($"Pinned lookup contains {pinnedLookup.Count} package(s): {string.Join(", ", pinnedLookup.Keys)}");

				PrintScanSummary(repoIndex, context.TotalProjectFiles, allPackageReferences.Count, options, nugetResolver);
					PackageReferenceExtractor.PrintPackageReferences(allPackageReferences, options.ExclusionList, options.ResolveNuGet, options.ShowDetailedInfo, pinnedLookup, options.VersionWarningConfig);
					PackageReferenceExtractor.PrintPackageSummary(allPackageReferences, options.ResolveNuGet, options.ShowDetailedInfo, pinnedLookup, options.VersionWarningConfig);

				return new ScanResult(allPackageReferences, context.RenovateOverrides);
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException($"Failed to scan repositories: {ex.Message}", ex);
			}
		}

		/// <summary>
		/// Scans a single repository: reads Renovate config, enumerates project files,
		/// discovers CPM and packages.config files, and extracts package references.
		/// </summary>
		private static async Task ScanRepositoryAsync(
			AzureDevOpsClient client, PackageReferenceExtractor extractor,
			GitRepository repository, ScanContext context, ScanOptions options)
		{
			// Check for Renovate configuration
			var repoRenovate = await TryReadRenovateConfigAsync(client, repository, options, context.RenovateOverrides);

			try
			{
				var projectFiles = await client.GetProjectFilesAsync(repository);
				context.TotalProjectFiles += projectFiles.Count;

				if (projectFiles.Count == 0)
				{
					ConsoleWriter.Out.WriteLine($"  No project files found in {repository.Name}");
					return;
				}

				ConsoleWriter.Out.WriteLine($"  Found {projectFiles.Count} project file(s)");

				// Discover CPM and packages.config files
				var managementFiles = await client.GetPackageManagementFilesAsync(repository, options.IncludePackagesConfig);
				var cpmResult = await TryReadCpmAsync(client, repository, managementFiles);
				var projectFilePaths = projectFiles.Select(f => f.Path).ToList();

				foreach (var projectFile in projectFiles)
				{
					try
					{
						var refs = await ProcessProjectFileAsync(client, extractor, repository, projectFile, repoRenovate);

						// Merge CPM versions when Directory.Packages.props is present
						if (cpmResult != null && cpmResult.ManagePackageVersionsCentrally)
						{
							refs = CpmPackageExtractor.MergeCpmVersions(refs, cpmResult.PackageVersions);
						}

						context.TempPackageReferences.AddRange(refs);

						if (refs.Count > 0)
						{
							ConsoleWriter.Out.WriteLine($"      Found {refs.Count} package reference(s)");
						}
					}
					catch (Exception ex)
					{
						ConsoleWriter.Out.WriteLine($"      Warning: Failed to process {projectFile.Path}: {ex.Message}");
					}
				}

				// Process packages.config files (opt-in via --include-packages-config)
				if (options.IncludePackagesConfig)
				{
					await ProcessPackagesConfigFilesAsync(client, repository, managementFiles, projectFilePaths, extractor, repoRenovate, context);
				}
			}
			catch (Exception ex)
			{
				ConsoleWriter.Out.WriteLine($"  Warning: Failed to scan repository {repository.Name}: {ex.Message}");
			}
		}

		/// <summary>
		/// Reads and parses <c>Directory.Packages.props</c> from the management files if present.
		/// </summary>
		private static async Task<CpmPackageExtractor.CpmParseResult?> TryReadCpmAsync(
			AzureDevOpsClient client, GitRepository repository,
			List<Microsoft.TeamFoundation.SourceControl.WebApi.GitItem> managementFiles)
		{
			var cpmFile = managementFiles.FirstOrDefault(f =>
				Path.GetFileName(f.Path).Equals("Directory.Packages.props", StringComparison.OrdinalIgnoreCase));

			if (cpmFile == null)
			{
				return null;
			}

			var content = await client.GetFileContentAsync(repository, cpmFile);

			if (string.IsNullOrWhiteSpace(content))
			{
				return null;
			}

			var result = CpmPackageExtractor.Parse(content);

			if (result.ManagePackageVersionsCentrally)
			{
				ConsoleWriter.Out.WriteLine($"  CPM detected ({result.PackageVersions.Count} centrally managed package(s))");
			}

			return result;
		}

		/// <summary>
		/// Processes all <c>packages.config</c> files found in the repository,
		/// associating each with its co-located project file.
		/// </summary>
		private static async Task ProcessPackagesConfigFilesAsync(
			AzureDevOpsClient client, GitRepository repository,
			List<Microsoft.TeamFoundation.SourceControl.WebApi.GitItem> managementFiles,
			List<string> projectFilePaths, PackageReferenceExtractor extractor,
			RenovateOverrides? repoRenovate, ScanContext context)
		{
			var pkgConfigFiles = managementFiles
				.Where(f => Path.GetFileName(f.Path).Equals("packages.config", StringComparison.OrdinalIgnoreCase))
				.ToList();

			foreach (var pkgConfigFile in pkgConfigFiles)
			{
				try
				{
					var associatedProject = PackagesConfigExtractor.FindColocatedProjectFile(pkgConfigFile.Path, projectFilePaths);

					if (associatedProject == null)
					{
						Logger.Debug($"packages.config at {pkgConfigFile.Path} has no co-located project file, skipping");
						continue;
					}

					var content = await client.GetFileContentAsync(repository, pkgConfigFile);
					var refs = PackagesConfigExtractor.Extract(
						content,
						repository.Name,
						associatedProject,
						repository.ProjectReference?.Name ?? "Unknown",
						extractor.GetExclusionList());

					if (repoRenovate != null)
					{
						refs = ApplyRenovateFiltering(refs, repoRenovate, pkgConfigFile.Path);
					}

					// Avoid duplicates: skip packages already extracted from the project file
					var existingPackages = context.TempPackageReferences
						.Where(r => r.ProjectPath == associatedProject)
						.Select(r => r.PackageName)
						.ToHashSet(StringComparer.OrdinalIgnoreCase);

					var newRefs = refs.Where(r => !existingPackages.Contains(r.PackageName)).ToList();

					if (newRefs.Count > 0)
					{
						context.TempPackageReferences.AddRange(newRefs);
						ConsoleWriter.Out.WriteLine($"    packages.config: {newRefs.Count} additional package(s) for {associatedProject}");
					}
				}
				catch (Exception ex)
				{
					ConsoleWriter.Out.WriteLine($"      Warning: Failed to process {pkgConfigFile.Path}: {ex.Message}");
				}
			}
		}

		/// <summary>
		/// Attempts to read a Renovate configuration from the repository.
		/// </summary>
		private static async Task<RenovateOverrides?> TryReadRenovateConfigAsync(
			AzureDevOpsClient client, GitRepository repository,
			ScanOptions options, Dictionary<string, RenovateOverrides> renovateOverrides)
		{
			if (options.IgnoreRenovate)
			{
				return null;
			}

			try
			{
				var repoRenovate = await RenovateConfigReader.TryReadFromRepositoryAsync(client, repository);

				if (repoRenovate != null)
				{
					renovateOverrides[repository.Name] = repoRenovate;
					ConsoleWriter.Out.WriteLine($"  Renovate config found ({repoRenovate.IgnoredPackages.Count} ignored, {repoRenovate.DisabledPackages.Count} disabled)");
				}

				return repoRenovate;
			}
			catch (Exception ex)
			{
				Logger.Debug($"Failed to read Renovate config for {repository.Name}: {ex.Message}");

				return null;
			}
		}

		/// <summary>
		/// Processes a single project file: reads content, extracts package references, and applies Renovate filtering.
		/// </summary>
		private static async Task<List<PackageReferenceExtractor.PackageReference>> ProcessProjectFileAsync(
			AzureDevOpsClient client, PackageReferenceExtractor extractor,
			GitRepository repository, GitItem projectFile, RenovateOverrides? repoRenovate)
		{
			ConsoleWriter.Out.WriteLine($"    Processing: {projectFile.Path}");
			var content = await client.GetFileContentAsync(repository, projectFile);

			if (string.IsNullOrWhiteSpace(content))
			{
				return [];
			}

			var refs = extractor.ExtractPackageReferences(content, repository.Name, projectFile.Path, repository.ProjectReference?.Name);

			// Apply Renovate ignoreDeps filtering
			if (repoRenovate != null)
			{
				refs = ApplyRenovateFiltering(refs, repoRenovate, projectFile.Path);
			}

			return refs;
		}

		/// <summary>
		/// Filters package references by removing packages excluded via Renovate ignoreDeps or packageRules.
		/// </summary>
		private static List<PackageReferenceExtractor.PackageReference> ApplyRenovateFiltering(
			List<PackageReferenceExtractor.PackageReference> refs, RenovateOverrides repoRenovate, string filePath)
		{
			var beforeCount = refs.Count;
			var filtered = refs.Where(r => !RenovateConfigReader.IsPackageExcluded(r.PackageName, repoRenovate)).ToList();
			var removedCount = beforeCount - filtered.Count;

			if (removedCount > 0)
			{
				Logger.Debug($"Renovate: excluded {removedCount} package(s) via ignoreDeps/packageRules in {filePath}");
			}

			return filtered;
		}

		/// <summary>
		/// Enriches package references with NuGet metadata when resolution is enabled.
		/// </summary>
		private static async Task<List<PackageReferenceExtractor.PackageReference>> EnrichWithNuGetInfoAsync(
			List<PackageReferenceExtractor.PackageReference> tempReferences,
			NuGetPackageResolver? nugetResolver, ScanOptions options)
		{
			if (!options.ResolveNuGet || nugetResolver == null || !tempReferences.Any())
			{
				return new List<PackageReferenceExtractor.PackageReference>(tempReferences);
			}

			ConsoleWriter.Out.WriteLine("Resolving NuGet package information and cross-referencing source projects...");
			var uniquePackages = tempReferences.Select(pr => pr.PackageName).Distinct().ToList();
			var nugetInfos = await nugetResolver.ResolvePackagesAsync(uniquePackages, tempReferences);
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
		/// Mutable state accumulated during the repository scanning phase.
		/// </summary>
		private sealed class ScanContext
		{
			public int TotalProjectFiles { get; set; }
			public List<PackageReferenceExtractor.PackageReference> TempPackageReferences { get; } = [];
			public Dictionary<string, RenovateOverrides> RenovateOverrides { get; } = new(StringComparer.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Prints the scan configuration header to the console
		/// </summary>
		private static void PrintScanHeader(ScanOptions options)
		{
			var config = options.Config;

			var c = ConsoleWriter.Out
				.WriteLine("Finding Package References in Azure DevOps")
				.WriteLine("=========================================")
				.WriteLine($"Organization: {config.OrganizationUrl}");

			if (!string.IsNullOrEmpty(config.ProjectName))
			{
				c.WriteLine($"Project: {config.ProjectName}");
			}
			else
			{
				c.WriteLine("Project: All projects");
			}

			c.WriteLine($"Max repositories: {config.MaxRepositories}")
				.WriteLine($"Include archived: {config.IncludeArchivedRepositories}")
				.WriteLine($"Resolve NuGet info: {options.ResolveNuGet}");

			if (options.ResolveNuGet)
			{
				PrintNuGetFeedConfig(options);
			}

			// Display version warning configuration
			if (options.VersionWarningConfig != null && options.VersionWarningConfig.DefaultLevel != VersionWarningLevel.None)
			{
				c.WriteLine($"Version warning level: {options.VersionWarningConfig.DefaultLevel}");

				if (options.VersionWarningConfig.PackageRules?.Any() == true)
				{
					c.WriteLine($"Package-specific rules: {options.VersionWarningConfig.PackageRules.Count} configured");
				}
			}

			if (options.PinnedPackages?.Any() == true)
			{
				c.WriteLine($"Pinned packages: {options.PinnedPackages.Count} configured");

				foreach (var p in options.PinnedPackages)
				{
					var ver = p.Version ?? "(current)";
					c.WriteLine($"  - {p.PackageName} @ {ver}");
				}
			}

			PrintPackageExclusionConfig(options.ExclusionList);

			c.WriteLine()
				.WriteLine("Project File Exclusion Configuration:")
				.WriteLine($"  Case sensitive: {config.CaseSensitiveProjectFilters}");

			if (config.ExcludeProjectPatterns.Any())
			{
				c.WriteLine($"  Excluded patterns: {string.Join(", ", config.ExcludeProjectPatterns)}");
			}
			else
			{
				c.WriteLine("  No project file exclusions configured");
			}

			PrintRepositoryConfig(config);
		}

		/// <summary>
		/// Prints NuGet feed and authentication configuration to the console.
		/// </summary>
		private static void PrintNuGetFeedConfig(ScanOptions options)
		{
			if (options.Feeds?.Any() == true)
			{
				ConsoleWriter.Out.WriteLine("NuGet feeds:");

				foreach (var f in options.Feeds)
				{
					ConsoleWriter.Out.WriteLine($"  - {f.Name}: {f.Url}");
				}
			}
			else
			{
				ConsoleWriter.Out.WriteLine("NuGet feeds:")
					.WriteLine("  - NuGet.org: https://api.nuget.org/v3/index.json");
			}

			if (options.FeedAuth?.Any() == true)
			{
				ConsoleWriter.Out.WriteLine($"Feed authentication configured for {options.FeedAuth.Count} feed(s)");
			}
		}

		/// <summary>
		/// Prints package exclusion configuration to the console.
		/// </summary>
		private static void PrintPackageExclusionConfig(PackageReferenceExtractor.ExclusionList exclusionList)
		{
			ConsoleWriter.Out.WriteLine()
				.WriteLine("Package Exclusion Configuration:")
				.WriteLine($"  Case sensitive: {exclusionList.CaseSensitive}");

			if (exclusionList.ExcludedPrefixes.Any())
			{
				ConsoleWriter.Out.WriteLine($"  Excluded prefixes: {string.Join(", ", exclusionList.ExcludedPrefixes)}");
			}

			if (exclusionList.ExcludedPackages.Any())
			{
				ConsoleWriter.Out.WriteLine($"  Excluded packages: {string.Join(", ", exclusionList.ExcludedPackages)}");
			}

			if (exclusionList.ExcludedPatterns.Any())
			{
				ConsoleWriter.Out.WriteLine($"  Excluded patterns: {string.Join(", ", exclusionList.ExcludedPatterns)}");
			}

			if (!exclusionList.ExcludedPrefixes.Any() && !exclusionList.ExcludedPackages.Any() && !exclusionList.ExcludedPatterns.Any())
			{
				ConsoleWriter.Out.WriteLine("  No package exclusions configured");
			}
		}

		/// <summary>
		/// Prints repository include/exclude configuration to the console.
		/// </summary>
		private static void PrintRepositoryConfig(AzureDevOpsConfig config)
		{
			ConsoleWriter.Out.WriteLine()
				.WriteLine("Repository Configuration:");

			if (config.IncludeRepositories.Any())
			{
				ConsoleWriter.Out.WriteLine($"  Include list ({config.IncludeRepositories.Count}): {string.Join(", ", config.IncludeRepositories)}");
			}

			if (config.ExcludeRepositories.Any())
			{
				ConsoleWriter.Out.WriteLine($"  Excluded patterns: {string.Join(", ", config.ExcludeRepositories)}");
			}

			if (!config.IncludeRepositories.Any() && !config.ExcludeRepositories.Any())
			{
				ConsoleWriter.Out.WriteLine("  No repository filters configured");
			}

			ConsoleWriter.Out.WriteLine();
		}

		/// <summary>
		/// Prints the scan summary statistics to the console
		/// </summary>
		private static void PrintScanSummary(
			int processedRepos,
			int totalProjectFiles,
			int totalReferences,
			ScanOptions options,
			NuGetPackageResolver? nugetResolver)
		{
			var c = ConsoleWriter.Out
				.WriteLine()
				.WriteLine(new string('=', 80))
				.WriteLine("SCAN SUMMARY")
				.WriteLine(new string('=', 80))
				.WriteLine($"Repositories processed: {processedRepos}")
				.WriteLine($"Total project files found: {totalProjectFiles}")
				.WriteLine($"Total package references found: {totalReferences}");

			if (options.ResolveNuGet && nugetResolver != null)
			{
				var (_, foundOnNuGet, notFound) = nugetResolver.GetCacheStats();
				c.WriteLine($"Packages resolved from feeds: {foundOnNuGet} found, {notFound} not found");
			}

			c.WriteLine()
				.WriteLine(new string('=', 80))
				.WriteLine("PACKAGE REFERENCES")
				.WriteLine(new string('=', 80));
		}
	}
}
