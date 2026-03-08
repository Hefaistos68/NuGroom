using NuGroom.Configuration;
using NuGroom.Nuget;
using NuGroom.Reporting;

using Shouldly;

using System.Text.Json;

namespace NuGroom.Tests
{
	[TestFixture]
	public class ReportExporterTests
	{
		private string _tempDir = string.Empty;

		[SetUp]
		public void SetUp()
		{
			_tempDir = Path.Combine(Path.GetTempPath(), $"NuGroomReportExporterTests_{Guid.NewGuid():N}");
			Directory.CreateDirectory(_tempDir);
		}

		[TearDown]
		public void TearDown()
		{
			if (Directory.Exists(_tempDir))
			{
				Directory.Delete(_tempDir, recursive: true);
			}
		}

		[Test]
		public void WhenExportPackageReferencesJsonThenWritesPackagesWarningsAndRecommendations()
		{
			var path = CreatePath("packages.json");
			var packageInfo = CreatePackageInfo() with
			{
				IsDeprecated   = true,
				IsOutdated     = true,
				IsVulnerable   = true,
				FeedName       = "PrivateFeed",
				LatestVersion  = "2.0.0",
				Vulnerabilities = new List<string> { "ADV-001" },
				SourceProjects = new List<NuGetPackageResolver.ProjectReference>
				{
					new("Source.Project", "SourceRepo", "/src/Source.Project/Source.Project.csproj", 1.0)
				}
			};
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				CreatePackageReference("Newtonsoft.Json", "1.0.0", packageInfo)
			};
			var warnings = new List<VersionWarning>
			{
				new("Newtonsoft.Json", "Repo", "/src/App/App.csproj", "2.0.0", "1.0.0", "version-mismatch-available", VersionWarningLevel.Major, "Major version mismatch")
			};
			var recommendations = new List<PackageRecommendation>
			{
				new("Newtonsoft.Json", "Repo", "/src/App/App.csproj", "1.0.0", "2.0.0", "upgrade", "Update to latest version")
			};

			ReportExporter.ExportPackageReferencesJson(references, path, warnings, recommendations);

			using var document = JsonDocument.Parse(File.ReadAllText(path));
			var root = document.RootElement;
			root.GetProperty("packageCount").GetInt32().ShouldBe(1);
			root.GetProperty("packages")[0].GetProperty("Status").GetString().ShouldBe("deprecated;outdated;vulnerable");
			root.GetProperty("packages")[0].GetProperty("Feed").GetString().ShouldBe("PrivateFeed");
			root.GetProperty("packages")[0].GetProperty("SourceProjectRepository").GetString().ShouldBe("SourceRepo");
			root.GetProperty("versionWarnings").GetProperty("totalWarnings").GetInt32().ShouldBe(1);
			root.GetProperty("recommendations").GetProperty("totalRecommendations").GetInt32().ShouldBe(1);
		}

		[Test]
		public void WhenExportPackageReferencesCsvThenEscapesSpecialCharacters()
		{
			var path = CreatePath("packages.csv");
			var packageInfo = CreatePackageInfo() with
			{
				FeedName = "Feed,One",
				SourceProjects = new List<NuGetPackageResolver.ProjectReference>
				{
					new("Source \"Project\"", "Source,Repo", "/src/Source/Project.csproj", 1.0)
				}
			};
			var reference = new PackageReferenceExtractor.PackageReference(
				PackageName: "Newtonsoft.Json",
				Version: "1.0.0",
				ProjectPath: "/src/\"App\"/App.csproj",
				RepositoryName: "Repo,One",
				ProjectName: "App",
				LineNumber: 1,
				NuGetInfo: packageInfo);

			ReportExporter.ExportPackageReferencesCsv(new List<PackageReferenceExtractor.PackageReference> { reference }, path);

			var content = File.ReadAllText(path);
			content.ShouldContain("\"Repo,One\"");
			content.ShouldContain("\"/src/\"\"App\"\"/App.csproj\"");
			content.ShouldContain("\"Feed,One\"");
			content.ShouldContain("\"Source,Repo\"");
			content.ShouldContain("\"Source \"\"Project\"\"\"");
		}

