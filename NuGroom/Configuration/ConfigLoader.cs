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
		/// <param name="organization">Target Azure DevOps organization (may be updated).</param>
		/// <param name="token">Authentication token (may be updated).</param>
		/// <param name="project">Optional project filter (may be updated).</param>
		/// <param name="maxRepos">Maximum repo limit (may be updated if default).</param>
		/// <param name="includeArchived">Flag indicating inclusion of archived repos (may be updated).</param>
		/// <param name="resolveNuGet">Flag indicating whether to resolve NuGet metadata (may be updated).</param>
		/// <param name="showDetailedInfo">Flag for detailed output (may be updated).</param>
		/// <param name="feeds">Collection of feeds to merge new entries into.</param>
		/// <param name="excludePrefixes">Repository name prefixes to exclude (merged).</param>
		/// <param name="excludePackages">Package IDs to exclude (merged).</param>
		/// <param name="excludePatterns">Generic exclusion patterns (merged).</param>
		/// <param name="noDefaultExclusions">Flag disabling default exclusions (may be updated).</param>
		/// <param name="caseSensitive">Flag for case sensitive filtering (may be updated).</param>
		/// <param name="exportPackagesPath">Destination path for package export (may be set).</param>
		/// <param name="feedAuth">Authentication entries for private feeds (merged).</param>
		/// <param name="excludeCsprojPatterns">.csproj-specific exclusion patterns (merged).</param>
		/// <param name="caseSensitiveCsprojFilters">Flag for case sensitivity of .csproj filters (may be updated).</param>
		/// <param name="versionWarningConfig">Version warning configuration (may be set if absent).</param>
		public static void ApplyConfig(ToolConfig config,
			ref string? organization,
			ref string? token,
			ref string? project,
			ref int maxRepos,
			ref bool includeArchived,
			ref bool resolveNuGet,
			ref bool showDetailedInfo,
			List<Feed> feeds,
			List<string> excludePrefixes,
			List<string> excludePackages,
			List<string> excludePatterns,
			ref bool noDefaultExclusions,
			ref bool caseSensitive,
			ref string? exportPackagesPath,
			List<FeedAuth> feedAuth,
			List<string> excludeCsprojPatterns,
			ref bool caseSensitiveCsprojFilters,
			ref VersionWarningConfig? versionWarningConfig,
			ref UpdateConfig? updateConfig)
		{
			if (config.Organization != null && organization == null) organization = config.Organization;
			if (config.Token != null && token == null) token = config.Token;
			if (config.Project != null && project == null) project = config.Project;
			if (config.MaxRepos.HasValue && config.MaxRepos.Value > 0 && maxRepos == 100) maxRepos = config.MaxRepos.Value;
			if (config.IncludeArchived.HasValue && includeArchived == false) includeArchived = config.IncludeArchived.Value;
			if (config.ResolveNuGet.HasValue) resolveNuGet = config.ResolveNuGet.Value;
			if (config.Detailed.HasValue) showDetailedInfo = config.Detailed.Value;
			if (config.NoDefaultExclusions.HasValue) noDefaultExclusions = config.NoDefaultExclusions.Value;
			if (config.CaseSensitive.HasValue) caseSensitive = config.CaseSensitive.Value;
			if (config.CaseSensitiveProjectFilters.HasValue) caseSensitiveCsprojFilters = config.CaseSensitiveProjectFilters.Value;
			if (config.ExcludePrefixes?.Any() == true) excludePrefixes.AddRange(config.ExcludePrefixes.Where(p => !excludePrefixes.Contains(p)));
			if (config.ExcludePackages?.Any() == true) excludePackages.AddRange(config.ExcludePackages.Where(p => !excludePackages.Contains(p)));
			if (config.ExcludePatterns?.Any() == true) excludePatterns.AddRange(config.ExcludePatterns.Where(p => !excludePatterns.Contains(p)));
			if (config.ExcludeProjectPatterns?.Any() == true) excludeCsprojPatterns.AddRange(config.ExcludeProjectPatterns.Where(p => !excludeCsprojPatterns.Contains(p)));

			// Merge named feeds
			if (config.Feeds?.Any() == true)
			{
				foreach (var feed in config.Feeds)
				{
					if (!feeds.Any(f => f.Name.Equals(feed.Name, StringComparison.OrdinalIgnoreCase)))
					{
						feeds.Add(feed);
					}
				}
			}

			if (config.ExportPackages != null && exportPackagesPath == null) exportPackagesPath = config.ExportPackages;

			// Merge FeedAuth entries by feed name
			if (config.FeedAuth?.Any() == true)
			{
				foreach (var auth in config.FeedAuth)
				{
					if (!feedAuth.Any(a => a.FeedName.Equals(auth.FeedName, StringComparison.OrdinalIgnoreCase)))
					{
						feedAuth.Add(auth);
					}
				}
			}

			// Merge version warning config
			if (config.VersionWarnings != null && versionWarningConfig == null)
			{
				versionWarningConfig = config.VersionWarnings;
			}

			// Merge update config
			if (config.Update != null && updateConfig == null)
			{
				updateConfig = config.Update;
			}
		}
	}
}
