using NuGroom.Configuration;
using NuGroom.Nuget;
using NuGroom.Reporting;

namespace NuGroom.Workflows
{
	/// <summary>
	/// Orchestrates version-warning analysis and report exports
	/// after a scan has completed.
	/// </summary>
	internal static class ReportWorkflow
	{
		/// <summary>
		/// Analyzes version warnings, generates recommendations, and exports reports
		/// based on the scan results and the parsed configuration.
		/// </summary>
		/// <param name="references">Package references produced by the scan.</param>
		/// <param name="parseResult">Parsed command line / config options controlling export behaviour.</param>
		public static void Execute(
			List<PackageReferenceExtractor.PackageReference> references,
			ParseResult parseResult)
		{
			List<VersionWarning>? warnings = null;
			List<PackageRecommendation>? recommendations = null;

			if (parseResult.VersionWarningConfig != null)
			{
				var pinnedLookup = BuildPinnedLookup(parseResult.UpdateConfig?.PinnedPackages);
				var analyzer = new VersionWarningAnalyzer(parseResult.VersionWarningConfig, pinnedLookup);
				warnings = analyzer.AnalyzeVersionWarnings(references);
				recommendations = analyzer.GenerateRecommendations(references);

				ReportExporter.PrintVersionWarnings(warnings, pinnedLookup);
				ReportExporter.PrintRecommendations(recommendations);

				// Auto-generated sibling CSV files when package export uses CSV format
				if (parseResult.ExportFormat == ExportFormat.Csv)
				{
					ExportWarningsCsv(warnings, parseResult.ExportPackagesPath);
					ExportRecommendationsCsv(recommendations, parseResult.ExportPackagesPath);
				}
			}

			ExportPackages(references, parseResult.ExportPackagesPath, parseResult.ExportFormat, warnings, recommendations);

			// Granular exports (warnings / recommendations) using configured format
			ExportWarnings(warnings, parseResult.ExportWarningsPath, parseResult.ExportFormat);
			ExportRecommendations(recommendations, parseResult.ExportRecommendationsPath, parseResult.ExportFormat);

			// SPDX 3.0.0 SBOM export (always JSON-LD, independent of ExportFormat)
			ExportSbom(references, parseResult.ExportSbomPath);
		}

		/// <summary>
		/// Builds a pinned package lookup dictionary from the configuration list.
		/// </summary>
		private static Dictionary<string, string?> BuildPinnedLookup(List<PinnedPackage>? pinnedPackages)
		{
			var lookup = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

			if (pinnedPackages == null || pinnedPackages.Count == 0)
			{
				return lookup;
			}

			foreach (var p in pinnedPackages)
			{
				lookup[p.PackageName] = p.Version;
			}

			return lookup;
		}

		/// <summary>
		/// Exports version warnings to a separate CSV file if the main package export path is configured
		/// </summary>
		private static void ExportWarningsCsv(List<VersionWarning> warnings, string? exportPackagesPath)
		{
			if (string.IsNullOrWhiteSpace(exportPackagesPath) || !warnings.Any())
			{
				return;
			}

			var warningsCsvPath = Path.ChangeExtension(exportPackagesPath, null) + "-warnings.csv";
			ReportExporter.ExportVersionWarningsCsv(warnings, warningsCsvPath);
			ConsoleWriter.Out.Green().WriteLine($"Version warnings CSV exported: {warningsCsvPath}").ResetColor();
		}

		/// <summary>
		/// Exports recommendations to a separate CSV file if the main package export path is configured
		/// </summary>
		private static void ExportRecommendationsCsv(List<PackageRecommendation> recommendations, string? exportPackagesPath)
		{
			if (string.IsNullOrWhiteSpace(exportPackagesPath) || !recommendations.Any())
			{
				return;
			}

			var recommendationsCsvPath = Path.ChangeExtension(exportPackagesPath, null) + "-recommendations.csv";
			ReportExporter.ExportRecommendationsCsv(recommendations, recommendationsCsvPath);
			ConsoleWriter.Out.Green().WriteLine($"Recommendations CSV exported: {recommendationsCsvPath}").ResetColor();
		}

		/// <summary>
		/// Exports the full package reference report in the configured format
		/// </summary>
		private static void ExportPackages(
			List<PackageReferenceExtractor.PackageReference> references,
			string? exportPackagesPath,
			ExportFormat format,
			List<VersionWarning>? warnings,
			List<PackageRecommendation>? recommendations)
		{
			if (string.IsNullOrWhiteSpace(exportPackagesPath))
			{
				return;
			}

			if (format == ExportFormat.Csv)
			{
				ReportExporter.ExportPackageReferencesCsv(references, exportPackagesPath!);
			}
			else
			{
				ReportExporter.ExportPackageReferencesJson(references, exportPackagesPath!, warnings, recommendations);
			}

			ConsoleWriter.Out.Green().WriteLine($"Packages exported ({format}): {exportPackagesPath}").ResetColor();
		}

		/// <summary>
		/// Exports version warnings to a standalone file in the configured format
		/// </summary>
		private static void ExportWarnings(List<VersionWarning>? warnings, string? path, ExportFormat format)
		{
			if (string.IsNullOrWhiteSpace(path) || warnings == null || !warnings.Any())
			{
				return;
			}

			if (format == ExportFormat.Csv)
			{
				ReportExporter.ExportVersionWarningsCsv(warnings, path!);
			}
			else
			{
				ReportExporter.ExportWarningsJson(warnings, path!);
			}

			ConsoleWriter.Out.Green().WriteLine($"Warnings exported ({format}): {path}").ResetColor();
		}

		/// <summary>
		/// Exports package update recommendations to a standalone file in the configured format
		/// </summary>
		private static void ExportRecommendations(List<PackageRecommendation>? recommendations, string? path, ExportFormat format)
		{
			if (string.IsNullOrWhiteSpace(path) || recommendations == null || !recommendations.Any())
			{
				return;
			}

			if (format == ExportFormat.Csv)
			{
				ReportExporter.ExportRecommendationsCsv(recommendations, path!);
			}
			else
			{
				ReportExporter.ExportRecommendationsJson(recommendations, path!);
			}

			ConsoleWriter.Out.Green().WriteLine($"Recommendations exported ({format}): {path}").ResetColor();
		}

		/// <summary>
		/// Exports an SPDX 3.0.0 SBOM (Software Bill of Materials) in JSON-LD format
		/// </summary>
		private static void ExportSbom(List<PackageReferenceExtractor.PackageReference> references, string? path)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				return;
			}

			SpdxSbomExporter.Export(references, path!);
			ConsoleWriter.Out.Green().WriteLine($"SBOM exported (SPDX 3.0.0): {path}").ResetColor();
		}
	}
}
