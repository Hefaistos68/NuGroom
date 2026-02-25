using NuGroom.ADO;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGroom.Configuration
{
	/// <summary>
	/// Represents the relevant subset of a Renovate configuration file (renovate.json).
	/// See https://docs.renovatebot.com/configuration-options/ for the full schema.
	/// </summary>
	public class RenovateConfig
	{
		/// <summary>
		/// List of package names to ignore (will not be updated or reported).
		/// Maps to Renovate's <c>ignoreDeps</c> field.
		/// </summary>
		[JsonPropertyName("ignoreDeps")]
		public List<string>? IgnoreDeps { get; set; }

		/// <summary>
		/// List of rules to apply to matching packages.
		/// Maps to Renovate's <c>packageRules</c> field.
		/// </summary>
		[JsonPropertyName("packageRules")]
		public List<RenovatePackageRule>? PackageRules { get; set; }

		/// <summary>
		/// List of reviewer usernames or email addresses to assign to PRs.
		/// Maps to Renovate's <c>reviewers</c> field.
		/// </summary>
		[JsonPropertyName("reviewers")]
		public List<string>? Reviewers { get; set; }
	}

	/// <summary>
	/// Represents a single package rule from Renovate configuration.
	/// Only the fields relevant to this tool are mapped.
	/// </summary>
	public class RenovatePackageRule
	{
		/// <summary>
		/// Exact package names this rule applies to.
		/// </summary>
		[JsonPropertyName("matchPackageNames")]
		public List<string>? MatchPackageNames { get; set; }

		/// <summary>
		/// Regex patterns to match package names this rule applies to.
		/// </summary>
		[JsonPropertyName("matchPackagePatterns")]
		public List<string>? MatchPackagePatterns { get; set; }

		/// <summary>
		/// Whether updates are enabled for matched packages. When <c>false</c>, matched packages are pinned.
		/// </summary>
		[JsonPropertyName("enabled")]
		public bool? Enabled { get; set; }

		/// <summary>
		/// List of reviewer usernames or email addresses for PRs affecting matched packages.
		/// Overrides the top-level <c>reviewers</c> for matching packages.
		/// </summary>
		[JsonPropertyName("reviewers")]
		public List<string>? Reviewers { get; set; }
	}

	/// <summary>
	/// Parsed result of a Renovate configuration mapped to this tool's concepts
	/// </summary>
	/// <param name="IgnoredPackages">Package names to exclude from scanning and updates.</param>
	/// <param name="DisabledPackages">Package names disabled via packageRules (enabled=false).</param>
	/// <param name="Reviewers">Top-level PR reviewer overrides.</param>
	/// <param name="PackageReviewers">Per-package reviewer overrides from packageRules.</param>
	public record RenovateOverrides(
		HashSet<string> IgnoredPackages,
		HashSet<string> DisabledPackages,
		List<string>? Reviewers,
		Dictionary<string, List<string>> PackageReviewers);

	/// <summary>
	/// Reads and interprets Renovate configuration files from repositories
	/// </summary>
	public static class RenovateConfigReader
	{
		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true,
			ReadCommentHandling         = JsonCommentHandling.Skip,
			AllowTrailingCommas         = true
		};

		/// <summary>
		/// Known file paths where Renovate configuration can be found, in priority order
		/// </summary>
		private static readonly string[] KnownPaths =
		[
			"/renovate.json",
			"/.renovaterc",
			"/.renovaterc.json",
			"/.github/renovate.json"
		];

		/// <summary>
		/// Attempts to read and parse a Renovate configuration from a repository by checking known file paths
		/// </summary>
		/// <param name="client">Azure DevOps client for file access.</param>
		/// <param name="repository">The repository to read from.</param>
		/// <returns>Parsed <see cref="RenovateOverrides"/>, or <c>null</c> if no Renovate config is found.</returns>
		public static async Task<RenovateOverrides?> TryReadFromRepositoryAsync(
			AzureDevOpsClient client,
			Microsoft.TeamFoundation.SourceControl.WebApi.GitRepository repository)
		{
			foreach (var path in KnownPaths)
			{
				var content = await client.ReadFileContentByPathAsync(repository, path);

				if (!string.IsNullOrWhiteSpace(content))
				{
					Logger.Debug($"Found Renovate config at {path} in {repository.Name}");

					return Parse(content, repository.Name);
				}
			}

			return null;
		}

		/// <summary>
		/// Parses Renovate JSON content into tool-specific overrides
		/// </summary>
		/// <param name="json">Raw JSON content of the Renovate config file.</param>
		/// <param name="repositoryName">Repository name for logging.</param>
		/// <returns>Parsed <see cref="RenovateOverrides"/>.</returns>
		public static RenovateOverrides Parse(string json, string repositoryName)
		{
			var ignoredPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var disabledPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var packageReviewers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
			List<string>? reviewers = null;

			try
			{
				var config = JsonSerializer.Deserialize<RenovateConfig>(json, JsonOptions);

				if (config == null)
				{
					return new RenovateOverrides(ignoredPackages, disabledPackages, null, packageReviewers);
				}

				// Map ignoreDeps
				if (config.IgnoreDeps != null)
				{
					foreach (var dep in config.IgnoreDeps)
					{
						ignoredPackages.Add(dep);
					}

					Logger.Debug($"Renovate [{repositoryName}]: {ignoredPackages.Count} ignored dependency(ies)");
				}

				// Map top-level reviewers
				if (config.Reviewers != null && config.Reviewers.Count > 0)
				{
					reviewers = config.Reviewers;
					Logger.Debug($"Renovate [{repositoryName}]: {reviewers.Count} reviewer(s) configured");
				}

				// Map packageRules
				if (config.PackageRules != null)
				{
					foreach (var rule in config.PackageRules)
					{
						var matchedNames = ResolveMatchedPackageNames(rule);

						if (rule.Enabled == false)
						{
							foreach (var name in matchedNames)
							{
								disabledPackages.Add(name);
							}
						}

						if (rule.Reviewers != null && rule.Reviewers.Count > 0)
						{
							foreach (var name in matchedNames)
							{
								packageReviewers[name] = rule.Reviewers;
							}
						}
					}

					if (disabledPackages.Count > 0)
					{
						Logger.Debug($"Renovate [{repositoryName}]: {disabledPackages.Count} disabled package(s) via rules");
					}
				}
			}
			catch (JsonException ex)
			{
				Logger.Warning($"Failed to parse Renovate config in {repositoryName}: {ex.Message}");
			}

			return new RenovateOverrides(ignoredPackages, disabledPackages, reviewers, packageReviewers);
		}

		/// <summary>
		/// Extracts exact package names from a Renovate package rule's match criteria.
		/// Only <c>matchPackageNames</c> is used for exact matching; patterns are stored as-is for logging.
		/// </summary>
		private static List<string> ResolveMatchedPackageNames(RenovatePackageRule rule)
		{
			var names = new List<string>();

			if (rule.MatchPackageNames != null)
			{
				names.AddRange(rule.MatchPackageNames);
			}

			return names;
		}

		/// <summary>
		/// Checks whether a package name matches any disabled pattern from a Renovate packageRules entry
		/// </summary>
		/// <param name="packageName">The package name to check.</param>
		/// <param name="overrides">The Renovate overrides to check against.</param>
		/// <returns><c>true</c> if the package is ignored or disabled by the Renovate config.</returns>
		public static bool IsPackageExcluded(string packageName, RenovateOverrides overrides)
		{
			if (overrides.IgnoredPackages.Contains(packageName))
			{
				return true;
			}

			if (overrides.DisabledPackages.Contains(packageName))
			{
				return true;
			}

			return false;
		}
	}
}
