using NuGroom.ADO;
using NuGroom.Configuration;
using NuGroom.Nuget;

namespace NuGroom
{
	/// <summary>
	/// Represents the result of parsing command line arguments.
	/// A null <see cref="Config"/> indicates a parse error or help request.
	/// </summary>
	public sealed record ParseResult(
		AzureDevOpsConfig? Config,
		PackageReferenceExtractor.ExclusionList ExclusionList,
		bool ResolveNuGet,
		bool ShowDetailedInfo,
		bool IgnoreRenovate,
		List<Feed> Feeds,
		string? ExportPackagesPath,
		string? ExportWarningsPath,
		string? ExportRecommendationsPath,
		string? ExportSbomPath,
		ExportFormat ExportFormat,
		List<FeedAuth> FeedAuth,
		VersionWarningConfig? VersionWarningConfig,
		UpdateConfig? UpdateConfig,
		List<SyncConfig> SyncConfigs);

	/// <summary>
	/// Parses and validates command line arguments and optional configuration files
	/// to produce a consolidated <see cref="ParseResult"/>.
	/// </summary>
	internal static class CommandLineParser
	{
		/// <summary>
		/// Holds intermediate state during command line argument parsing
		/// </summary>
		private sealed class CliParsingState
		{
			public string? Organization { get; set; }
			public string? Token { get; set; }
			public string? Project { get; set; }
			public int MaxRepos { get; set; } = 100;
			public bool? IncludeArchived { get; set; }
			public bool? ResolveNuGet { get; set; }
			public bool? ShowDetailedInfo { get; set; }
			public List<Feed> Feeds { get; } = new();
			public string? ExportPackagesPath { get; set; }
			public string? ExportWarningsPath { get; set; }
			public string? ExportRecommendationsPath { get; set; }
			public string? ExportSbomPath { get; set; }
			public ExportFormat? ExportFormat { get; set; }
			public string? ConfigPath { get; set; }
			public List<FeedAuth> FeedAuth { get; } = new();
			public VersionWarningConfig? VersionWarningConfig { get; set; }
			public List<string> ExcludePrefixes { get; } = new();
			public List<string> ExcludePackages { get; } = new();
			public List<string> ExcludePatterns { get; } = new();
			public List<string> ExcludeProjectPatterns { get; } = new();
			public List<string> ExcludeRepositories { get; } = new();
			public List<string> IncludeRepositories { get; } = new();
			public bool? NoDefaultExclusions { get; set; }
			public bool? CaseSensitive { get; set; }
			public bool? CaseSensitiveProjectFilters { get; set; }
			public int CliFeedCounter { get; set; }
			public UpdateConfig? UpdateConfig { get; set; }
			public bool? IgnoreRenovate { get; set; }
			public bool RequestedDryRun { get; set; }
			public bool DryRunExplicit { get; set; }
			public bool UpdateScopeExplicit { get; set; }
			public bool TagCommitsExplicit { get; set; }
			public bool FeatureBranchExplicit { get; set; }
			public bool SourceBranchExplicit { get; set; }
			public bool TargetBranchExplicit { get; set; }
			public bool SourcePackagesOnlyExplicit { get; set; }
			public bool NoIncrementalPrsExplicit { get; set; }
			public List<SyncConfig> SyncConfigs { get; } = new();
		}

		/// <summary>
		/// Parses command line arguments and optional configuration file producing a consolidated parse result.
		/// Validates required parameters and applies exclusion settings.
		/// </summary>
		/// <param name="args">Command line arguments.</param>
		/// <returns>A <see cref="ParseResult"/> containing parsed configuration and options, or Config = null on error.</returns>
		public static ParseResult Parse(string[] args)
		{
			if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
			{
				ShowHelp();

				return CreateEmptyParseResult();
			}

			var state = new CliParsingState();

			ExtractConfigPath(args, state);

			var fileConfig = LoadConfigFileIfSpecified(state.ConfigPath);

			ParseCommandLineArguments(args, state);

			// --dry-run always takes priority over --update-references regardless of argument order
			if (state.RequestedDryRun && state.UpdateConfig != null)
			{
				state.UpdateConfig.DryRun = true;
			}

			if (fileConfig != null)
			{
				ApplyConfigFileDefaults(fileConfig, state);
			}

			var exclusionList = BuildExclusionList(state);

			var validationResult = ValidateRequiredParameters(state, exclusionList);

			if (validationResult != null)
			{
				return validationResult;
			}

			return BuildSuccessfulParseResult(state, exclusionList);
		}

