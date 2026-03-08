using NuGroom.Configuration;
using NuGroom.Nuget;
using NuGroom.Workflows;

using Shouldly;

namespace NuGroom.Tests
{
	[TestFixture]
	public class LocalCpmMigrationWorkflowTests
	{
		private string _tempDir = string.Empty;

		[SetUp]
		public void SetUp()
		{
			_tempDir = Path.Combine(Path.GetTempPath(), $"NuGroomLocalCpm_{Guid.NewGuid():N}");
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
		public void WhenLocalCpmMigrationThenPropsFileIsGenerated()
		{
			var projectPath = Path.Combine(_tempDir, "MyApp.csproj");
			File.WriteAllText(projectPath, """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
					<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
				  </ItemGroup>
				</Project>
				""");

			var parseResult = BuildParseResult(
				paths: [_tempDir],
				updateConfig: null,
				migrateToCpm: true,
				perProject: false);
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "Newtonsoft.Json",
					Version: "13.0.3",
					ProjectPath: projectPath,
					RepositoryName: Path.GetFileName(_tempDir),
					ProjectName: "local",
					LineNumber: 1)
			};

			LocalCpmMigrationWorkflow.Execute(parseResult, references);

			File.Exists(Path.Combine(_tempDir, "Directory.Packages.props")).ShouldBeTrue();
		}

		[Test]
		public void WhenLocalCpmMigrationWithDryRunThenPropsFileIsNotGenerated()
		{
			var projectPath = Path.Combine(_tempDir, "MyApp.csproj");
			File.WriteAllText(projectPath, """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
					<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
				  </ItemGroup>
				</Project>
				""");

			var parseResult = BuildParseResult(
				paths: [_tempDir],
				updateConfig: new UpdateConfig { DryRun = true },
				migrateToCpm: true,
				perProject: false);
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "Newtonsoft.Json",
					Version: "13.0.3",
					ProjectPath: projectPath,
					RepositoryName: Path.GetFileName(_tempDir),
					ProjectName: "local",
					LineNumber: 1)
			};

			LocalCpmMigrationWorkflow.Execute(parseResult, references);

			File.Exists(Path.Combine(_tempDir, "Directory.Packages.props")).ShouldBeFalse();
		}

		private static ParseResult BuildParseResult(
			List<string> paths,
			UpdateConfig? updateConfig,
			bool migrateToCpm,
			bool perProject)
		{
			return new ParseResult(
				Config: null,
				ExclusionList: PackageReferenceExtractor.ExclusionList.CreateEmpty(),
				ResolveNuGet: false,
				ShowDetailedInfo: false,
				IgnoreRenovate: true,
				Feeds: new List<Feed>(),
				ExportPackagesPath: null,
				ExportWarningsPath: null,
				ExportRecommendationsPath: null,
				ExportSbomPath: null,
				ExportFormat: ExportFormat.Json,
				FeedAuth: new List<FeedAuth>(),
				VersionWarningConfig: null,
				UpdateConfig: updateConfig,
				SyncConfigs: new List<SyncConfig>(),
				MigrateToCpm: migrateToCpm,
				PerProject: perProject,
				LocalPaths: paths);
		}
	}
}
