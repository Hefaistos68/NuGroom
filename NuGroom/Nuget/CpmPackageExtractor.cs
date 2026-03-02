using System.Xml;

namespace NuGroom.Nuget
{
	/// <summary>
	/// Parses <c>Directory.Packages.props</c> files to extract centrally managed
	/// package versions defined via <c>&lt;PackageVersion&gt;</c> elements.
	/// </summary>
	internal static class CpmPackageExtractor
	{
		/// <summary>
		/// Result of parsing a <c>Directory.Packages.props</c> file.
		/// </summary>
		/// <param name="ManagePackageVersionsCentrally">
		/// Whether the <c>&lt;ManagePackageVersionsCentrally&gt;</c> property is <c>true</c>.
		/// </param>
		/// <param name="PackageVersions">
		/// Dictionary mapping package name (case-insensitive) to its centrally defined version.
		/// </param>
		/// <param name="FilePath">
		/// Repository-relative path of the <c>Directory.Packages.props</c> file that was parsed.
		/// </param>
		internal record CpmParseResult(
			bool ManagePackageVersionsCentrally,
			Dictionary<string, string> PackageVersions,
			string? FilePath = null);

		/// <summary>
		/// Parses the content of a <c>Directory.Packages.props</c> file and returns
		/// the set of centrally defined package versions.
		/// </summary>
		/// <param name="xmlContent">Raw XML content of the <c>Directory.Packages.props</c> file.</param>
		/// <returns>
		/// A <see cref="CpmParseResult"/> containing the CPM flag and all <c>PackageVersion</c> entries.
		/// Returns an empty result when content is null/whitespace or XML parsing fails.
		/// </returns>
		public static CpmParseResult Parse(string? xmlContent)
		{
			var empty = new CpmParseResult(false, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

			if (string.IsNullOrWhiteSpace(xmlContent))
			{
				return empty;
			}

			try
			{
				var doc = new XmlDocument();
				doc.LoadXml(xmlContent);

				var isCpm = DetectCpmFlag(doc);
				var versions = ExtractPackageVersions(doc);

				Logger.Debug($"CPM parse: ManagePackageVersionsCentrally={isCpm}, {versions.Count} PackageVersion entries");

				return new CpmParseResult(isCpm, versions);
			}
			catch (XmlException ex)
			{
				Logger.Warning($"Failed to parse Directory.Packages.props: {ex.Message}");

				return empty;
			}
		}

		/// <summary>
		/// Merges centrally managed versions into project-level package references
		/// that have no inline <c>Version</c> attribute. References that already
		/// carry a <c>Version</c> (i.e. <c>VersionOverride</c>) are left untouched.
		/// </summary>
		/// <param name="projectReferences">
		/// Package references extracted from a project file (may have null <c>Version</c>
		/// when CPM manages the version).
		/// </param>
		/// <param name="cpmVersions">
		/// Centrally managed versions keyed by package name (case-insensitive).
		/// </param>
		/// <param name="cpmFilePath">
		/// Repository-relative path of the <c>Directory.Packages.props</c> file.
		/// Stored on enriched references so the update plan can target the correct file.
		/// </param>
		/// <returns>
		/// A new list where version-less references are enriched from the CPM lookup
		/// and their <see cref="PackageSourceKind"/> is set to
		/// <see cref="PackageSourceKind.CentralPackageManagement"/>.
		/// </returns>
		public static List<PackageReferenceExtractor.PackageReference> MergeCpmVersions(
			List<PackageReferenceExtractor.PackageReference> projectReferences,
			Dictionary<string, string> cpmVersions,
			string? cpmFilePath = null)
		{
			if (cpmVersions.Count == 0)
			{
				return projectReferences;
			}

			var result = new List<PackageReferenceExtractor.PackageReference>(projectReferences.Count);

			foreach (var pr in projectReferences)
			{
				if (pr.Version != null)
				{
					// Project specifies VersionOverride — keep as-is
					result.Add(pr);
					continue;
				}

				if (cpmVersions.TryGetValue(pr.PackageName, out var centralVersion))
				{
					result.Add(pr with
					{
						Version = centralVersion,
						SourceKind = PackageSourceKind.CentralPackageManagement,
						CpmFilePath = cpmFilePath
					});
				}
				else
				{
					// No CPM version found — keep original (version stays null)
					result.Add(pr);
				}
			}

			return result;
		}

		/// <summary>
		/// Detects whether <c>&lt;ManagePackageVersionsCentrally&gt;true&lt;/...&gt;</c>
		/// is present in any <c>PropertyGroup</c>.
		/// </summary>
		private static bool DetectCpmFlag(XmlDocument doc)
		{
			var nodes = doc.SelectNodes("//*[local-name()='ManagePackageVersionsCentrally']");

			if (nodes == null)
			{
				return false;
			}

			foreach (XmlNode node in nodes)
			{
				if (string.Equals(node.InnerText?.Trim(), "true", StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Extracts all MSBuild properties from <c>&lt;PropertyGroup&gt;</c> elements.
		/// These properties can be referenced via <c>$(PropertyName)</c> syntax in package versions.
		/// </summary>
		private static Dictionary<string, string> ExtractProperties(XmlDocument doc)
		{
			var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			var propertyGroups = doc.SelectNodes("//*[local-name()='PropertyGroup']");

			if (propertyGroups == null)
			{
				return properties;
			}

			foreach (XmlNode propertyGroup in propertyGroups)
			{
				if (propertyGroup.ChildNodes == null)
				{
					continue;
				}

				foreach (XmlNode child in propertyGroup.ChildNodes)
				{
					if (child.NodeType == XmlNodeType.Element && !string.IsNullOrWhiteSpace(child.InnerText))
					{
						properties[child.LocalName] = child.InnerText.Trim();
					}
				}
			}

			return properties;
		}

		/// <summary>
		/// Resolves MSBuild property variables in the format <c>$(PropertyName)</c>.
		/// Supports nested property references and handles undefined variables by returning the original text.
		/// </summary>
		/// <param name="value">The value that may contain property variable references.</param>
		/// <param name="properties">Dictionary of available MSBuild properties.</param>
		/// <returns>The value with all property references resolved.</returns>
		private static string ResolveVariables(string value, Dictionary<string, string> properties)
		{
			if (string.IsNullOrWhiteSpace(value) || !value.Contains('$'))
			{
				return value;
			}

			var result = value;
			var maxIterations = 10; // Prevent infinite loops in circular references
			var iteration = 0;

			while (result.Contains("$(") && iteration < maxIterations)
			{
				var startIndex = result.IndexOf("$(", StringComparison.Ordinal);

				if (startIndex == -1)
				{
					break;
				}

				var endIndex = result.IndexOf(')', startIndex);

				if (endIndex == -1)
				{
					break;
				}

				var propertyName = result.Substring(startIndex + 2, endIndex - startIndex - 2);

				if (properties.TryGetValue(propertyName, out var propertyValue))
				{
					result = result.Substring(0, startIndex) + propertyValue + result.Substring(endIndex + 1);
				}
				else
				{
					// Property not found - leave as-is and move past it
					break;
				}

				iteration++;
			}

			return result;
		}

		/// <summary>
		/// Extracts all <c>&lt;PackageVersion Include="..." Version="..."/&gt;</c> entries.
		/// </summary>
		private static Dictionary<string, string> ExtractPackageVersions(XmlDocument doc)
		{
			var properties = ExtractProperties(doc);
			var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			var nodes = doc.SelectNodes("//*[local-name()='PackageVersion' and @Include]");

			if (nodes == null)
			{
				return versions;
			}

			foreach (XmlNode node in nodes)
			{
				var name = node.Attributes?["Include"]?.Value;
				var version = node.Attributes?["Version"]?.Value;

				if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(version))
				{
					var resolvedVersion = ResolveVariables(version, properties);
					versions[name] = resolvedVersion;
				}
			}

			return versions;
		}
	}
}
