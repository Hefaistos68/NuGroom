using System.Text.Json;
using System.Text.RegularExpressions;

namespace NuGroom.Configuration
{
	/// <summary>
	/// Represents a named NuGet feed
	/// </summary>
	/// <param name="Name">Logical name used to reference the feed.</param>
	/// <param name="Url">The feed URL (e.g. NuGet v3 index URL).</param>
	public record Feed(string Name, string Url);

	/// <summary>
	/// Represents feed authentication credentials for private NuGet feeds using feed name
	/// </summary>
	/// <param name="FeedName">The name of the feed these credentials apply to.</param>
	/// <param name="Username">Optional username for authenticated feeds (may be null for PAT-only auth).</param>
	/// <param name="Pat">Personal Access Token or password used for authentication (may be null).</param>
	public record FeedAuth(string FeedName, string? Username, string? Pat);

	/// <summary>
	/// Defines the output format for exported reports.
	/// </summary>
	[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
	public enum ExportFormat
	{
		/// <summary>Export as JSON (default)</summary>
		Json,

		/// <summary>Export as CSV</summary>
		Csv
	}

	/// <summary>
	/// Represents the mutable context used when applying configuration file values to runtime arguments.
	/// </summary>
	public sealed class ApplyConfigContext
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ApplyConfigContext"/> class with empty collections.
		/// </summary>
		public ApplyConfigContext()
		{
			Feeds                     = new List<Feed>();
			ExcludePrefixes           = new List<string>();
			ExcludePackages           = new List<string>();
			ExcludePatterns           = new List<string>();
			FeedAuth                  = new List<FeedAuth>();
			ExcludeCsprojPatterns     = new List<string>();
			ExcludeRepositories       = new List<string>();
			IncludeRepositories       = new List<string>();
		}

		/// <summary>Target Azure DevOps organization.</summary>
		public string? Organization { get; set; }

		/// <summary>Authentication token.</summary>
		public string? Token { get; set; }

		/// <summary>Optional project filter.</summary>
		public string? Project { get; set; }

		/// <summary>Maximum repositories to process when set.</summary>
		public int? MaxRepos { get; set; }

		/// <summary>Whether to include archived repositories.</summary>
		public bool? IncludeArchived { get; set; }

		/// <summary>Whether to resolve NuGet metadata.</summary>
		public bool? ResolveNuGet { get; set; }

		/// <summary>Whether to show detailed output.</summary>
		public bool? ShowDetailedInfo { get; set; }

		/// <summary>Whether to disable default exclusions.</summary>
		public bool? NoDefaultExclusions { get; set; }

		/// <summary>Whether to use case-sensitive matching.</summary>
		public bool? CaseSensitive { get; set; }

		/// <summary>Destination path for package export.</summary>
		public string? ExportPackagesPath { get; set; }

		/// <summary>Destination path for warnings export.</summary>
		public string? ExportWarningsPath { get; set; }

		/// <summary>Destination path for recommendations export.</summary>
		public string? ExportRecommendationsPath { get; set; }

		/// <summary>Destination path for SBOM export.</summary>
		public string? ExportSbomPath { get; set; }

		/// <summary>Export format override.</summary>
		public ExportFormat? ExportFormat { get; set; }

		/// <summary>Named feeds to merge.</summary>
		public List<Feed> Feeds { get; }

		/// <summary>Feed authentication entries to merge.</summary>
		public List<FeedAuth> FeedAuth { get; }

		/// <summary>Repository name prefixes to exclude.</summary>
		public List<string> ExcludePrefixes { get; }

		/// <summary>Package IDs to exclude.</summary>
		public List<string> ExcludePackages { get; }

		/// <summary>Path exclusion patterns.</summary>
		public List<string> ExcludePatterns { get; }

		/// <summary>.csproj-specific exclusion patterns.</summary>
		public List<string> ExcludeCsprojPatterns { get; }

		/// <summary>Repository exclusion regex patterns.</summary>
		public List<string> ExcludeRepositories { get; }

		/// <summary>Repository inclusion regex patterns.</summary>
		public List<string> IncludeRepositories { get; }

		/// <summary>Case sensitivity for .csproj filters.</summary>
		public bool? CaseSensitiveCsprojFilters { get; set; }

		/// <summary>Version warning configuration.</summary>
		public VersionWarningConfig? VersionWarningConfig { get; set; }

		/// <summary>Update workflow configuration.</summary>
		public UpdateConfig? UpdateConfig { get; set; }

		/// <summary>Whether to skip renovate processing.</summary>
		public bool? IgnoreRenovate { get; set; }

		/// <summary>Whether to include packages.config references.</summary>
		public bool? IncludePackagesConfig { get; set; }
	}

