using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Identity;
using Microsoft.VisualStudio.Services.Identity.Client;
using Microsoft.VisualStudio.Services.WebApi;

using System.Text;
using System.Text.RegularExpressions;

namespace NuGroom.ADO
{
	/// <summary>
	/// Service for interacting with Azure DevOps API
	/// </summary>
	public class AzureDevOpsClient : IDisposable
	{
		/// <summary>
		/// Supported project file extensions (.csproj, .vbproj, .fsproj).
		/// </summary>
		private static readonly string[] SupportedProjectExtensions = [".csproj", ".vbproj", ".fsproj"];

		private readonly VssConnection _connection;
		private readonly GitHttpClient _gitClient;
		private readonly ProjectHttpClient _projectClient;
		private readonly AzureDevOpsConfig _config;
		private readonly List<Regex> _excludeProjectRegexes; // precompiled project file exclusion patterns
		private readonly List<Regex> _excludeRepoRegexes;    // precompiled repository exclusion patterns
		private readonly List<Regex> _includeRepoRegexes;    // precompiled repository inclusion patterns

		/// <summary>
		/// Initializes a new instance of the <see cref="AzureDevOpsClient"/> class.
		/// </summary>
		/// <param name="config">The config.</param>
		public AzureDevOpsClient(AzureDevOpsConfig config)
		{
			_config = config ?? throw new ArgumentNullException(nameof(config));

			try
			{
				_config.Validate();
				Logger.Debug($"Initializing Azure DevOps client for {_config.OrganizationUrl}");

				var credentials = new VssBasicCredential(string.Empty, _config.PersonalAccessToken);
				_connection = new VssConnection(new Uri(_config.OrganizationUrl), credentials);

				_gitClient = _connection.GetClient<GitHttpClient>();
				_projectClient = _connection.GetClient<ProjectHttpClient>();

				// Precompile exclusion regex patterns once
				_excludeProjectRegexes = new List<Regex>();
				if (_config.ExcludeProjectPatterns != null && _config.ExcludeProjectPatterns.Count > 0)
				{
					var options = _config.CaseSensitiveProjectFilters ? RegexOptions.Compiled : (RegexOptions.Compiled | RegexOptions.IgnoreCase);
					foreach (var pattern in _config.ExcludeProjectPatterns)
					{
						try { _excludeProjectRegexes.Add(new Regex(pattern, options, TimeSpan.FromSeconds(2))); }
						catch (Exception ex) { Logger.Warning($"Invalid exclude regex '{pattern}': {ex.Message}"); }
					}

					if (_excludeProjectRegexes.Count > 0)
					{
						Logger.Debug($"Compiled {_excludeProjectRegexes.Count} project file exclusion pattern(s)");
					}
				}

				// Precompile repository exclusion regex patterns once
				_excludeRepoRegexes = new List<Regex>();
				if (_config.ExcludeRepositories != null && _config.ExcludeRepositories.Count > 0)
				{
					var repoOptions = RegexOptions.Compiled | RegexOptions.IgnoreCase;
					foreach (var pattern in _config.ExcludeRepositories)
					{
						try { _excludeRepoRegexes.Add(new Regex(pattern, repoOptions, TimeSpan.FromSeconds(2))); }
						catch (Exception ex) { Logger.Warning($"Invalid exclude-repo regex '{pattern}': {ex.Message}"); }
					}

					if (_excludeRepoRegexes.Count > 0)
					{
						Logger.Debug($"Compiled {_excludeRepoRegexes.Count} repository exclusion pattern(s)");
					}
				}

				// Precompile repository inclusion regex patterns once
				_includeRepoRegexes = new List<Regex>();
				if (_config.IncludeRepositories != null && _config.IncludeRepositories.Count > 0)
				{
					var includeOptions = RegexOptions.Compiled | RegexOptions.IgnoreCase;
					foreach (var pattern in _config.IncludeRepositories)
					{
						try { _includeRepoRegexes.Add(new Regex(pattern, includeOptions, TimeSpan.FromSeconds(2))); }
						catch (Exception ex) { Logger.Warning($"Invalid include-repo regex '{pattern}': {ex.Message}"); }
					}

					if (_includeRepoRegexes.Count > 0)
					{
						Logger.Debug($"Compiled {_includeRepoRegexes.Count} repository inclusion pattern(s)");
					}
				}

				Logger.Debug("Azure DevOps client initialized successfully");
			}
			catch (Exception ex)
			{
				throw new AzureDevOpsException($"Failed to initialize Azure DevOps client: {ex.Message}", ex);
			}
		}

