using System.Xml;

namespace NuGroom.Nuget
{
	/// <summary>
	/// Parses legacy <c>packages.config</c> files to extract
	/// <c>&lt;package id="..." version="..."/&gt;</c> entries.
	/// </summary>
	internal static class PackagesConfigExtractor
	{
		/// <summary>
		/// Extracts package references from the content of a <c>packages.config</c> file.
		/// Each entry is associated with <paramref name="projectPath"/> so it can be
		/// correlated with the co-located project file.
		/// </summary>
		/// <param name="xmlContent">Raw XML content of the <c>packages.config</c> file.</param>
		/// <param name="repositoryName">Repository label for provenance.</param>
		/// <param name="projectPath">
		/// Path to the project file that lives alongside this <c>packages.config</c>.
		/// Used as the <see cref="PackageReferenceExtractor.PackageReference.ProjectPath"/>.
		/// </param>
		/// <param name="projectName">Logical project name.</param>
		/// <param name="exclusionList">Optional exclusion list to filter results.</param>
		/// <returns>
		/// List of <see cref="PackageReferenceExtractor.PackageReference"/> entries with
		/// <see cref="PackageSourceKind.PackagesConfig"/>.
		/// Returns an empty list when content is null/whitespace or XML parsing fails.
		/// </returns>
		public static List<PackageReferenceExtractor.PackageReference> Extract(
			string? xmlContent,
			string repositoryName,
			string projectPath,
			string projectName,
			PackageReferenceExtractor.ExclusionList? exclusionList = null)
		{
			var references = new List<PackageReferenceExtractor.PackageReference>();

			if (string.IsNullOrWhiteSpace(xmlContent))
			{
				return references;
			}

			try
			{
				var doc = new XmlDocument();
				doc.LoadXml(xmlContent);

				var nodes = doc.SelectNodes("//package[@id]");

				if (nodes == null)
				{
					return references;
				}

				int lineEstimate = 1;

				foreach (XmlNode node in nodes)
				{
					var id = node.Attributes?["id"]?.Value;
					var version = node.Attributes?["version"]?.Value;

					if (string.IsNullOrWhiteSpace(id))
					{
						continue;
					}

					if (exclusionList != null && exclusionList.ShouldExclude(id))
					{
						continue;
					}

					references.Add(new PackageReferenceExtractor.PackageReference(
						PackageName: id,
						Version: version,
						ProjectPath: projectPath,
						RepositoryName: repositoryName,
						ProjectName: projectName,
						LineNumber: lineEstimate++,
						SourceKind: PackageSourceKind.PackagesConfig));

					Logger.Debug($"packages.config: found {id} v{version ?? "unspecified"}");
				}

				Logger.Debug($"Extracted {references.Count} package(s) from packages.config for {projectPath}");
			}
			catch (XmlException ex)
			{
				Logger.Warning($"Failed to parse packages.config for {projectPath}: {ex.Message}");
			}

			return references;
		}

		/// <summary>
		/// Infers the co-located project file path for a given <c>packages.config</c> path.
		/// The project file is assumed to live in the same directory.
		/// Returns <c>null</c> when no matching project file can be determined.
		/// </summary>
		/// <param name="packagesConfigPath">Path to the <c>packages.config</c> file (e.g. <c>/src/App/packages.config</c>).</param>
		/// <param name="projectFilePaths">All known project file paths in the repository.</param>
		/// <returns>
		/// The path of the first project file that shares the same directory,
		/// or <c>null</c> if no match is found.
		/// </returns>
		public static string? FindColocatedProjectFile(string packagesConfigPath, IEnumerable<string> projectFilePaths)
		{
			var directory = Path.GetDirectoryName(packagesConfigPath)?.Replace('\\', '/');

			if (string.IsNullOrEmpty(directory))
			{
				return null;
			}

			return projectFilePaths.FirstOrDefault(p =>
			{
				var projDir = Path.GetDirectoryName(p)?.Replace('\\', '/');

				return string.Equals(projDir, directory, StringComparison.OrdinalIgnoreCase);
			});
		}
	}
}