	/// <summary>
	/// Represents configuration options that mirror command line arguments.
	/// Fields left null/empty will be ignored allowing overrides from CLI.
	/// </summary>
	public class ToolConfig
	{
		/// <summary>
		/// Azure DevOps organization name (e.g. 'myorg'). Used when enumerating repositories.
		/// </summary>
		public string? Organization { get; set; }

		/// <summary>
		/// Authentication token (PAT) used to access Azure DevOps APIs if not supplied via CLI.
		/// </summary>
		public string? Token { get; set; }

		/// <summary>
		/// Optional Azure DevOps project filter. If null all projects in the organization may be considered.
		/// </summary>
		public string? Project { get; set; }

		/// <summary>
		/// Maximum number of repositories to scan. Allows limiting breadth for large orgs.
		/// </summary>
		public int? MaxRepos { get; set; }

		/// <summary>
		/// Indicates whether archived repositories should be included in the scan.
		/// </summary>
		public bool? IncludeArchived { get; set; }

		/// <summary>
		/// If true, attempt to resolve package versions against NuGet feeds to enrich output.
		/// </summary>
		public bool? ResolveNuGet { get; set; }

		/// <summary>
		/// If true, produce detailed output (e.g. per project/package breakdown).
		/// </summary>
		public bool? Detailed { get; set; }

		/// <summary>
		/// Disables built-in default exclusion filters when true.
		/// </summary>
		public bool? NoDefaultExclusions { get; set; }

		/// <summary>
		/// Enables case-sensitive matching for package filters when true.
		/// </summary>
		public bool? CaseSensitive { get; set; }

		/// <summary>
		/// List of repository name prefixes to exclude from scanning.
		/// </summary>
		public List<string>? ExcludePrefixes { get; set; }

		/// <summary>
		/// Explicit package IDs to exclude from results.
		/// </summary>
		public List<string>? ExcludePackages { get; set; }

		/// <summary>
		/// Glob or pattern list applied to paths / projects to exclude them.
		/// </summary>
		public List<string>? ExcludePatterns { get; set; }

		/// <summary>
		/// Glob or pattern list applied specifically to .csproj file names for exclusion.
		/// </summary>
		public List<string>? ExcludeProjectPatterns { get; set; }

		/// <summary>
		/// Regex patterns to exclude repositories by name.
		/// </summary>
		public List<string>? ExcludeRepositories { get; set; }

		/// <summary>
		/// Regex patterns to include repositories by name. When specified, only matching repositories are processed in the order listed.
		/// </summary>
		public List<string>? IncludeRepositories { get; set; }

		/// <summary>
		/// Enables case sensitivity for .csproj pattern filters when true.
		/// </summary>
		public bool? CaseSensitiveProjectFilters { get; set; }

		/// <summary>
		/// Additional named NuGet feeds to consider when resolving packages.
		/// </summary>
		public List<Feed>? Feeds { get; set; }

		/// <summary>
		/// Path to export package reference results. Format is controlled by <see cref="ExportFormat"/>.
		/// </summary>
		public string? ExportPackages { get; set; }

		/// <summary>
		/// Path to export version warnings. If null, warnings are only included in the main report.
		/// </summary>
		public string? ExportWarnings { get; set; }

		/// <summary>
		/// Path to export package update recommendations. If null, recommendations are only included in the main report.
		/// </summary>
		public string? ExportRecommendations { get; set; }

		/// <summary>
		/// Path to export an SPDX 3.0.0 SBOM (Software Bill of Materials) in JSON-LD format.
		/// If null, no SBOM is generated.
		/// </summary>
		public string? ExportSbom { get; set; }

		/// <summary>
		/// Output format for exported reports. Defaults to <see cref="Configuration.ExportFormat.Json"/>.
		/// </summary>
		public ExportFormat? ExportFormat { get; set; }

		/// <summary>
		/// Authentication entries for private feeds referenced by <see cref="Feeds"/>.
		/// </summary>
		public List<FeedAuth>? FeedAuth { get; set; }

		/// <summary>
		/// Configuration controlling version warning levels and per package overrides.
		/// </summary>
		public VersionWarningConfig? VersionWarnings { get; set; }

		/// <summary>
		/// Configuration for automatic package update and PR creation.
		/// </summary>
		public UpdateConfig? Update { get; set; }

		/// <summary>
		/// If true, skip reading renovate.json files from repositories.
		/// </summary>
		public bool? IgnoreRenovate { get; set; }

		/// <summary>
		/// If true, include legacy <c>packages.config</c> references in the scan.
		/// </summary>
		public bool? IncludePackagesConfig { get; set; }
	}

