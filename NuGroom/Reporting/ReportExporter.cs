using NuGroom.Configuration;
using NuGroom.Nuget;

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGroom.Reporting
{
	/// <summary>
	/// Exports scan results (per project) to JSON or CSV.
	/// </summary>
	public static class ReportExporter
	{
		private record PackageReportItem(
			string Repository,
			string ProjectPath,
			string PackageName,
			string? Version,
			string? LatestVersion,
			string Feed,
			bool Deprecated,
			bool Outdated,
			bool Vulnerable,
			string Status,
			string? SourceProjectRepository,
			string? SourceProjectName);

		/// <summary>
		/// Export to JSON (human-readable pretty format) with optional version warnings
		/// </summary>
		public static void ExportPackageReferencesJson(
			IEnumerable<PackageReferenceExtractor.PackageReference> references,
			string path,
			List<VersionWarning>? warnings = null,
			List<PackageRecommendation>? recommendations = null)
		{
			var items = BuildItems(references).ToList();
			var options = new JsonSerializerOptions
			{
				WriteIndented = true,
				DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
			};
			var dto = new
			{
				generatedUtc = DateTime.UtcNow,
				projectCount = items.Select(i => i.ProjectPath).Distinct().Count(),
				packageCount = items.Count,
				packages = items,
				versionWarnings = warnings != null ? new
				{
					totalWarnings = warnings.Count,
					packagesWithWarnings = warnings.Select(w => w.PackageName).Distinct().Count(),
					warnings = warnings.Select(w => new
					{
						packageName = w.PackageName,
						repository = w.Repository,
						projectPath = w.ProjectPath,
						currentVersion = w.CurrentVersion,
						referenceVersion = w.ReferenceVersion,
						warningType = w.WarningType,
						level = w.Level.ToString(),
						description = w.Description
					}).ToList()
				} : null,
				recommendations = recommendations != null ? new
				{
					totalRecommendations = recommendations.Count,
					packagesNeedingUpdate = recommendations.Select(r => r.PackageName).Distinct().Count(),
					projectsAffected = recommendations.Select(r => new { r.Repository, r.ProjectPath }).Distinct().Count(),
					updates = recommendations.Select(r => new
					{
						packageName = r.PackageName,
						repository = r.Repository,
						projectPath = r.ProjectPath,
						currentVersion = r.CurrentVersion,
						recommendedVersion = r.RecommendedVersion,
						recommendationType = r.RecommendationType,
						reason = r.Reason
					}).ToList()
				} : null
			};
			var json = JsonSerializer.Serialize(dto, options);
			File.WriteAllText(path, json, Encoding.UTF8);
		}

		/// <summary>
		/// Export to CSV (RFC4180 compatible)
		/// </summary>
		public static void ExportPackageReferencesCsv(IEnumerable<PackageReferenceExtractor.PackageReference> references, string path)
		{
			var items = BuildItems(references).ToList();
			var sb = new StringBuilder();
			sb.AppendLine("Repository,ProjectPath,PackageName,Version,LatestVersion,Feed,Deprecated,Outdated,Vulnerable,Status,SourceRepository,SourceProject");
			foreach (var i in items)
			{
				sb.AppendLine(string.Join(',', new[]
				{
					Csv(i.Repository),
					Csv(i.ProjectPath),
					Csv(i.PackageName),
					Csv(i.Version),
					Csv(i.LatestVersion),
					Csv(i.Feed),
					Csv(i.Deprecated.ToString()),
					Csv(i.Outdated.ToString()),
					Csv(i.Vulnerable.ToString()),
					Csv(i.Status),
					Csv(i.SourceProjectRepository),
					Csv(i.SourceProjectName)
				}));
			}
			File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
		}

		/// <summary>
		/// Export version warnings to separate CSV file
		/// </summary>
		public static void ExportVersionWarningsCsv(List<VersionWarning> warnings, string path)
		{
			var sb = new StringBuilder();
			sb.AppendLine("PackageName,Repository,ProjectPath,CurrentVersion,ReferenceVersion,WarningType,Level,Description");
			foreach (var w in warnings)
			{
				sb.AppendLine(string.Join(',', new[]
				{
					Csv(w.PackageName),
					Csv(w.Repository),
					Csv(w.ProjectPath),
					Csv(w.CurrentVersion),
					Csv(w.ReferenceVersion),
					Csv(w.WarningType),
					Csv(w.Level.ToString()),
					Csv(w.Description)
				}));
			}
			File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
		}

		/// <summary>
		/// Export recommendations to separate CSV file
		/// </summary>
		public static void ExportRecommendationsCsv(List<PackageRecommendation> recommendations, string path)
		{
			var sb = new StringBuilder();
			sb.AppendLine("PackageName,Repository,ProjectPath,CurrentVersion,RecommendedVersion,RecommendationType,Reason");
			foreach (var r in recommendations)
			{
				sb.AppendLine(string.Join(',', new[]
				{
					Csv(r.PackageName),
					Csv(r.Repository),
					Csv(r.ProjectPath),
					Csv(r.CurrentVersion),
					Csv(r.RecommendedVersion),
					Csv(r.RecommendationType),
					Csv(r.Reason)
				}));
			}
			File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
		}

		/// <summary>
		/// Export version warnings to a standalone JSON file
		/// </summary>
		public static void ExportWarningsJson(List<VersionWarning> warnings, string path)
		{
			var options = new JsonSerializerOptions
			{
				WriteIndented = true,
				DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
			};
			var dto = new
			{
				generatedUtc = DateTime.UtcNow,
				totalWarnings = warnings.Count,
				packagesWithWarnings = warnings.Select(w => w.PackageName).Distinct().Count(),
				warnings = warnings.Select(w => new
				{
					packageName = w.PackageName,
					repository = w.Repository,
					projectPath = w.ProjectPath,
					currentVersion = w.CurrentVersion,
					referenceVersion = w.ReferenceVersion,
					warningType = w.WarningType,
					level = w.Level.ToString(),
					description = w.Description
				}).ToList()
			};
			var json = JsonSerializer.Serialize(dto, options);
			File.WriteAllText(path, json, Encoding.UTF8);
		}

		/// <summary>
		/// Export recommendations to a standalone JSON file
		/// </summary>
		public static void ExportRecommendationsJson(List<PackageRecommendation> recommendations, string path)
		{
			var options = new JsonSerializerOptions
			{
				WriteIndented = true,
				DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
			};
			var dto = new
			{
				generatedUtc = DateTime.UtcNow,
				totalRecommendations = recommendations.Count,
				packagesNeedingUpdate = recommendations.Select(r => r.PackageName).Distinct().Count(),
				projectsAffected = recommendations.Select(r => new { r.Repository, r.ProjectPath }).Distinct().Count(),
				updates = recommendations.Select(r => new
				{
					packageName = r.PackageName,
					repository = r.Repository,
					projectPath = r.ProjectPath,
					currentVersion = r.CurrentVersion,
					recommendedVersion = r.RecommendedVersion,
					recommendationType = r.RecommendationType,
					reason = r.Reason
				}).ToList()
			};
			var json = JsonSerializer.Serialize(dto, options);
			File.WriteAllText(path, json, Encoding.UTF8);
		}

		/// <summary>
		/// Prints package update recommendations to console
		/// </summary>
		public static void PrintRecommendations(List<PackageRecommendation> recommendations)
		{
			var w = ConsoleWriter.Out;

			if (!recommendations.Any())
			{
				w.WriteLineColored(ConsoleColor.Green, "No package updates recommended. All packages are up to date!");
				return;
			}

			w.WriteLine("\n" + new string('=', 80))
			 .WriteLine("PACKAGE UPDATE RECOMMENDATIONS")
			 .WriteLine(new string('=', 80))
			 .WriteLine("The following projects should update their package versions:")
			 .WriteLine();

			// Group by package for better readability
			var grouped = recommendations.GroupBy(r => r.PackageName).OrderBy(g => g.Key);

			foreach (var group in grouped)
			{
				w.WriteLineColored(ConsoleColor.Cyan, $"\n{group.Key}:");

				var latestRecommended = group.Select(r => r.RecommendedVersion).Distinct().FirstOrDefault();

				if (latestRecommended != null)
				{
					w.WriteLineColored(ConsoleColor.Green, $"  Recommended version: {latestRecommended}");
				}

				w.WriteLine();

				foreach (var rec in group.OrderBy(r => r.Repository).ThenBy(r => r.ProjectPath))
				{
					w.WriteColored(ConsoleColor.White, "  • ")
					 .WriteLine($"{rec.Repository}/{Path.GetFileName(rec.ProjectPath)}")
					 .Gray().Write("    Current: ")
					 .Yellow().Write(rec.CurrentVersion)
					 .Gray().Write(" ? Upgrade to: ")
					 .Green().WriteLine(rec.RecommendedVersion)
					 .ResetColor()
					 .WriteLineColored(ConsoleColor.DarkGray, $"    {rec.Reason}");
				}
			}

			// Print summary
			var stats = GetRecommendationStats(recommendations);

			w.WriteLine("\n" + new string('-', 80))
			 .WriteLine($"Total update recommendations: {stats.TotalRecommendations}")
			 .WriteLine($"Packages needing update: {stats.PackagesNeedingUpdate}")
			 .WriteLine($"Projects affected: {stats.ProjectsAffected}")
			 .WriteLine(new string('-', 80));
		}

		private static (int TotalRecommendations, int PackagesNeedingUpdate, int ProjectsAffected)
			GetRecommendationStats(List<PackageRecommendation> recommendations)
		{
			var packagesNeedingUpdate = recommendations.Select(r => r.PackageName).Distinct().Count();
			var projectsAffected = recommendations.Select(r => new { r.Repository, r.ProjectPath })
												 .Distinct()
												 .Count();

			return (recommendations.Count, packagesNeedingUpdate, projectsAffected);
		}

		/// <summary>
		/// Prints version warnings to console
		/// </summary>
		public static void PrintVersionWarnings(List<VersionWarning> warnings, Dictionary<string, string?>? pinnedPackages = null)
		{
			var w = ConsoleWriter.Out;

			// Exclude pinned packages unless the warning is a pinned-version-mismatch
			var filtered = pinnedPackages is { Count: > 0 }
				? warnings.Where(wr => !pinnedPackages.ContainsKey(wr.PackageName) || wr.WarningType == "pinned-version-mismatch").ToList()
				: warnings;

			if (!filtered.Any())
			{
				w.WriteLineColored(ConsoleColor.Green, "No version warnings detected.");
				return;
			}

			w.WriteLine("\n" + new string('=', 80))
			 .WriteLine("VERSION WARNINGS")
			 .WriteLine(new string('=', 80));

			// Group by package for better readability
			var grouped = filtered.GroupBy(wr => wr.PackageName).OrderBy(g => g.Key);

			foreach (var group in grouped)
			{
				w.WriteLineColored(ConsoleColor.Yellow, $"\n{group.Key}:");

				foreach (var warning in group.OrderBy(wr => wr.Repository).ThenBy(wr => wr.ProjectPath))
				{
					var levelColor = warning.Level switch
					{
						VersionWarningLevel.Major => ConsoleColor.Red,
						VersionWarningLevel.Minor => ConsoleColor.Yellow,
						VersionWarningLevel.Patch => ConsoleColor.Cyan,
						_ => ConsoleColor.Gray
					};

					var warningIcon = warning.WarningType == "version-mismatch-available" ? "\u26A0" : "\u2139";

					w.WriteColored(levelColor, $"  {warningIcon} ")
					 .WriteLine($"{warning.Repository}/{warning.ProjectPath}")
					 .WriteLineColored(ConsoleColor.Gray, $"    {warning.Description}");
				}
			}

			// Print summary
			var stats = GetWarningStats(filtered.ToList());

			w.WriteLine("\n" + new string('-', 80))
			 .WriteLine($"Total warnings: {stats.TotalWarnings}")
			 .WriteLine($"Packages with warnings: {stats.PackagesWithWarnings}");

			if (stats.MajorWarnings > 0)
			{
				w.WriteLineColored(ConsoleColor.Red, $"Major version differences: {stats.MajorWarnings}");
			}

			if (stats.MinorWarnings > 0)
			{
				w.WriteLineColored(ConsoleColor.Yellow, $"Minor version differences: {stats.MinorWarnings}");
			}

			if (stats.PatchWarnings > 0)
			{
				w.WriteLineColored(ConsoleColor.Cyan, $"Patch version differences: {stats.PatchWarnings}");
			}

			w.WriteLine(new string('-', 80));
		}

		private static (int TotalWarnings, int PackagesWithWarnings, int MajorWarnings, int MinorWarnings, int PatchWarnings)
			GetWarningStats(List<VersionWarning> warnings)
		{
			var packagesWithWarnings = warnings.Select(w => w.PackageName).Distinct().Count();
			var majorWarnings = warnings.Count(w =>
				NuGetPackageResolver.GetVersionDifference(w.CurrentVersion, w.ReferenceVersion) == "major");
			var minorWarnings = warnings.Count(w =>
				NuGetPackageResolver.GetVersionDifference(w.CurrentVersion, w.ReferenceVersion) == "minor");
			var patchWarnings = warnings.Count(w =>
				NuGetPackageResolver.GetVersionDifference(w.CurrentVersion, w.ReferenceVersion) == "patch");

			return (warnings.Count, packagesWithWarnings, majorWarnings, minorWarnings, patchWarnings);
		}

		private static IEnumerable<PackageReportItem> BuildItems(IEnumerable<PackageReferenceExtractor.PackageReference> references)
		{
			foreach (var r in references)
			{
				var info = r.NuGetInfo;
				var feed = info?.FeedName ?? (info?.ExistsOnNuGetOrg == true ? "NuGet.org" : "(none)");
				var statusParts = new List<string>();
				if (info != null)
				{
					if (info.IsDeprecated) statusParts.Add("deprecated");
					if (info.IsOutdated) statusParts.Add("outdated");
					if (info.IsVulnerable) statusParts.Add("vulnerable");
				}
				var status = statusParts.Any() ? string.Join(";", statusParts) : "ok";
				var srcProj = info?.SourceProjects.FirstOrDefault();
				yield return new PackageReportItem(
					Repository: r.RepositoryName,
					ProjectPath: r.ProjectPath,
					PackageName: r.PackageName,
					Version: r.Version,
					LatestVersion: info?.LatestVersion,
					Feed: feed ?? "(unknown)",
					Deprecated: info?.IsDeprecated == true,
					Outdated: info?.IsOutdated == true,
					Vulnerable: info?.IsVulnerable == true,
					Status: status,
					SourceProjectRepository: srcProj?.RepositoryName,
					SourceProjectName: srcProj?.ProjectName);
			}
		}

		private static string Csv(string? value)
		{
			value ??= string.Empty;
			if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
			{
				value = '"' + value.Replace("\"", "\"\"") + '"';
			}
			return value;
		}
	}
}
