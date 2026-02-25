namespace NuGroom.ADO
{
	/// <summary>
	/// Configuration settings for connecting to Azure DevOps
	/// </summary>
	public class AzureDevOpsConfig
	{
		/// <summary>
		/// Azure DevOps organization URL (e.g., https://dev.azure.com/yourorg)
		/// </summary>
		public required string OrganizationUrl { get; set; }

		/// <summary>
		/// Personal Access Token for authentication
		/// </summary>
		public required string PersonalAccessToken { get; set; }

		/// <summary>
		/// Optional: Specific project name to search. If null, searches all projects
		/// </summary>
		public string? ProjectName { get; set; }

		/// <summary>
		/// Optional: Maximum number of repositories to process. Default is 100
		/// </summary>
		public int MaxRepositories { get; set; } = 100;

		/// <summary>
		/// Optional: Whether to include archived repositories. Default is false
		/// </summary>
		public bool IncludeArchivedRepositories { get; set; } = false;

		/// <summary>
		/// Optional: Regex patterns to exclude .csproj files (e.g., ".*\\.Tests\\.csproj", ".*\\.Core\\..*\\.csproj")
		/// </summary>
		public List<string> ExcludeCsprojPatterns { get; set; } = new();

		/// <summary>
		/// Optional: Whether to use case-sensitive matching for .csproj exclusions. Default is false
		/// </summary>
		public bool CaseSensitiveCsprojFilters { get; set; } = false;

		/// <summary>
		/// Optional: Regex patterns to exclude repositories by name (e.g., ".*\.Archive", "Legacy-.*")
		/// </summary>
		public List<string> ExcludeRepositories { get; set; } = new();

		/// <summary>
		/// Optional: Regex patterns to include repositories by name. When specified, only repositories matching
		/// these patterns are processed and they are scanned in the order the patterns are listed.
		/// Exclusion patterns are still applied on top. (e.g., "Vestas\\.Cir\\.Shared\\..*", "MyRepo")
		/// </summary>
		public List<string> IncludeRepositories { get; set; } = new();

		/// <summary>
		/// Validates the configuration
		/// </summary>
		public void Validate()
		{
			if (string.IsNullOrWhiteSpace(OrganizationUrl))
				throw new ArgumentException("Organization URL is required", nameof(OrganizationUrl));

			if (string.IsNullOrWhiteSpace(PersonalAccessToken))
				throw new ArgumentException("Personal Access Token is required", nameof(PersonalAccessToken));

			if (!Uri.TryCreate(OrganizationUrl, UriKind.Absolute, out _))
				throw new ArgumentException("Organization URL must be a valid URL", nameof(OrganizationUrl));

			if (MaxRepositories <= 0)
				throw new ArgumentException("Max repositories must be greater than 0", nameof(MaxRepositories));
		}
	}
}