		/// <summary>
		/// Creates an empty parse result with default values (used for help display and errors)
		/// </summary>
		private static ParseResult CreateEmptyParseResult()
		{
			return new ParseResult(
				Config: null,
				ExclusionList: PackageReferenceExtractor.ExclusionList.CreateDefault(),
				ResolveNuGet: true,
				ShowDetailedInfo: false,
				IgnoreRenovate: false,
				Feeds: new List<Feed>(),
				ExportPackagesPath: null,
				ExportWarningsPath: null,
				ExportRecommendationsPath: null,
				ExportSbomPath: null,
				ExportFormat: Configuration.ExportFormat.Json,
				FeedAuth: new List<FeedAuth>(),
				VersionWarningConfig: null,
				UpdateConfig: null,
				SyncConfigs: new List<SyncConfig>());
		}

		/// <summary>
		/// Extracts the config file path from command line arguments if present
		/// </summary>
		private static void ExtractConfigPath(string[] args, CliParsingState state)
		{
			for (int i = 0; i < args.Length; i++)
			{
				if (args[i].Equals("--config", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
				{
					state.ConfigPath = args[i + 1];
					break;
				}
			}
		}

		/// <summary>
		/// Loads configuration from file if path is specified
		/// </summary>
		private static ToolConfig? LoadConfigFileIfSpecified(string? configPath)
		{
			if (configPath == null)
			{
				return null;
			}

			try
			{
				var fileConfig = ConfigLoader.Load(configPath);
				Console.WriteLine($"Loaded config file: {configPath}");

				return fileConfig;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Warning: Failed to load config file '{configPath}': {ex.Message}");

				return null;
			}
		}

		/// <summary>
		/// Parses all command line arguments and populates the state object
		/// </summary>
		private static void ParseCommandLineArguments(string[] args, CliParsingState state)
		{
			for (int i = 0; i < args.Length; i++)
			{
				switch (args[i].ToLower())
				{
					case "--organization":
					case "-o":
						if (i + 1 < args.Length)
						{
							state.Organization = args[++i];
						}

						break;
					case "--token":
					case "-t":
						if (i + 1 < args.Length)
						{
							state.Token = args[++i];
						}

						break;
					case "--project":
					case "-p":
						if (i + 1 < args.Length)
						{
							state.Project = args[++i];
						}

						break;
					case "--max-repos":
					case "-m":
						ParseMaxReposArgument(args, ref i, state);
						break;
					case "--include-archived":
					case "-a":
						state.IncludeArchived = true;
						break;
					case "--exclude-archived":
						state.IncludeArchived = false;
						break;
					case "--exclude-prefix":
						if (i + 1 < args.Length)
						{
							state.ExcludePrefixes.Add(args[++i]);
						}

						break;
					case "--exclude-package":
						if (i + 1 < args.Length)
						{
							state.ExcludePackages.Add(args[++i]);
						}

						break;
					case "--exclude-pattern":
						if (i + 1 < args.Length)
						{
							state.ExcludePatterns.Add(args[++i]);
						}

						break;
					case "--exclude-project":
						if (i + 1 < args.Length)
						{
							state.ExcludeProjectPatterns.Add(args[++i]);
						}

						break;
					case "--exclude-repo":
						if (i + 1 < args.Length)
						{
							state.ExcludeRepositories.Add(args[++i]);
						}

						break;
					case "--include-repo":
						if (i + 1 < args.Length)
						{
							state.IncludeRepositories.Add(args[++i]);
						}

						break;
					case "--no-default-exclusions":
						state.NoDefaultExclusions = true;
						break;
					case "--case-sensitive":
						state.CaseSensitive = true;
						break;
					case "--case-sensitive-project":
						state.CaseSensitiveProjectFilters = true;
						break;
					case "--skip-nuget":
						state.ResolveNuGet = false;
						break;
					case "--resolve-nuget":
						state.ResolveNuGet = true;
						break;
					case "--detailed":
					case "-d":
						state.ShowDetailedInfo = true;
						break;
					case "--no-detailed":
						state.ShowDetailedInfo = false;
						break;
					case "--debug":
						Logger.EnableDebugLogging = true;
						Logger.Debug("Debug logging enabled via command line");
						break;
					case "--feed":
					case "--nuget-feed":
						ParseFeedArgument(args, ref i, state);
						break;
					case "--export-packages":
						if (i + 1 < args.Length)
						{
							state.ExportPackagesPath = args[++i];
						}
						else
						{
							Console.WriteLine("Warning: --export-packages requires a path.");
						}

						break;
					case "--export-warnings":
						if (i + 1 < args.Length)
						{
							state.ExportWarningsPath = args[++i];
						}
						else
						{
							Console.WriteLine("Warning: --export-warnings requires a path.");
						}

						break;
					case "--export-recommendations":
						if (i + 1 < args.Length)
						{
							state.ExportRecommendationsPath = args[++i];
						}
						else
						{
							Console.WriteLine("Warning: --export-recommendations requires a path.");
						}

						break;
					case "--export-format":
						if (i + 1 < args.Length)
						{
							var formatStr = args[++i];

							if (Enum.TryParse<ExportFormat>(formatStr, ignoreCase: true, out var format))
							{
								state.ExportFormat = format;
							}
							else
							{
								Console.WriteLine($"Warning: Invalid export format '{formatStr}'. Use Json or Csv.");
							}
						}

						break;
					case "--export-sbom":
						if (i + 1 < args.Length)
						{
							state.ExportSbomPath = args[++i];
						}
						else
						{
							Console.WriteLine("Warning: --export-sbom requires a path.");
						}

						break;
					case "--feed-auth":
						ParseFeedAuthArgument(args, ref i, state);
						break;
					case "--config":
						i++;
						break;
					case "--update-references":
						state.UpdateConfig ??= new UpdateConfig();
						state.UpdateConfig.DryRun = false;
						state.DryRunExplicit = true;
						break;
					case "--dry-run":
						state.UpdateConfig ??= new UpdateConfig();
						state.UpdateConfig.DryRun = true;
						state.RequestedDryRun = true;
						state.DryRunExplicit = true;
						break;
					case "--update-scope":
						ParseUpdateScopeArgument(args, ref i, state);
						break;
					case "--source-branch":
						if (i + 1 < args.Length)
						{
							state.UpdateConfig ??= new UpdateConfig();
							state.UpdateConfig.SourceBranchPattern = args[++i];
							state.SourceBranchExplicit = true;
						}

						break;
					case "--target-branch":
						if (i + 1 < args.Length)
						{
							state.UpdateConfig ??= new UpdateConfig();
							state.UpdateConfig.TargetBranchPattern = args[++i];
							state.TargetBranchExplicit = true;
						}

						break;
					case "--feature-branch":
						if (i + 1 < args.Length)
						{
							state.UpdateConfig ??= new UpdateConfig();
							state.UpdateConfig.FeatureBranchName = args[++i];
							state.FeatureBranchExplicit = true;
						}

						break;
					case "--source-packages-only":
						state.UpdateConfig ??= new UpdateConfig();
						state.UpdateConfig.SourcePackagesOnly = true;
						state.SourcePackagesOnlyExplicit = true;
						break;
					case "--required-reviewer":
						if (i + 1 < args.Length)
						{
							state.UpdateConfig ??= new UpdateConfig();
							state.UpdateConfig.RequiredReviewers ??= new List<string>();
							state.UpdateConfig.RequiredReviewers.Add(args[++i]);
						}

						break;
					case "--optional-reviewer":
						if (i + 1 < args.Length)
						{
							state.UpdateConfig ??= new UpdateConfig();
							state.UpdateConfig.OptionalReviewers ??= new List<string>();
							state.UpdateConfig.OptionalReviewers.Add(args[++i]);
						}

						break;
					case "--tag-commits":
						state.UpdateConfig ??= new UpdateConfig();
						state.UpdateConfig.TagCommits = true;
						state.TagCommitsExplicit = true;
						break;
					case "--no-incremental-prs":
						state.UpdateConfig ??= new UpdateConfig();
						state.UpdateConfig.NoIncrementalPrs = true;
						state.NoIncrementalPrsExplicit = true;
						break;
					case "--ignore-renovate":
						state.IgnoreRenovate = true;
						break;
					case "--sync":
					case "-sync":
						if (i + 1 < args.Length)
						{
							var syncPackage = args[++i];
							string? syncVersion = null;

							if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
							{
								syncVersion = args[++i];
							}

							state.SyncConfigs.Add(new SyncConfig(syncPackage, syncVersion));
						}
						else
						{
							Console.WriteLine("Error: --sync requires a package name.");
						}

						break;
					default:
						Console.WriteLine($"Error: Unknown argument '{args[i]}'");
						ShowHelp();
						break;
				}
			}
		}

		/// <summary>
		/// Parses the --max-repos argument
		/// </summary>
		private static void ParseMaxReposArgument(string[] args, ref int i, CliParsingState state)
		{
			if (i + 1 < args.Length && int.TryParse(args[++i], out var parsed))
			{
				state.MaxRepos = parsed;
			}
			else
			{
				Console.WriteLine("Error: Invalid value for --max-repos.");
			}
		}

		/// <summary>
		/// Parses the --feed argument
		/// </summary>
		private static void ParseFeedArgument(string[] args, ref int i, CliParsingState state)
		{
			if (i + 1 < args.Length)
			{
				var feedUrl = args[++i];
				var feedName = $"CLIFeed{++state.CliFeedCounter}";
				state.Feeds.Add(new Feed(feedName, feedUrl));
			}
		}

		/// <summary>
		/// Parses the --feed-auth argument
		/// </summary>
		private static void ParseFeedAuthArgument(string[] args, ref int i, CliParsingState state)
		{
			if (i + 1 < args.Length)
			{
				var authStr = args[++i];
				var parts = authStr.Split('|');

				if (parts.Length == 3)
				{
					state.FeedAuth.Add(new FeedAuth(parts[0], string.IsNullOrEmpty(parts[1]) ? null : parts[1], parts[2]));
				}
				else
				{
					Console.WriteLine("Warning: --feed-auth format is feedName|username|pat");
				}
			}
		}

		/// <summary>
		/// Parses the --update-scope argument
		/// </summary>
		private static void ParseUpdateScopeArgument(string[] args, ref int i, CliParsingState state)
		{
			if (i + 1 < args.Length)
			{
				var scopeStr = args[++i];

				if (Enum.TryParse<UpdateScope>(scopeStr, ignoreCase: true, out var scope))
					{
						state.UpdateConfig ??= new UpdateConfig();
						state.UpdateConfig.Scope = scope;
						state.UpdateScopeExplicit = true;
					}
				else
				{
					Console.WriteLine($"Warning: Invalid update scope '{scopeStr}'. Use Patch, Minor, or Major.");
				}
			}
		}

		/// <summary>
		/// Applies configuration file defaults to CLI parsing state where CLI values were not provided
		/// </summary>
		private static void ApplyConfigFileDefaults(ToolConfig fileConfig, CliParsingState state)
		{
			// Apply config file defaults only where CLI did not set a value (null = not set)
			if (state.Organization == null && fileConfig.Organization != null)
			{
				state.Organization = fileConfig.Organization;
			}

			if (state.Token == null && fileConfig.Token != null)
			{
				state.Token = fileConfig.Token;
			}

			if (state.Project == null && fileConfig.Project != null)
			{
				state.Project = fileConfig.Project;
			}

			if (state.MaxRepos == 100 && fileConfig.MaxRepos.HasValue && fileConfig.MaxRepos.Value > 0)
			{
				state.MaxRepos = fileConfig.MaxRepos.Value;
			}

			if (!state.IncludeArchived.HasValue && fileConfig.IncludeArchived.HasValue)
			{
				state.IncludeArchived = fileConfig.IncludeArchived.Value;
			}

			if (!state.ResolveNuGet.HasValue && fileConfig.ResolveNuGet.HasValue)
			{
				state.ResolveNuGet = fileConfig.ResolveNuGet.Value;
			}

			if (!state.ShowDetailedInfo.HasValue && fileConfig.Detailed.HasValue)
			{
				state.ShowDetailedInfo = fileConfig.Detailed.Value;
			}

			if (!state.NoDefaultExclusions.HasValue && fileConfig.NoDefaultExclusions.HasValue)
			{
				state.NoDefaultExclusions = fileConfig.NoDefaultExclusions.Value;
			}

			if (!state.CaseSensitive.HasValue && fileConfig.CaseSensitive.HasValue)
			{
				state.CaseSensitive = fileConfig.CaseSensitive.Value;
			}

			if (!state.CaseSensitiveProjectFilters.HasValue && fileConfig.CaseSensitiveProjectFilters.HasValue)
			{
				state.CaseSensitiveProjectFilters = fileConfig.CaseSensitiveProjectFilters.Value;
			}

			if (!state.IgnoreRenovate.HasValue && fileConfig.IgnoreRenovate.HasValue)
			{
				state.IgnoreRenovate = fileConfig.IgnoreRenovate.Value;
			}

			if (state.ExportPackagesPath == null && fileConfig.ExportPackages != null)
			{
				state.ExportPackagesPath = fileConfig.ExportPackages;
			}

			if (state.ExportWarningsPath == null && fileConfig.ExportWarnings != null)
			{
				state.ExportWarningsPath = fileConfig.ExportWarnings;
			}

			if (state.ExportRecommendationsPath == null && fileConfig.ExportRecommendations != null)
			{
				state.ExportRecommendationsPath = fileConfig.ExportRecommendations;
			}

			if (state.ExportSbomPath == null && fileConfig.ExportSbom != null)
			{
				state.ExportSbomPath = fileConfig.ExportSbom;
			}

			if (!state.ExportFormat.HasValue && fileConfig.ExportFormat.HasValue)
			{
				state.ExportFormat = fileConfig.ExportFormat.Value;
			}

			if (state.VersionWarningConfig == null && fileConfig.VersionWarnings != null)
			{
				state.VersionWarningConfig = fileConfig.VersionWarnings;
			}

			if (state.UpdateConfig == null && fileConfig.Update != null)
			{
				state.UpdateConfig = fileConfig.Update;
			}
			else if (state.UpdateConfig != null && fileConfig.Update != null)
			{
				// CLI args created an UpdateConfig (e.g. --source-branch); merge file-only fields
				if (!state.DryRunExplicit)
				{
					state.UpdateConfig.DryRun = fileConfig.Update.DryRun;
				}

				if (!state.UpdateScopeExplicit)
				{
					state.UpdateConfig.Scope = fileConfig.Update.Scope;
				}

				state.UpdateConfig.PinnedPackages ??= fileConfig.Update.PinnedPackages;
				state.UpdateConfig.RequiredReviewers ??= fileConfig.Update.RequiredReviewers;
				state.UpdateConfig.OptionalReviewers ??= fileConfig.Update.OptionalReviewers;

				if (!state.TagCommitsExplicit)
				{
					state.UpdateConfig.TagCommits = fileConfig.Update.TagCommits;
				}

				if (!state.FeatureBranchExplicit)
				{
					state.UpdateConfig.FeatureBranchName = fileConfig.Update.FeatureBranchName;
				}

				if (!state.SourceBranchExplicit)
				{
					state.UpdateConfig.SourceBranchPattern = fileConfig.Update.SourceBranchPattern;
				}

				if (!state.TargetBranchExplicit)
				{
					state.UpdateConfig.TargetBranchPattern = fileConfig.Update.TargetBranchPattern;
				}

				if (!state.SourcePackagesOnlyExplicit)
				{
					state.UpdateConfig.SourcePackagesOnly = fileConfig.Update.SourcePackagesOnly;
				}

				if (!state.NoIncrementalPrsExplicit)
				{
					state.UpdateConfig.NoIncrementalPrs = fileConfig.Update.NoIncrementalPrs;
				}
			}

			// Merge list-based settings (additive from config file)
			if (fileConfig.Feeds?.Any() == true)
			{
				foreach (var feed in fileConfig.Feeds)
				{
					if (!state.Feeds.Any(f => f.Name.Equals(feed.Name, StringComparison.OrdinalIgnoreCase)))
					{
						state.Feeds.Add(feed);
					}
				}
			}

			if (fileConfig.FeedAuth?.Any() == true)
			{
				foreach (var auth in fileConfig.FeedAuth)
				{
					if (!state.FeedAuth.Any(a => a.FeedName.Equals(auth.FeedName, StringComparison.OrdinalIgnoreCase)))
					{
						state.FeedAuth.Add(auth);
					}
				}
			}

			if (fileConfig.ExcludePrefixes?.Any() == true)
			{
				state.ExcludePrefixes.AddRange(fileConfig.ExcludePrefixes.Where(p => !state.ExcludePrefixes.Contains(p)));
			}

			if (fileConfig.ExcludePackages?.Any() == true)
			{
				state.ExcludePackages.AddRange(fileConfig.ExcludePackages.Where(p => !state.ExcludePackages.Contains(p)));
			}

			if (fileConfig.ExcludePatterns?.Any() == true)
			{
				state.ExcludePatterns.AddRange(fileConfig.ExcludePatterns.Where(p => !state.ExcludePatterns.Contains(p)));
			}

			if (fileConfig.ExcludeProjectPatterns?.Any() == true)
			{
				state.ExcludeProjectPatterns.AddRange(fileConfig.ExcludeProjectPatterns.Where(p => !state.ExcludeProjectPatterns.Contains(p)));
			}

			if (fileConfig.ExcludeRepositories?.Any() == true)
			{
				state.ExcludeRepositories.AddRange(fileConfig.ExcludeRepositories.Where(p => !state.ExcludeRepositories.Contains(p)));
			}

			if (fileConfig.IncludeRepositories?.Any() == true)
			{
				foreach (var repo in fileConfig.IncludeRepositories)
				{
					if (!state.IncludeRepositories.Contains(repo, StringComparer.OrdinalIgnoreCase))
					{
						state.IncludeRepositories.Add(repo);
					}
				}
			}
		}

		/// <summary>
		/// Builds the package exclusion list from CLI parsing state
		/// </summary>
		private static PackageReferenceExtractor.ExclusionList BuildExclusionList(CliParsingState state)
		{
			var exclusionList = (state.NoDefaultExclusions == true)
				? PackageReferenceExtractor.ExclusionList.CreateEmpty()
				: PackageReferenceExtractor.ExclusionList.CreateDefault();

			exclusionList.CaseSensitive = state.CaseSensitive == true;

			foreach (var prefix in state.ExcludePrefixes)
			{
				exclusionList.AddPrefixExclusion(prefix);
			}

			foreach (var pkg in state.ExcludePackages)
			{
				exclusionList.AddPackageExclusion(pkg);
			}

			foreach (var pat in state.ExcludePatterns)
			{
				exclusionList.AddPatternExclusion(pat);
			}

			return exclusionList;
		}

		/// <summary>
		/// Validates required parameters and returns error parse result if validation fails
		/// </summary>
		private static ParseResult? ValidateRequiredParameters(CliParsingState state, PackageReferenceExtractor.ExclusionList exclusionList)
		{
			if (string.IsNullOrWhiteSpace(state.Organization))
			{
				Console.WriteLine("Error: --organization is required (missing in CLI/config)");
				ShowHelp();

				return CreateErrorParseResult(state, exclusionList);
			}

			if (string.IsNullOrWhiteSpace(state.Token))
			{
				Console.WriteLine("Error: --token is required (missing in CLI/config)");
				ShowHelp();

				return CreateErrorParseResult(state, exclusionList);
			}

			return null;
		}

		/// <summary>
		/// Creates an error parse result with Config set to null
		/// </summary>
		private static ParseResult CreateErrorParseResult(CliParsingState state, PackageReferenceExtractor.ExclusionList exclusionList)
		{
			return new ParseResult(
				Config: null,
				ExclusionList: exclusionList,
				ResolveNuGet: state.ResolveNuGet ?? true,
				ShowDetailedInfo: state.ShowDetailedInfo ?? false,
				IgnoreRenovate: state.IgnoreRenovate ?? false,
				Feeds: state.Feeds,
				ExportPackagesPath: state.ExportPackagesPath,
				ExportWarningsPath: state.ExportWarningsPath,
				ExportRecommendationsPath: state.ExportRecommendationsPath,
				ExportSbomPath: state.ExportSbomPath,
				ExportFormat: state.ExportFormat ?? Configuration.ExportFormat.Json,
				FeedAuth: state.FeedAuth,
				VersionWarningConfig: state.VersionWarningConfig,
				UpdateConfig: state.UpdateConfig,
				SyncConfigs: state.SyncConfigs);
		}

		/// <summary>
		/// Builds a successful parse result
		/// </summary>
		private static ParseResult BuildSuccessfulParseResult(CliParsingState state, PackageReferenceExtractor.ExclusionList exclusionList)
		{
			var azConfig = new AzureDevOpsConfig
			{
				OrganizationUrl             = state.Organization!,
				PersonalAccessToken         = state.Token!,
				ProjectName                 = state.Project,
				MaxRepositories             = state.MaxRepos,
				IncludeArchivedRepositories = state.IncludeArchived ?? false,
				ExcludeProjectPatterns      = state.ExcludeProjectPatterns,
				CaseSensitiveProjectFilters = state.CaseSensitiveProjectFilters ?? false,
				ExcludeRepositories         = state.ExcludeRepositories,
				IncludeRepositories         = state.IncludeRepositories
			};

			return new ParseResult(
				Config: azConfig,
				ExclusionList: exclusionList,
				ResolveNuGet: state.ResolveNuGet ?? true,
				ShowDetailedInfo: state.ShowDetailedInfo ?? false,
				IgnoreRenovate: state.IgnoreRenovate ?? false,
				Feeds: state.Feeds,
				ExportPackagesPath: state.ExportPackagesPath,
				ExportWarningsPath: state.ExportWarningsPath,
				ExportRecommendationsPath: state.ExportRecommendationsPath,
				ExportSbomPath: state.ExportSbomPath,
				ExportFormat: state.ExportFormat ?? Configuration.ExportFormat.Json,
				FeedAuth: state.FeedAuth,
				VersionWarningConfig: state.VersionWarningConfig,
				UpdateConfig: state.UpdateConfig,
				SyncConfigs: state.SyncConfigs);
		}

		/// <summary>
		/// Writes help and usage information to the console
		/// </summary>
		private static void ShowHelp()
		{
			Console.WriteLine("Groom Package References in Azure DevOps");
			Console.WriteLine("=====================================");
			Console.WriteLine("Supports configuration via --config path/to/config.json with named feeds and PAT authentication.");
			Console.WriteLine();
			Console.WriteLine("Usage:");
			Console.WriteLine("  NuGroom --config settings.json");
			Console.WriteLine("  NuGroom -o https://dev.azure.com/org -t token --feed https://feed/index.json --feed-auth \"FeedName||pat\"");
			Console.WriteLine();
			Console.WriteLine("Options:");
			Console.WriteLine("  --config <path>              Load configuration from JSON file");
			Console.WriteLine("  -o, --organization <url>     Azure DevOps organization URL");
			Console.WriteLine("  -t, --token <pat>            Personal Access Token");
			Console.WriteLine("  -p, --project <name>         Project name (optional, scans all projects if not specified)");
			Console.WriteLine("  -m, --max-repos <number>     Maximum number of repositories to scan (default: 100)");
			Console.WriteLine("  -a, --include-archived       Include archived repositories");
			Console.WriteLine("  --resolve-nuget              Resolve NuGet package information (default: true)");
			Console.WriteLine("  --skip-nuget                 Skip NuGet package resolution");
			Console.WriteLine("  -d, --detailed               Show detailed package information");
			Console.WriteLine("  --debug                      Enable debug logging (automatically enabled when debugger attached)");
			Console.WriteLine("  --feed <url>                 Add NuGet feed URL");
			Console.WriteLine("  --feed-auth <auth>           Feed authentication: feedName|username|pat");
			Console.WriteLine("  --export-packages <path>     Export package results to file (format via --export-format)");
			Console.WriteLine("  --export-warnings <path>     Export version warnings to a separate file");
			Console.WriteLine("  --export-recommendations <path> Export update recommendations to a separate file");
			Console.WriteLine("  --export-format <format>     Export format: Json or Csv (default: Json)");
			Console.WriteLine("  --export-sbom <path>         Export SPDX 3.0.0 SBOM (Software Bill of Materials) as JSON-LD");
			Console.WriteLine();
			Console.WriteLine("Exclusion Options:");
			Console.WriteLine("  --exclude-prefix <prefix>    Exclude packages starting with prefix (e.g., Microsoft.)");
			Console.WriteLine("  --exclude-package <name>     Exclude specific package by exact name");
			Console.WriteLine("  --exclude-pattern <regex>    Exclude packages matching regex pattern");
			Console.WriteLine("  --exclude-project <pattern>  Exclude project files matching regex pattern (.csproj, .vbproj, .fsproj)");
			Console.WriteLine("  --exclude-repo <pattern>     Exclude repositories matching regex pattern (e.g., \"Legacy-.*\")");
			Console.WriteLine("  --include-repo <pattern>     Include only repositories matching regex pattern (repeatable, processed in order)");
			Console.WriteLine("  --no-default-exclusions      Don't exclude Microsoft.* and System.* by default");
			Console.WriteLine("  --case-sensitive             Use case-sensitive package name matching");
			Console.WriteLine("  --case-sensitive-project     Use case-sensitive project file matching");
			Console.WriteLine();
			Console.WriteLine("Update Options:");
			Console.WriteLine("  --update-references          Enable auto-update mode (creates branches and PRs)");
			Console.WriteLine("  --dry-run                    Show planned updates without creating branches/PRs");
			Console.WriteLine("  --update-scope <scope>       Version update scope: Patch, Minor, or Major (default: Patch)");
			Console.WriteLine("  --source-branch <pattern>    Source branch pattern to branch from (default: same as target-branch)");
			Console.WriteLine("  --target-branch <pattern>    Target branch pattern for PRs (default: develop/*)");
			Console.WriteLine("  --feature-branch <name>      Feature branch name prefix (default: feature/update-nuget-references)");
			Console.WriteLine("  --source-packages-only       Only update packages that have source code in scanned repositories");
			Console.WriteLine("  --required-reviewer <email>  Add a required PR reviewer (repeatable, must approve)");
			Console.WriteLine("  --optional-reviewer <email>  Add an optional PR reviewer (repeatable, notified only)");
			Console.WriteLine("  --tag-commits                Create a lightweight git tag on the feature branch commit");
			Console.WriteLine("  --no-incremental-prs         Skip repos that already have open NuGroom PRs (exit with warning)");
			Console.WriteLine("  --ignore-renovate            Skip reading renovate.json from repositories");
			Console.WriteLine();
			Console.WriteLine("Sync Options:");
			Console.WriteLine("  --sync <package> [version]   Sync a specific package to a version across all repos (creates PRs)");
			Console.WriteLine("                               If version is omitted, the latest available version is used");
			Console.WriteLine("                               Repeatable: specify multiple --sync flags to sync several packages");
			Console.WriteLine();
			Console.WriteLine("For detailed help, see README.md");
		}
	}
}
