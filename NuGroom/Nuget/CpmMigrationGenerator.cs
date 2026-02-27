using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace NuGroom.Nuget
{
	/// <summary>
	/// Represents a single file change produced by the CPM migration.
	/// </summary>
	/// <param name="FilePath">Repository-relative path of the file to create or update.</param>
	/// <param name="Content">New content of the file.</param>
	/// <param name="IsNew">Whether this is a new file (<c>true</c>) or an update to an existing file (<c>false</c>).</param>
	public record CpmFileChange(string FilePath, string Content, bool IsNew);

	/// <summary>
	/// Represents a version conflict warning emitted during CPM migration.
	/// </summary>
	/// <param name="PackageName">The package with conflicting versions.</param>
	/// <param name="ProjectPath">The project that will receive a <c>VersionOverride</c>.</param>
	/// <param name="OverrideVersion">The lower version kept via <c>VersionOverride</c>.</param>
	/// <param name="CentralVersion">The higher version used in <c>Directory.Packages.props</c>.</param>
	public record CpmVersionConflict(string PackageName, string ProjectPath, string OverrideVersion, string CentralVersion);

	/// <summary>
	/// Result of a CPM migration for a single repository.
	/// </summary>
	/// <param name="FileChanges">All file creations and modifications.</param>
	/// <param name="Conflicts">Version conflict warnings.</param>
	public record CpmMigrationResult(List<CpmFileChange> FileChanges, List<CpmVersionConflict> Conflicts);

	/// <summary>
	/// Generates Central Package Management migration output from scanned package references.
	/// Creates <c>Directory.Packages.props</c> files and modifies project files to remove
	/// explicit version attributes, adding <c>VersionOverride</c> where version conflicts exist.
	/// </summary>
	internal static class CpmMigrationGenerator
	{
		/// <summary>
		/// Regex to match a <c>PackageReference</c> element with a <c>Version</c> attribute on the same line.
		/// </summary>
		private static readonly Regex PackageReferenceVersionRegex = new(
			@"(<PackageReference\s+Include\s*=\s*""[^""]+"")\s+Version\s*=\s*""([^""]+)""",
			RegexOptions.IgnoreCase | RegexOptions.Compiled);

		/// <summary>
		/// Migrates a set of package references to Central Package Management.
		/// Produces one <c>Directory.Packages.props</c> per repository (or per project when
		/// <paramref name="perProject"/> is <c>true</c>) and modified project files with
		/// version attributes removed.
		/// </summary>
		/// <param name="references">All package references from the scan.</param>
		/// <param name="projectContents">
		/// Dictionary mapping project path to its XML content, keyed by repository-relative path.
		/// </param>
		/// <param name="perProject">
		/// When <c>true</c>, creates a <c>Directory.Packages.props</c> alongside each project
		/// instead of a single file at the repository root.
		/// </param>
		/// <returns>A <see cref="CpmMigrationResult"/> containing file changes and conflict warnings.</returns>
		public static CpmMigrationResult Migrate(
			List<PackageReferenceExtractor.PackageReference> references,
			Dictionary<string, string> projectContents,
			bool perProject)
		{
			ArgumentNullException.ThrowIfNull(references);
			ArgumentNullException.ThrowIfNull(projectContents);

			// Filter to only project-file references that have explicit versions
			var eligibleRefs = references
				.Where(r => r.SourceKind == PackageSourceKind.ProjectFile && r.Version != null)
				.ToList();

			if (eligibleRefs.Count == 0)
			{
				return new CpmMigrationResult(new List<CpmFileChange>(), new List<CpmVersionConflict>());
			}

			if (perProject)
			{
				return MigratePerProject(eligibleRefs, projectContents);
			}

			return MigratePerRepository(eligibleRefs, projectContents);
		}

		/// <summary>
		/// Generates the XML content for a <c>Directory.Packages.props</c> file.
		/// </summary>
		/// <param name="packageVersions">Package names mapped to their central version strings.</param>
		/// <returns>Well-formed XML string.</returns>
		public static string GenerateDirectoryPackagesProps(SortedDictionary<string, string> packageVersions)
		{
			ArgumentNullException.ThrowIfNull(packageVersions);

			var sb = new StringBuilder();
			sb.AppendLine("<Project>");
			sb.AppendLine("  <PropertyGroup>");
			sb.AppendLine("    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>");
			sb.AppendLine("  </PropertyGroup>");
			sb.AppendLine("  <ItemGroup>");

			foreach (var kvp in packageVersions)
			{
				sb.AppendLine($"    <PackageVersion Include=\"{kvp.Key}\" Version=\"{kvp.Value}\" />");
			}

			sb.AppendLine("  </ItemGroup>");
			sb.AppendLine("</Project>");

			return sb.ToString();
		}

		/// <summary>
		/// Removes <c>Version</c> attributes from <c>PackageReference</c> elements in a project file,
		/// and adds <c>VersionOverride</c> attributes where version conflicts require it.
		/// </summary>
		/// <param name="projectContent">Original XML content of the project file.</param>
		/// <param name="overrides">
		/// Dictionary mapping package name to the version that should be kept as <c>VersionOverride</c>.
		/// Packages not in this dictionary have their <c>Version</c> attribute removed entirely.
		/// </param>
		/// <returns>Modified project file content.</returns>
		public static string RemoveVersionAttributes(string projectContent, Dictionary<string, string> overrides)
		{
			ArgumentNullException.ThrowIfNull(projectContent);
			ArgumentNullException.ThrowIfNull(overrides);

			var result = PackageReferenceVersionRegex.Replace(projectContent, match =>
			{
				var prefix = match.Groups[1].Value;
				var version = match.Groups[2].Value;

				// Extract package name from the Include attribute
				var includeMatch = Regex.Match(prefix, @"Include\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);

				if (!includeMatch.Success)
				{
					return match.Value;
				}

				var packageName = includeMatch.Groups[1].Value;

				if (overrides.TryGetValue(packageName, out var overrideVersion))
				{
					return $"{prefix} VersionOverride=\"{overrideVersion}\"";
				}

				// Remove Version attribute entirely
				return prefix;
			});

			return result;
		}

		/// <summary>
		/// Migrates references to a single <c>Directory.Packages.props</c> at the repository root.
		/// </summary>
		private static CpmMigrationResult MigratePerRepository(
			List<PackageReferenceExtractor.PackageReference> references,
			Dictionary<string, string> projectContents)
		{
			var fileChanges = new List<CpmFileChange>();
			var conflicts = new List<CpmVersionConflict>();

			// Group all references by package name to find the highest version
			var packageGroups = references
				.GroupBy(r => r.PackageName, StringComparer.OrdinalIgnoreCase);

			var centralVersions = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			// Per-project overrides: projectPath -> (packageName -> overrideVersion)
			var projectOverrides = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

			foreach (var group in packageGroups)
			{
				var versions = group
					.Select(r => r.Version!)
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();

				var highestVersion = GetHighestVersion(versions);
				centralVersions[group.Key] = highestVersion;

				if (versions.Count > 1)
				{
					// There are version conflicts — projects with lower versions get VersionOverride
					foreach (var reference in group)
					{
						if (!string.Equals(reference.Version, highestVersion, StringComparison.OrdinalIgnoreCase))
						{
							conflicts.Add(new CpmVersionConflict(
								reference.PackageName,
								reference.ProjectPath,
								reference.Version!,
								highestVersion));

							if (!projectOverrides.ContainsKey(reference.ProjectPath))
							{
								projectOverrides[reference.ProjectPath] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
							}

							projectOverrides[reference.ProjectPath][reference.PackageName] = reference.Version!;
						}
					}
				}
			}

			// Generate Directory.Packages.props at repository root
			var propsContent = GenerateDirectoryPackagesProps(centralVersions);
			fileChanges.Add(new CpmFileChange("Directory.Packages.props", propsContent, IsNew: true));

			// Modify each project file
			var projectPaths = references.Select(r => r.ProjectPath).Distinct(StringComparer.OrdinalIgnoreCase);

			foreach (var projectPath in projectPaths)
			{
				if (!projectContents.TryGetValue(projectPath, out var content))
				{
					continue;
				}

				var overrides = projectOverrides.TryGetValue(projectPath, out var po)
					? po
					: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

				var modifiedContent = RemoveVersionAttributes(content, overrides);
				fileChanges.Add(new CpmFileChange(projectPath, modifiedContent, IsNew: false));
			}

			return new CpmMigrationResult(fileChanges, conflicts);
		}

		/// <summary>
		/// Migrates references to a <c>Directory.Packages.props</c> alongside each project file.
		/// </summary>
		private static CpmMigrationResult MigratePerProject(
			List<PackageReferenceExtractor.PackageReference> references,
			Dictionary<string, string> projectContents)
		{
			var fileChanges = new List<CpmFileChange>();
			var conflicts = new List<CpmVersionConflict>();

			// Group references by project path
			var projectGroups = references
				.GroupBy(r => r.ProjectPath, StringComparer.OrdinalIgnoreCase);

			foreach (var projectGroup in projectGroups)
			{
				var projectPath = projectGroup.Key;
				var centralVersions = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

				foreach (var reference in projectGroup)
				{
					centralVersions[reference.PackageName] = reference.Version!;
				}

				// Generate Directory.Packages.props alongside the project file
				var propsPath = GetPerProjectPropsPath(projectPath);
				var propsContent = GenerateDirectoryPackagesProps(centralVersions);
				fileChanges.Add(new CpmFileChange(propsPath, propsContent, IsNew: true));

				// Modify the project file — no overrides needed for per-project (each project has its own versions)
				if (projectContents.TryGetValue(projectPath, out var content))
				{
					var modifiedContent = RemoveVersionAttributes(content, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
					fileChanges.Add(new CpmFileChange(projectPath, modifiedContent, IsNew: false));
				}
			}

			return new CpmMigrationResult(fileChanges, conflicts);
		}

		/// <summary>
		/// Computes the path for a per-project <c>Directory.Packages.props</c> file.
		/// </summary>
		/// <param name="projectPath">Repository-relative path to the project file.</param>
		/// <returns>Path to the <c>Directory.Packages.props</c> in the same directory.</returns>
		private static string GetPerProjectPropsPath(string projectPath)
		{
			var directory = projectPath.Replace('\\', '/');
			var lastSlash = directory.LastIndexOf('/');

			if (lastSlash >= 0)
			{
				return directory[..(lastSlash + 1)] + "Directory.Packages.props";
			}

			return "Directory.Packages.props";
		}

		/// <summary>
		/// Returns the highest version string from a list of version strings.
		/// Falls back to lexicographic comparison if NuGet version parsing fails.
		/// </summary>
		/// <param name="versions">Non-empty list of version strings.</param>
		/// <returns>The highest version string.</returns>
		private static string GetHighestVersion(List<string> versions)
		{
			if (versions.Count == 1)
			{
				return versions[0];
			}

			string highest = versions[0];

			for (int i = 1; i < versions.Count; i++)
			{
				if (CompareVersions(versions[i], highest) > 0)
				{
					highest = versions[i];
				}
			}

			return highest;
		}

		/// <summary>
		/// Compares two version strings. Returns positive if <paramref name="a"/> is higher,
		/// negative if lower, and zero if equal. Uses NuGet version parsing with a lexicographic fallback.
		/// </summary>
		private static int CompareVersions(string a, string b)
		{
			if (NuGet.Versioning.NuGetVersion.TryParse(a, out var va) &&
				NuGet.Versioning.NuGetVersion.TryParse(b, out var vb))
			{
				return va.CompareTo(vb);
			}

			return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
		}
	}
}