	/// <summary>
	/// Provides functionality to load configuration from disk and merge with runtime arguments.
	/// </summary>
	public static class ConfigLoader
	{
		/// <summary>
		/// Loads a <see cref="ToolConfig"/> from a JSON file.
		/// </summary>
		/// <param name="path">Absolute or relative path to the JSON configuration file.</param>
		/// <returns>The deserialized <see cref="ToolConfig"/> instance (never null).</returns>
		/// <exception cref="ArgumentException">Thrown when the path is null or whitespace.</exception>
		/// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
		public static ToolConfig Load(string path)
		{
			if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Config path required", nameof(path));

			if (!File.Exists(path)) throw new FileNotFoundException("Config file not found", path);

			var json = File.ReadAllText(path);
			var options = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			};
			var cfg = JsonSerializer.Deserialize<ToolConfig>(json, options) ?? new ToolConfig();

			ResolveSecrets(cfg);

			return cfg;
		}

		/// <summary>
		/// Resolves environment variable references in sensitive config fields.
		/// Supported syntaxes: <c>$env:VAR_NAME</c> (PowerShell-style) and <c>${VAR_NAME}</c> (shell-style).
		/// When a field value matches one of these patterns, it is replaced with the
		/// corresponding environment variable value. If the variable is not set the
		/// original placeholder is kept as-is.
		/// </summary>
		/// <param name="config">The config whose <see cref="ToolConfig.Token"/> and <see cref="ToolConfig.FeedAuth"/> Pat values will be resolved in place.</param>
		internal static void ResolveSecrets(ToolConfig config)
		{
			ArgumentNullException.ThrowIfNull(config);

			config.Token = ResolveEnvironmentVariable(config.Token);

			if (config.FeedAuth is { Count: > 0 })
			{
				for (int i = 0; i < config.FeedAuth.Count; i++)
				{
					var auth = config.FeedAuth[i];
					var resolvedPat = ResolveEnvironmentVariable(auth.Pat);

					if (!ReferenceEquals(resolvedPat, auth.Pat))
					{
						config.FeedAuth[i] = auth with { Pat = resolvedPat };
					}
				}
			}
		}

		private static readonly Regex EnvVarPattern = new(
			@"^(?:\$env:(?<name1>[A-Za-z_][A-Za-z0-9_]*))|(?:\$\{(?<name2>[A-Za-z_][A-Za-z0-9_]*)\})$",
			RegexOptions.Compiled | RegexOptions.CultureInvariant);

		/// <summary>
		/// Resolves a single string value that may contain an environment variable reference.
		/// Returns the environment variable value when the entire string matches
		/// <c>$env:VAR</c> or <c>${VAR}</c>, otherwise returns the original value unchanged.
		/// </summary>
		/// <param name="value">The raw config value to resolve.</param>
		/// <returns>The resolved value, or the original value if no pattern matched or the variable is not set.</returns>
		internal static string? ResolveEnvironmentVariable(string? value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return value;
			}

			var match = EnvVarPattern.Match(value);

			if (!match.Success)
			{
				return value;
			}

			var varName = match.Groups["name1"].Success
				? match.Groups["name1"].Value
				: match.Groups["name2"].Value;

