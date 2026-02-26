using NuGroom.Configuration;

using System.Text.RegularExpressions;

namespace NuGroom
{
	/// <summary>
	/// Increments version properties (Version, AssemblyVersion, FileVersion) in .csproj file content
	/// based on the configured <see cref="VersionIncrementScope"/>
	/// </summary>
	internal static partial class ProjectVersionIncrementer
	{
		/// <summary>
		/// Applies version increments to the specified .csproj content based on the given configuration.
		/// Only modifies properties that already exist in the file.
		/// </summary>
		/// <param name="csprojContent">The raw .csproj file content.</param>
		/// <param name="config">Configuration specifying which properties to increment and the scope.</param>
		/// <returns>The modified .csproj content with incremented versions.</returns>
		public static string ApplyVersionIncrements(string csprojContent, VersionIncrementConfig config)
		{
			ArgumentNullException.ThrowIfNull(csprojContent);
			ArgumentNullException.ThrowIfNull(config);

			if (!config.IsEnabled)
			{
				return csprojContent;
			}

			var result = csprojContent;

			if (config.IncrementVersion)
			{
				result = IncrementProperty(result, "Version", config.Scope);
			}

			if (config.IncrementAssemblyVersion)
			{
				result = IncrementProperty(result, "AssemblyVersion", config.Scope);
			}

			if (config.IncrementFileVersion)
			{
				result = IncrementProperty(result, "FileVersion", config.Scope);
			}

			return result;
		}

		/// <summary>
		/// Increments a single MSBuild property element value in place.
		/// Supports both 3-part (Major.Minor.Patch) and 4-part (Major.Minor.Build.Revision) version formats.
		/// If the property is not found or the version cannot be parsed, the content is returned unchanged.
		/// </summary>
		/// <param name="content">The .csproj file content.</param>
		/// <param name="propertyName">The MSBuild property name (e.g., "Version", "AssemblyVersion").</param>
		/// <param name="scope">The version component to increment.</param>
		/// <returns>The modified content with the incremented property value.</returns>
		internal static string IncrementProperty(string content, string propertyName, VersionIncrementScope scope)
		{
			var pattern = $@"(<{Regex.Escape(propertyName)}>)([\d]+(?:\.[\d]+){{2,3}})(</{Regex.Escape(propertyName)}>)";

			return Regex.Replace(content, pattern, match =>
			{
				var prefix = match.Groups[1].Value;
				var versionStr = match.Groups[2].Value;
				var suffix = match.Groups[3].Value;

				var incremented = IncrementVersion(versionStr, scope);

				if (incremented == null)
				{
					return match.Value;
				}

				return $"{prefix}{incremented}{suffix}";
			});
		}

		/// <summary>
		/// Increments a version string according to the specified scope.
		/// Supports 3-part (Major.Minor.Patch) and 4-part (Major.Minor.Build.Revision) formats.
		/// </summary>
		/// <param name="version">The version string to increment.</param>
		/// <param name="scope">The component to increment.</param>
		/// <returns>The incremented version string, or <c>null</c> if the version cannot be parsed.</returns>
		internal static string? IncrementVersion(string version, VersionIncrementScope scope)
		{
			if (string.IsNullOrWhiteSpace(version))
			{
				return null;
			}

			var parts = version.Split('.');

			if (parts.Length < 3 || parts.Length > 4)
			{
				return null;
			}

			var segments = new int[parts.Length];

			for (int i = 0; i < parts.Length; i++)
			{
				if (!int.TryParse(parts[i], out segments[i]))
				{
					return null;
				}
			}

			switch (scope)
			{
				case VersionIncrementScope.Major:
					segments[0]++;
					segments[1] = 0;
					segments[2] = 0;

					if (segments.Length == 4)
					{
						segments[3] = 0;
					}

					break;

				case VersionIncrementScope.Minor:
					segments[1]++;
					segments[2] = 0;

					if (segments.Length == 4)
					{
						segments[3] = 0;
					}

					break;

				case VersionIncrementScope.Patch:
					segments[2]++;

					if (segments.Length == 4)
					{
						segments[3] = 0;
					}

					break;

				default:
					return null;
			}

			return string.Join('.', segments);
		}
	}
}