		[Test]
		public void WhenExportVersionWarningsCsvThenWritesHeaderAndWarning()
		{
			var path = CreatePath("warnings.csv");
			var warnings = new List<VersionWarning>
			{
				new("Newtonsoft.Json", "Repo", "/src/App/App.csproj", "2.0.0", "1.0.0", "version-mismatch-available", VersionWarningLevel.Major, "Major version mismatch")
			};

			ReportExporter.ExportVersionWarningsCsv(warnings, path);

			var content = File.ReadAllText(path);
			content.ShouldContain("PackageName,Repository,ProjectPath,CurrentVersion,ReferenceVersion,WarningType,Level,Description");
			content.ShouldContain("Newtonsoft.Json,Repo,/src/App/App.csproj,2.0.0,1.0.0,version-mismatch-available,Major,Major version mismatch");
		}

		[Test]
		public void WhenExportRecommendationsCsvThenWritesHeaderAndRecommendation()
		{
			var path = CreatePath("recommendations.csv");
			var recommendations = new List<PackageRecommendation>
			{
				new("Newtonsoft.Json", "Repo", "/src/App/App.csproj", "1.0.0", "2.0.0", "upgrade", "Update to latest version")
			};

			ReportExporter.ExportRecommendationsCsv(recommendations, path);

			var content = File.ReadAllText(path);
			content.ShouldContain("PackageName,Repository,ProjectPath,CurrentVersion,RecommendedVersion,RecommendationType,Reason");
			content.ShouldContain("Newtonsoft.Json,Repo,/src/App/App.csproj,1.0.0,2.0.0,upgrade,Update to latest version");
		}

		[Test]
		public void WhenExportWarningsJsonThenWritesWarningLevel()
		{
			var path = CreatePath("warnings.json");
			var warnings = new List<VersionWarning>
			{
				new("Newtonsoft.Json", "Repo", "/src/App/App.csproj", "2.0.0", "1.0.0", "version-mismatch-available", VersionWarningLevel.Minor, "Minor version mismatch")
			};

			ReportExporter.ExportWarningsJson(warnings, path);

			using var document = JsonDocument.Parse(File.ReadAllText(path));
			var root = document.RootElement;
			root.GetProperty("totalWarnings").GetInt32().ShouldBe(1);
			root.GetProperty("warnings")[0].GetProperty("level").GetString().ShouldBe("Minor");
		}

		[Test]
		public void WhenExportRecommendationsJsonThenWritesSummaryCounts()
		{
			var path = CreatePath("recommendations.json");
			var recommendations = new List<PackageRecommendation>
			{
				new("Newtonsoft.Json", "Repo", "/src/App/App.csproj", "1.0.0", "2.0.0", "upgrade", "Update project A"),
				new("Newtonsoft.Json", "Repo", "/src/Lib/Lib.csproj", "1.0.0", "2.0.0", "upgrade", "Update project B")
			};

			ReportExporter.ExportRecommendationsJson(recommendations, path);

			using var document = JsonDocument.Parse(File.ReadAllText(path));
			var root = document.RootElement;
			root.GetProperty("totalRecommendations").GetInt32().ShouldBe(2);
			root.GetProperty("packagesNeedingUpdate").GetInt32().ShouldBe(1);
			root.GetProperty("projectsAffected").GetInt32().ShouldBe(2);
		}

		[Test]
		public void WhenExportVulnerabilitiesJsonThenGroupsAffectedProjectsByPackage()
		{
			var path = CreatePath("vulnerabilities.json");
			var packageInfo = CreatePackageInfo() with
			{
				IsVulnerable   = true,
				LatestVersion  = "3.0.0",
				Vulnerabilities = new List<string> { "ADV-001", "ADV-002" }
			};
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				CreatePackageReference("Serilog", "1.0.0", packageInfo),
				new(
					PackageName: "Serilog",
					Version: "2.0.0",
					ProjectPath: "/src/Lib/Lib.csproj",
					RepositoryName: "Repo",
					ProjectName: "Lib",
					LineNumber: 1,
					NuGetInfo: packageInfo)
			};

			ReportExporter.ExportVulnerabilitiesJson(references, path);