			return Environment.GetEnvironmentVariable(varName) ?? value;
		}

		/// <summary>
		/// Merge config values onto parsed args (args override file).
		/// </summary>
		/// <param name="config">The loaded configuration object whose values will be applied.</param>
		/// <param name="context">The mutable context to populate.</param>
		public static void ApplyConfig(ToolConfig config, ApplyConfigContext context)
		{
			ArgumentNullException.ThrowIfNull(config);
			ArgumentNullException.ThrowIfNull(context);

			if (!string.IsNullOrWhiteSpace(config.Organization) && string.IsNullOrWhiteSpace(context.Organization))
			{
				context.Organization = config.Organization;
			}

			if (!string.IsNullOrWhiteSpace(config.Token) && string.IsNullOrWhiteSpace(context.Token))
			{
				context.Token = config.Token;
			}

			if (!string.IsNullOrWhiteSpace(config.Project) && string.IsNullOrWhiteSpace(context.Project))
			{
				context.Project = config.Project;
			}

			if (config.MaxRepos is > 0 && (!context.MaxRepos.HasValue || context.MaxRepos.Value <= 0))
			{
				context.MaxRepos = config.MaxRepos;
			}

			if (config.IncludeArchived.HasValue && context.IncludeArchived == null)
			{
				context.IncludeArchived = config.IncludeArchived.Value;
			}

			if (config.ResolveNuGet.HasValue && context.ResolveNuGet == null)
			{
				context.ResolveNuGet = config.ResolveNuGet.Value;
			}

			if (config.Detailed.HasValue && context.ShowDetailedInfo == null)
			{
				context.ShowDetailedInfo = config.Detailed.Value;
			}

			if (config.NoDefaultExclusions.HasValue && context.NoDefaultExclusions == null)
			{
				context.NoDefaultExclusions = config.NoDefaultExclusions.Value;
			}

			if (config.CaseSensitive.HasValue && context.CaseSensitive == null)
			{
				context.CaseSensitive = config.CaseSensitive.Value;
			}

			if (config.CaseSensitiveProjectFilters.HasValue && context.CaseSensitiveCsprojFilters == null)
			{
				context.CaseSensitiveCsprojFilters = config.CaseSensitiveProjectFilters.Value;
			}

			if (config.ExcludePrefixes?.Any() == true)
			{
				foreach (var prefix in config.ExcludePrefixes)
				{
					if (!context.ExcludePrefixes.Contains(prefix))
					{
						context.ExcludePrefixes.Add(prefix);
					}
				}
			}

			if (config.ExcludePackages?.Any() == true)
			{
				foreach (var package in config.ExcludePackages)
				{
					if (!context.ExcludePackages.Contains(package))
					{
						context.ExcludePackages.Add(package);
					}
				}
			}

			if (config.ExcludePatterns?.Any() == true)
			{
				foreach (var pattern in config.ExcludePatterns)
				{
					if (!context.ExcludePatterns.Contains(pattern))
					{
						context.ExcludePatterns.Add(pattern);
					}
				}
			}

			if (config.ExcludeProjectPatterns?.Any() == true)
			{
				foreach (var pattern in config.ExcludeProjectPatterns)
				{
					if (!context.ExcludeCsprojPatterns.Contains(pattern))
					{
						context.ExcludeCsprojPatterns.Add(pattern);
					}
				}
			}

			if (config.ExcludeRepositories?.Any() == true)
			{
				foreach (var pattern in config.ExcludeRepositories)
				{
					if (!context.ExcludeRepositories.Contains(pattern))
					{
						context.ExcludeRepositories.Add(pattern);
					}
				}
			}

			if (config.IncludeRepositories?.Any() == true)
			{
				foreach (var pattern in config.IncludeRepositories)
				{
					if (!context.IncludeRepositories.Contains(pattern))
					{
						context.IncludeRepositories.Add(pattern);
					}
				}
			}

			if (config.IgnoreRenovate.HasValue && context.IgnoreRenovate == null)
			{
				context.IgnoreRenovate = config.IgnoreRenovate.Value;
			}

			if (config.IncludePackagesConfig.HasValue && context.IncludePackagesConfig == null)
			{
				context.IncludePackagesConfig = config.IncludePackagesConfig.Value;
			}

			if (config.Feeds?.Any() == true)
			{
				foreach (var feed in config.Feeds)
				{
					if (!context.Feeds.Any(f => f.Name.Equals(feed.Name, StringComparison.OrdinalIgnoreCase)))
					{
						context.Feeds.Add(feed);
					}
				}
			}

			if (!string.IsNullOrWhiteSpace(config.ExportPackages) && string.IsNullOrWhiteSpace(context.ExportPackagesPath))
			{
				context.ExportPackagesPath = config.ExportPackages;
			}

			if (!string.IsNullOrWhiteSpace(config.ExportWarnings) && string.IsNullOrWhiteSpace(context.ExportWarningsPath))
			{
				context.ExportWarningsPath = config.ExportWarnings;
			}

			if (!string.IsNullOrWhiteSpace(config.ExportRecommendations) && string.IsNullOrWhiteSpace(context.ExportRecommendationsPath))
			{
				context.ExportRecommendationsPath = config.ExportRecommendations;
			}

			if (!string.IsNullOrWhiteSpace(config.ExportSbom) && string.IsNullOrWhiteSpace(context.ExportSbomPath))
			{
				context.ExportSbomPath = config.ExportSbom;
			}

			if (config.ExportFormat.HasValue && context.ExportFormat == null)
			{
				context.ExportFormat = config.ExportFormat.Value;
			}

			if (config.FeedAuth?.Any() == true)
			{
				foreach (var auth in config.FeedAuth)
				{
					if (!context.FeedAuth.Any(a => a.FeedName.Equals(auth.FeedName, StringComparison.OrdinalIgnoreCase)))
					{
						context.FeedAuth.Add(auth);
					}
				}
			}

			if (config.VersionWarnings != null && context.VersionWarningConfig == null)
			{
				context.VersionWarningConfig = config.VersionWarnings;
			}

			if (config.Update != null && context.UpdateConfig == null)
			{
				context.UpdateConfig = config.Update;
			}
		}
	}
}