		/// <summary>
		/// Determines whether a file path has a supported project file extension (.csproj, .vbproj, .fsproj).
		/// </summary>
		/// <param name="path">The file path to check.</param>
		/// <returns><c>true</c> if the file has a supported project extension; otherwise, <c>false</c>.</returns>
		private static bool IsProjectFile(string path)
		{
			return SupportedProjectExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Determines whether a given project file path should be excluded based on configured exclusion patterns.
		/// </summary>
		/// <param name="path">The file path to check against exclusion patterns.</param>
		/// <returns><c>true</c> if the file matches any exclusion pattern; otherwise, <c>false</c>.</returns>
		private bool IsExcludedProject(string path)
		{
			if (_excludeProjectRegexes.Count == 0) return false;
			var fileName = Path.GetFileName(path);
			foreach (var rx in _excludeProjectRegexes)
			{
				if (rx.IsMatch(fileName))
				{
					Logger.Debug($"Excluding {fileName} (matched pattern: {rx.ToString()})");
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Determines whether a repository name matches any configured exclusion pattern.
		/// </summary>
		/// <param name="repositoryName">The repository name to check.</param>
		/// <returns><c>true</c> if the repository matches any exclusion pattern; otherwise <c>false</c>.</returns>
		private bool IsExcludedRepository(string repositoryName)
		{
			if (_excludeRepoRegexes.Count == 0)
			{
				return false;
			}

			foreach (var rx in _excludeRepoRegexes)
			{
				if (rx.IsMatch(repositoryName))
				{
					Logger.Debug($"Excluding repository '{repositoryName}' (matched pattern: {rx})");

					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Gets all repositories across all projects or a specific project
		/// </summary>
		public async Task<List<GitRepository>> GetRepositoriesAsync()
		{
			var repositories = new List<GitRepository>();

			try
			{
				if (!string.IsNullOrWhiteSpace(_config.ProjectName))
				{
					Logger.Debug($"Getting repositories for project: {_config.ProjectName}");
					var projectRepos = await _gitClient.GetRepositoriesAsync(_config.ProjectName);
					repositories.AddRange(projectRepos);
					Logger.Debug($"Found {projectRepos.Count} repositories in project {_config.ProjectName}");
				}
				else
				{
					Logger.Debug("Getting all projects");
					var projects = await _projectClient.GetProjects();
					Logger.Debug($"Found {projects.Count} projects");

					foreach (var project in projects.Take(_config.MaxRepositories))
					{
						try
						{
							Logger.Debug($"Getting repositories for project: {project.Name}");
							var projectRepos = await _gitClient.GetRepositoriesAsync(project.Id);
							repositories.AddRange(projectRepos);
							Logger.Debug($"Found {projectRepos.Count} repositories in project {project.Name}");

							if (repositories.Count >= _config.MaxRepositories)
							{
								Logger.Warning($"Reached maximum repository limit of {_config.MaxRepositories}");
								break;
							}
						}
						catch (Exception ex)
						{
							Logger.Warning($"Could not access repositories for project {project.Name}: {ex.Message}");
						}
					}
				}

				// Filter out archived repositories if requested
				if (!_config.IncludeArchivedRepositories)
				{
					var beforeCount = repositories.Count;
					repositories = repositories.Where(r => r.IsDisabled != true).ToList();
					var filteredCount = beforeCount - repositories.Count;
					if (filteredCount > 0)
					{
						Logger.Debug($"Filtered out {filteredCount} archived repositories");
					}
				}

				// Filter out excluded repositories by name pattern
				if (_excludeRepoRegexes.Count > 0)
				{
					var beforeCount = repositories.Count;
					repositories = repositories.Where(r => !IsExcludedRepository(r.Name)).ToList();
					var filteredCount = beforeCount - repositories.Count;

					if (filteredCount > 0)
					{
						Logger.Debug($"Filtered out {filteredCount} repository(ies) by exclusion patterns");
					}
				}

				// When IncludeRepositories is specified, keep only repos matching the patterns and preserve the specified order
				if (_includeRepoRegexes.Count > 0)
				{
					var ordered = new List<GitRepository>();

					foreach (var rx in _includeRepoRegexes)
					{
						foreach (var repo in repositories)
						{
							if (rx.IsMatch(repo.Name) && !ordered.Any(r => r.Name.Equals(repo.Name, StringComparison.OrdinalIgnoreCase)))
							{
								ordered.Add(repo);
							}
						}
					}

					foreach (var rx in _includeRepoRegexes)
					{
						if (!repositories.Any(r => rx.IsMatch(r.Name)))
						{
							Logger.Warning($"Include repository pattern '{rx}' did not match any repository");
						}
					}

					Logger.Debug($"Filtered to {ordered.Count} included repository(ies) (pattern order preserved)");
					repositories = ordered;
				}

				var finalRepos = repositories.Take(_config.MaxRepositories).ToList();
				Logger.Info($"Retrieved {finalRepos.Count} repositories for scanning");
				return finalRepos;
			}
			catch (Exception ex) when (!(ex is AzureDevOpsException))
			{
				throw new AzureDevOpsException($"Failed to retrieve repositories: {ex.Message}", ex);
			}
		}

		/// <summary>
		/// Gets all project files (.csproj, .vbproj, .fsproj) from a repository with exclusion applied before content reads.
		/// </summary>
		public async Task<List<GitItem>> GetProjectFilesAsync(GitRepository repository)
		{
			try
			{
				Logger.Debug($"Getting project files from repository: {repository.Name}");

				// Get all items in the repository
				var items = await _gitClient.GetItemsAsync(
					repository.Id,
					scopePath: "/",
					recursionLevel: VersionControlRecursionType.Full);

				// Early filter: only supported project files and not excluded
				var projectFiles = items
					.Where(item => !item.IsFolder && IsProjectFile(item.Path) && !IsExcludedProject(item.Path))
					.ToList();

				Logger.Debug($"Returning {projectFiles.Count} project file(s) after early exclusion filtering in repository {repository.Name}");
				return projectFiles;
			}
			catch (Exception ex)
			{
				Logger.Warning($"Could not access files in repository {repository.Name}: {ex.Message}");
				return new List<GitItem>();
			}
		}

		/// <summary>
		/// Result of a single item enumeration that classifies repository files into
		/// project files and package management files.
		/// </summary>
		/// <param name="ProjectFiles">Filtered project files (.csproj, .vbproj, .fsproj).</param>
		/// <param name="ManagementFiles">Package management files (Directory.Packages.props, packages.config).</param>
		public record RepositoryFiles(List<GitItem> ProjectFiles, List<GitItem> ManagementFiles);

		/// <summary>
		/// Gets both project files and package management files from a single repository
		/// item enumeration, avoiding a redundant API traversal.
		/// </summary>
		/// <param name="repository">The repository to scan.</param>
		/// <param name="includePackagesConfig">
		/// When <c>false</c>, <c>packages.config</c> files are excluded from management files.
		/// </param>
		/// <returns>A <see cref="RepositoryFiles"/> containing both classified file lists.</returns>
		public async Task<RepositoryFiles> GetRepositoryFilesAsync(
			GitRepository repository,
			bool includePackagesConfig = false)
		{
			try
			{
				Logger.Debug($"Getting repository files from: {repository.Name}");

				var items = await _gitClient.GetItemsAsync(
					repository.Id,
					scopePath: "/",
					recursionLevel: VersionControlRecursionType.Full);

				var projectFiles = new List<GitItem>();
				var managementFiles = new List<GitItem>();

				foreach (var item in items)
				{
					if (item.IsFolder)
					{
						continue;
					}

					if (IsProjectFile(item.Path) && !IsExcludedProject(item.Path))
					{
						projectFiles.Add(item);
					}
					else if (IsPackageManagementFile(item.Path, includePackagesConfig))
					{
						managementFiles.Add(item);
					}
				}

				Logger.Debug($"Found {projectFiles.Count} project file(s) and {managementFiles.Count} management file(s) in repository {repository.Name}");

				return new RepositoryFiles(projectFiles, managementFiles);
			}
			catch (Exception ex)
			{
				Logger.Warning($"Could not access files in repository {repository.Name}: {ex.Message}");

				return new RepositoryFiles(new List<GitItem>(), new List<GitItem>());
			}
		}

		/// <summary>
		/// Gets package management files (<c>Directory.Packages.props</c> and <c>packages.config</c>)
		/// from a repository. These files are discovered from the same item tree used by
		/// <see cref="GetProjectFilesAsync"/> but matched by exact file name.
		/// </summary>
		/// <param name="repository">The repository to scan.</param>
		/// <param name="includePackagesConfig">
		/// When <c>false</c>, <c>packages.config</c> files are excluded from the result.
		/// </param>
		/// <returns>List of matching <see cref="GitItem"/> entries.</returns>
		public async Task<List<GitItem>> GetPackageManagementFilesAsync(
			GitRepository repository,
			bool includePackagesConfig = false)
		{
			try
			{
				Logger.Debug($"Getting package management files from repository: {repository.Name}");

				var items = await _gitClient.GetItemsAsync(
					repository.Id,
					scopePath: "/",
					recursionLevel: VersionControlRecursionType.Full);

				var managementFiles = items
					.Where(item => !item.IsFolder && IsPackageManagementFile(item.Path, includePackagesConfig))
					.ToList();

				Logger.Debug($"Found {managementFiles.Count} package management file(s) in repository {repository.Name}");

				return managementFiles;
			}
			catch (Exception ex)
			{
				Logger.Warning($"Could not access package management files in repository {repository.Name}: {ex.Message}");

				return new List<GitItem>();
			}
		}

		/// <summary>
		/// Determines whether a file path matches a known package management file name.
		/// </summary>
		private static bool IsPackageManagementFile(string path, bool includePackagesConfig)
		{
			var fileName = Path.GetFileName(path);

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
		/// Gets the content of a file (skips excluded project files defensively)
		/// </summary>
		public async Task<string> GetFileContentAsync(GitRepository repository, GitItem item)
		{
			if (IsProjectFile(item.Path) && IsExcludedProject(item.Path))
			{
				Logger.Debug($"Skipping content read for excluded file: {item.Path}");
				return string.Empty;
			}
			try
			{
				Logger.Debug($"Reading file content: {item.Path} from repository {repository.Name}");

				using var stream = await _gitClient.GetItemContentAsync(repository.Id, item.Path);
				using var reader = new StreamReader(stream, Encoding.UTF8);
				var content = await reader.ReadToEndAsync();

				Logger.Debug($"Successfully read {content.Length} characters from {item.Path}");
				return content;
			}
			catch (Exception ex)
			{
				Logger.Warning($"Could not read file {item.Path} in repository {repository.Name}: {ex.Message}");
				return string.Empty;
			}
		}

		/// <summary>
		/// Reads a file from a repository by its exact path. Returns empty string if the file does not exist.
		/// </summary>
		/// <param name="repository">The repository to read from.</param>
		/// <param name="filePath">Full path to the file (e.g., "/renovate.json").</param>
		/// <returns>The file content, or empty string if not found or on error.</returns>
		public async Task<string> ReadFileContentByPathAsync(GitRepository repository, string filePath)
		{
			try
			{
				using var stream = await _gitClient.GetItemContentAsync(repository.Id, filePath);
				using var reader = new StreamReader(stream, Encoding.UTF8);

				return await reader.ReadToEndAsync();
			}
			catch
			{
				return string.Empty;
			}
		}

		/// <summary>
		/// Finds the latest branch matching a pattern (e.g., "develop/*") by semver ordering
		/// </summary>
		/// <param name="repository">The repository to search.</param>
		/// <param name="branchPattern">Branch pattern with wildcard (e.g., "develop/*").</param>
		/// <returns>The full ref name of the latest matching branch, or null if none found.</returns>
		public async Task<(string RefName, string ObjectId)?> FindLatestBranchAsync(GitRepository repository, string branchPattern)
		{
			try
			{
				var prefix = branchPattern.Replace("*", "");
				var filterPrefix = $"refs/heads/{prefix}";
				Logger.Debug($"Searching for branches matching '{filterPrefix}' in {repository.Name}");

				var refs = await _gitClient.GetRefsAsync(repository.Id, filter: $"heads/{prefix}");

				if (refs == null || refs.Count == 0)
				{
					Logger.Debug($"No branches matching '{branchPattern}' found in {repository.Name}");

					return null;
				}

				// Sort by semver extracted from the branch name suffix
				var sorted = refs
					.Select(r => new { Ref = r, Version = ExtractVersionFromBranchName(r.Name, filterPrefix) })
					.Where(r => r.Version != null)
					.OrderByDescending(r => r.Version)
					.FirstOrDefault();

				if (sorted == null)
				{
					// Fall back to most recently updated branch
					var fallback = refs.OrderByDescending(r => r.Name).First();
					Logger.Debug($"No semver branches found, using '{fallback.Name}' as fallback");

					return (fallback.Name, fallback.ObjectId);
				}

				Logger.Debug($"Found latest branch: {sorted.Ref.Name} (version {sorted.Version})");

				return (sorted.Ref.Name, sorted.Ref.ObjectId);
			}
			catch (Exception ex)
			{
				Logger.Warning($"Failed to find branches matching '{branchPattern}' in {repository.Name}: {ex.Message}");

				return null;
			}
		}

		/// <summary>
		/// Resolves the default branch of a repository (e.g., refs/heads/main) to a ref name and object ID
		/// </summary>
		/// <param name="repository">The repository whose default branch to resolve.</param>
		/// <returns>The ref name and object ID of the default branch, or null if it cannot be resolved.</returns>
		public async Task<(string RefName, string ObjectId)?> GetDefaultBranchAsync(GitRepository repository)
		{
			try
			{
				var defaultBranch = repository.DefaultBranch;

				if (string.IsNullOrEmpty(defaultBranch))
				{
					Logger.Warning($"Repository '{repository.Name}' has no default branch configured.");

					return null;
				}

				var filter = defaultBranch.Replace("refs/", "");
				var refs = await _gitClient.GetRefsAsync(repository.Id, filter: filter);
				var branchRef = refs?.FirstOrDefault(r => r.Name == defaultBranch);

				if (branchRef == null)
				{
					Logger.Warning($"Could not resolve default branch '{defaultBranch}' in '{repository.Name}'.");

					return null;
				}

				Logger.Debug($"Resolved default branch: {branchRef.Name} ({branchRef.ObjectId}) in {repository.Name}");

				return (branchRef.Name, branchRef.ObjectId);
			}
			catch (Exception ex)
			{
				Logger.Warning($"Failed to resolve default branch for '{repository.Name}': {ex.Message}");

				return null;
			}
		}

		/// <summary>
		/// Extracts a semver-like version from a branch name suffix
		/// </summary>
		private static Version? ExtractVersionFromBranchName(string refName, string prefix)
		{
			var suffix = refName.Replace(prefix, "").TrimStart('/');

			return Version.TryParse(suffix, out var version) ? version : null;
		}

		/// <summary>
		/// Creates a feature branch from a source branch, pushes updated file contents, and returns the new branch ref
		/// </summary>
		/// <param name="repository">The target repository.</param>
		/// <param name="sourceBranchObjectId">The commit object ID to branch from.</param>
		/// <param name="featureBranchName">Name for the new branch (without refs/heads/ prefix).</param>
		/// <param name="fileChanges">Dictionary of file path to new content.</param>
		/// <param name="commitMessage">Commit message for the push.</param>
		/// <param name="newFiles">Optional set of file paths that are new (use <c>Add</c> instead of <c>Edit</c>).</param>
		/// <returns>A tuple containing the ref name of the created branch and the new commit object ID.</returns>
		public async Task<(string BranchRef, string CommitId)> CreateBranchAndPushAsync(
			GitRepository repository,
			string sourceBranchObjectId,
			string featureBranchName,
			Dictionary<string, string> fileChanges,
			string commitMessage,
			HashSet<string>? newFiles = null)
		{
			var newBranchRef = $"refs/heads/{featureBranchName}";
			Logger.Debug($"Creating branch '{newBranchRef}' in {repository.Name} from {sourceBranchObjectId}");

			var changes = fileChanges.Select(kvp => new GitChange
			{
				ChangeType = newFiles?.Contains(kvp.Key) == true
					? VersionControlChangeType.Add
					: VersionControlChangeType.Edit,
				Item = new GitItem { Path = kvp.Key },
				NewContent = new ItemContent
				{
					Content = kvp.Value,
					ContentType = ItemContentType.RawText
				}
			}).ToList();

			// A push must contain exactly one commit and one refUpdate.
			// NewObjectId is omitted so the server computes it from the commit.
			// The commit's Parents tells the server which commit the edits are based on.
			var push = new GitPush
			{
				RefUpdates =
				[
					new GitRefUpdate
					{
						Name = newBranchRef,
						OldObjectId = "0000000000000000000000000000000000000000"
					}
				],
				Commits =
				[
					new GitCommitRef
					{
						Comment = commitMessage,
						Changes = changes,
						Parents = [sourceBranchObjectId]
					}
				]
			};

			var result = await _gitClient.CreatePushAsync(push, repository.Id);
			var newCommitId = result.RefUpdates.First().NewObjectId;
			Logger.Info($"Created branch '{newBranchRef}' with {fileChanges.Count} file(s) in {repository.Name}");

			return (newBranchRef, newCommitId);
		}

		/// <summary>
		/// Creates a branch pointing at the specified commit without pushing any file changes.
		/// </summary>
		/// <param name="repository">The target repository.</param>
		/// <param name="sourceBranchObjectId">The commit object ID the new branch should point to.</param>
		/// <param name="branchName">Branch name (without <c>refs/heads/</c> prefix).</param>
		/// <returns>The full ref name and object ID of the created branch.</returns>
		public async Task<(string RefName, string ObjectId)> CreateBranchAsync(
			GitRepository repository,
			string sourceBranchObjectId,
			string branchName)
		{
			ArgumentNullException.ThrowIfNull(repository);

			if (string.IsNullOrWhiteSpace(sourceBranchObjectId))
			{
				throw new ArgumentException("Source branch object ID is required.", nameof(sourceBranchObjectId));
			}

			if (string.IsNullOrWhiteSpace(branchName))
			{
				throw new ArgumentException("Branch name is required.", nameof(branchName));
			}

			var branchRef = $"refs/heads/{branchName}";
			Logger.Debug($"Creating branch '{branchRef}' at {sourceBranchObjectId} in {repository.Name}");

			var refUpdate = new GitRefUpdate
			{
				Name = branchRef,
				NewObjectId = sourceBranchObjectId,
				OldObjectId = "0000000000000000000000000000000000000000"
			};

			await _gitClient.UpdateRefsAsync(new[] { refUpdate }, repository.Id);
			Logger.Info($"Created branch '{branchRef}' in {repository.Name}");

			return (branchRef, sourceBranchObjectId);
		}

		/// <summary>
		/// Creates a lightweight git tag pointing at the specified commit in the repository.
		/// </summary>
		/// <param name="repository">The target repository.</param>
		/// <param name="tagName">Tag name (without refs/tags/ prefix).</param>
		/// <param name="commitId">The commit object ID the tag should point to.</param>
		public async Task CreateTagAsync(GitRepository repository, string tagName, string commitId)
		{
			ArgumentNullException.ThrowIfNull(repository);

			if (string.IsNullOrWhiteSpace(tagName))
			{
				throw new ArgumentException("Tag name is required.", nameof(tagName));
			}

			if (string.IsNullOrWhiteSpace(commitId))
			{
				throw new ArgumentException("Commit ID is required.", nameof(commitId));
			}

			var tagRef = $"refs/tags/{tagName}";
			Logger.Debug($"Creating tag '{tagRef}' at {commitId} in {repository.Name}");

			var refUpdate = new GitRefUpdate
			{
				Name = tagRef,
				NewObjectId = commitId,
				OldObjectId = "0000000000000000000000000000000000000000"
			};

			await _gitClient.UpdateRefsAsync(new[] { refUpdate }, repository.Id);
			Logger.Info($"Created tag '{tagRef}' in {repository.Name}");
		}

		/// <summary>
		/// Resolves reviewer email addresses or unique names to Azure DevOps identity references.
		/// Falls back to project team membership lookup when the VSSPS identity service is not accessible.
		/// </summary>
		/// <param name="reviewerIdentifiers">List of email addresses or unique names to resolve.</param>
		/// <param name="isRequired">Whether the resolved reviewers are required (must approve) or optional.</param>
		/// <returns>List of resolved identity references for use as PR reviewers.</returns>
		public async Task<List<IdentityRefWithVote>> ResolveReviewerIdentitiesAsync(List<string> reviewerIdentifiers, bool isRequired)
		{
			var reviewers = new List<IdentityRefWithVote>();
			var identityClient = _connection.GetClient<IdentityHttpClient>();
			var label = isRequired ? "required" : "optional";
			var useTeamFallback = false;

			foreach (var identifier in reviewerIdentifiers)
			{
				if (!useTeamFallback)
				{
					try
					{
						var identities = await identityClient.ReadIdentitiesAsync(
							IdentitySearchFilter.General,
							identifier,
							ReadIdentitiesOptions.None,
							QueryMembership.None);

						var identity = identities?.FirstOrDefault();

						if (identity != null)
						{
							reviewers.Add(new IdentityRefWithVote
							{
								Id = identity.Id.ToString(),
								IsRequired = isRequired
							});
							Logger.Debug($"Resolved {label} reviewer '{identifier}' → {identity.Id}");

							continue;
						}

						Logger.Warning($"Could not resolve {label} reviewer identity: '{identifier}'");

						continue;
					}
					catch (Exception ex) when (IsAuthorizationError(ex))
					{
						Logger.Debug($"VSSPS identity service not accessible, falling back to team-based resolution for '{identifier}'");
						useTeamFallback = true;
					}
					catch (Exception ex)
					{
						Logger.Warning($"Failed to resolve {label} reviewer '{identifier}': {ex.Message}");

						continue;
					}
				}

				// Fallback: resolve via project team membership when VSSPS is inaccessible
				var resolvedId = await TryResolveIdentityViaTeamsAsync(identifier);

				if (resolvedId != null)
				{
					reviewers.Add(new IdentityRefWithVote
					{
						Id = resolvedId,
						IsRequired = isRequired
					});
					Logger.Debug($"Resolved {label} reviewer '{identifier}' → {resolvedId} (via team membership)");
				}
				else
				{
					Logger.Warning($"Could not resolve {label} reviewer '{identifier}'. " +
						$"Ensure the PAT has 'Identity (Read)' scope or the reviewer is a member of a team in project '{_config.ProjectName}'.");
				}
			}

			return reviewers;
		}

		/// <summary>
		/// Determines whether an exception represents an authorization or authentication failure against the VSSPS identity service
		/// </summary>
		private static bool IsAuthorizationError(Exception ex)
		{
			var message = ex.Message;

			return message.Contains("VS30063") ||
				   message.Contains("not authorized", StringComparison.OrdinalIgnoreCase) ||
				   message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Attempts to resolve a user identity by searching project team memberships.
		/// Used as a fallback when the VSSPS identity service is not accessible.
		/// </summary>
		/// <param name="identifier">Email or unique name of the user to resolve.</param>
		/// <returns>The identity ID string if found; otherwise null.</returns>
		private async Task<string?> TryResolveIdentityViaTeamsAsync(string identifier)
		{
			if (string.IsNullOrWhiteSpace(_config.ProjectName))
			{
				Logger.Debug("Cannot resolve identity via teams: no project name configured");

				return null;
			}

			try
			{
				var teamClient = _connection.GetClient<TeamHttpClient>();
				var teams = await teamClient.GetTeamsAsync(_config.ProjectName);

				foreach (var team in teams)
				{
					var members = await teamClient.GetTeamMembersWithExtendedPropertiesAsync(
						_config.ProjectName, team.Id.ToString());

					var match = members?.FirstOrDefault(m =>
						m.Identity?.UniqueName?.Equals(identifier, StringComparison.OrdinalIgnoreCase) == true);

					if (match?.Identity?.Id != null)
					{
						return match.Identity.Id;
					}
				}
			}
			catch (Exception ex)
			{
				Logger.Debug($"Team-based identity resolution failed for '{identifier}': {ex.Message}");
			}

			return null;
		}

		/// <summary>
		/// Creates a pull request from a source branch to a target branch
		/// </summary>
		/// <param name="repository">The target repository.</param>
		/// <param name="sourceBranchRef">Full ref name of the source branch (e.g., refs/heads/feature/...).</param>
		/// <param name="targetBranchRef">Full ref name of the target branch (e.g., refs/heads/develop/1.0.10).</param>
		/// <param name="title">PR title.</param>
		/// <param name="description">PR description.</param>
		/// <param name="reviewers">Optional list of resolved reviewer identities to add to the PR.</param>
		/// <returns>The created pull request.</returns>
		public async Task<GitPullRequest> CreatePullRequestAsync(
			GitRepository repository,
			string sourceBranchRef,
			string targetBranchRef,
			string title,
			string description,
			List<IdentityRefWithVote>? reviewers = null)
		{
			Logger.Debug($"Creating PR in {repository.Name}: {sourceBranchRef} → {targetBranchRef}");

			var pr = new GitPullRequest
			{
				SourceRefName = sourceBranchRef,
				TargetRefName = targetBranchRef,
				Title = title,
				Description = description
			};

			if (reviewers != null && reviewers.Count > 0)
			{
				pr.Reviewers = reviewers.ToArray();
				Logger.Debug($"Adding {reviewers.Count} reviewer(s) to PR");
			}

			var created = await _gitClient.CreatePullRequestAsync(pr, repository.Id);
			Logger.Info($"Created PR #{created.PullRequestId}: '{title}' in {repository.Name}");

			return created;
		}

		/// <summary>
		/// Checks whether any open pull requests exist in the repository whose source branch
		/// starts with the specified prefix (e.g., "nugroom/").
		/// </summary>
		/// <param name="repository">The repository to search.</param>
		/// <param name="branchPrefix">The branch name prefix to match (without refs/heads/).</param>
		/// <returns>A list of open pull requests whose source branch matches the prefix.</returns>
		public async Task<List<GitPullRequest>> GetOpenPullRequestsByBranchPrefixAsync(
			GitRepository repository,
			string branchPrefix)
		{
			ArgumentNullException.ThrowIfNull(repository);

			if (string.IsNullOrWhiteSpace(branchPrefix))
			{
				throw new ArgumentException("Branch prefix is required.", nameof(branchPrefix));
			}

			try
			{
				var searchCriteria = new GitPullRequestSearchCriteria
				{
					Status = PullRequestStatus.Active
				};

				Logger.Debug($"Searching for open PRs in {repository.Name} with source branch prefix '{branchPrefix}'");

				var pullRequests = await _gitClient.GetPullRequestsAsync(repository.Id, searchCriteria);
				var refPrefix = $"refs/heads/{branchPrefix}";

				var matching = pullRequests
					.Where(pr => pr.SourceRefName.StartsWith(refPrefix, StringComparison.OrdinalIgnoreCase))
					.ToList();

				Logger.Debug($"Found {matching.Count} open PR(s) matching prefix '{branchPrefix}' in {repository.Name}");

				return matching;
			}
			catch (Exception ex)
			{
				Logger.Warning($"Failed to query open PRs in {repository.Name}: {ex.Message}");

				return [];
			}
		}

		/// <summary>
		/// Reads file content from a specific branch version
		/// </summary>
		/// <param name="repository">The repository.</param>
		/// <param name="filePath">Path to the file.</param>
		/// <param name="branchName">Full ref name of the branch.</param>
		/// <returns>The file content, or empty string on failure.</returns>
		public async Task<string> GetFileContentFromBranchAsync(GitRepository repository, string filePath, string branchName)
		{
			try
			{
				var versionDescriptor = new GitVersionDescriptor
				{
					VersionType = GitVersionType.Branch,
					Version = branchName.Replace("refs/heads/", "")
				};

				using var stream = await _gitClient.GetItemContentAsync(repository.Id, filePath, versionDescriptor: versionDescriptor);
				using var reader = new StreamReader(stream, Encoding.UTF8);

				return await reader.ReadToEndAsync();
			}
			catch (Exception ex)
			{
				Logger.Warning($"Could not read {filePath} from branch {branchName} in {repository.Name}: {ex.Message}");

				return string.Empty;
			}
		}

		/// <inheritdoc />
		public void Dispose()
		{
			try
			{
				Logger.Debug("Disposing Azure DevOps client");
				_connection?.Dispose();
			}
			catch (Exception ex)
			{
				Logger.Warning($"Error disposing Azure DevOps client: {ex.Message}");
			}
		}
	}
}