			using var document = JsonDocument.Parse(File.ReadAllText(path));
			var root = document.RootElement;
			root.GetProperty("totalVulnerabilities").GetInt32().ShouldBe(2);
			root.GetProperty("packagesAffected").GetInt32().ShouldBe(1);
			root.GetProperty("packages")[0].GetProperty("usedVersions").GetArrayLength().ShouldBe(2);
			root.GetProperty("packages")[0].GetProperty("affectedProjects").GetArrayLength().ShouldBe(2);
		}

		[Test]
		public void WhenExportVulnerabilitiesCsvThenWritesOneRowPerAdvisory()
		{
			var path = CreatePath("vulnerabilities.csv");
			var packageInfo = CreatePackageInfo() with
			{
				IsVulnerable   = true,
				LatestVersion  = "3.0.0",
				Vulnerabilities = new List<string> { "ADV-001", "ADV-002" }
			};
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				CreatePackageReference("Serilog", "1.0.0", packageInfo)
			};

			ReportExporter.ExportVulnerabilitiesCsv(references, path);

			var lines = File.ReadAllLines(path);
			lines.Length.ShouldBe(3);
			lines[1].ShouldContain("ADV-001");
			lines[2].ShouldContain("ADV-002");
		}

		[Test]
		public void WhenPrintRecommendationsWithNoRecommendationsThenWritesSuccessMessage()
		{
			var output = CaptureConsoleOutput(() => ReportExporter.PrintRecommendations(new List<PackageRecommendation>()));

			output.ShouldContain("No package updates recommended. All packages are up to date!");
		}

		[Test]
		public void WhenPrintVersionWarningsWithPinnedPackagesThenFiltersPinnedWarnings()
		{
			var warnings = new List<VersionWarning>
			{
				new("Newtonsoft.Json", "Repo", "/src/App/App.csproj", "2.0.0", "1.0.0", "version-mismatch-available", VersionWarningLevel.Major, "Should be filtered"),
				new("Newtonsoft.Json", "Repo", "/src/App/App.csproj", "2.0.0", "1.0.0", "pinned-version-mismatch", VersionWarningLevel.Major, "Should remain")
			};
			var pinnedPackages = new Dictionary<string, string?>
			{
				["Newtonsoft.Json"] = "1.0.0"
			};

			var output = CaptureConsoleOutput(() => ReportExporter.PrintVersionWarnings(warnings, pinnedPackages));

			output.ShouldContain("Should remain");
			output.ShouldNotContain("Should be filtered");
		}

		private string CreatePath(string fileName)
		{
			return Path.Combine(_tempDir, fileName);
		}

		private static PackageReferenceExtractor.PackageReference CreatePackageReference(
			string packageName,
			string version,
			NuGetPackageResolver.PackageInfo? packageInfo = null)
		{
			var info = packageInfo ?? CreatePackageInfo();

			return new PackageReferenceExtractor.PackageReference(
				PackageName: packageName,
				Version: version,
				ProjectPath: "/src/App/App.csproj",
				RepositoryName: "Repo",
				ProjectName: "App",
				LineNumber: 1,
				NuGetInfo: info);
		}

		private static NuGetPackageResolver.PackageInfo CreatePackageInfo()
		{
			return new NuGetPackageResolver.PackageInfo(
				PackageName: "Newtonsoft.Json",
				ExistsOnNuGetOrg: true,
				PackageUrl: null,
				Description: null,
				Authors: null,
				ProjectUrl: null,
				Published: null,
				DownloadCount: null,
				LicenseUrl: null,
				IconUrl: null,
				Tags: new List<string>(),
				IsDeprecated: false,
				DeprecationMessage: null,
				SourceProjects: new List<NuGetPackageResolver.ProjectReference>(),
				LatestVersion: "2.0.0",
				ResolvedVersion: "1.0.0",
				IsOutdated: false,
				IsVulnerable: false,
				Vulnerabilities: new List<string>(),
				FeedName: "NuGet.org");
		}

		private static string CaptureConsoleOutput(Action action)
		{
			var originalOut = Console.Out;
			using var writer = new StringWriter();
			Console.SetOut(writer);

			try
			{
				action();
			}
			finally
			{
				Console.SetOut(originalOut);
			}

			return writer.ToString();
		}
	}
}
