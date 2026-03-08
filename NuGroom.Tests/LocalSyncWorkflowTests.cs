using NuGroom.Configuration;
using NuGroom.Nuget;
using NuGroom.Workflows;

using Shouldly;

namespace NuGroom.Tests
{
	[TestFixture]
	public class LocalSyncWorkflowTests
	{
		private string _tempDir = string.Empty;

		[SetUp]
		public void SetUp()
		{
			_tempDir = Path.Combine(Path.GetTempPath(), $"NuGroomLocalSync_{Guid.NewGuid():N}");
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
		public async Task WhenLocalSyncThenFileVersionIsUpdated()
		{
			var projectPath = Path.Combine(_tempDir, "MyApp.csproj");
			File.WriteAllText(projectPath, """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
					<PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
				  </ItemGroup>
				</Project>
				""");

			var parseResult = BuildParseResult(
				paths: [_tempDir],
				syncConfigs: [new SyncConfig("Newtonsoft.Json", "13.0.3")],
				updateConfig: null);
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "Newtonsoft.Json",
					Version: "13.0.1",
					ProjectPath: projectPath,
					RepositoryName: Path.GetFileName(_tempDir),
					ProjectName: "local",
					LineNumber: 1)
			};

			await LocalSyncWorkflow.ExecuteAsync(parseResult, references);

			var updated = File.ReadAllText(projectPath);
			updated.ShouldContain("13.0.3");
		}

		[Test]
		public async Task WhenLocalSyncWithDryRunThenFileIsNotModified()
		{
			var projectPath = Path.Combine(_tempDir, "MyApp.csproj");
			var originalContent = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
					<PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
				  </ItemGroup>
				</Project>
				""";
			File.WriteAllText(projectPath, originalContent);

			var parseResult = BuildParseResult(
				paths: [_tempDir],
				syncConfigs: [new SyncConfig("Newtonsoft.Json", "13.0.3")],
				updateConfig: new UpdateConfig { DryRun = true });
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "Newtonsoft.Json",
					Version: "13.0.1",
					ProjectPath: projectPath,
					RepositoryName: Path.GetFileName(_tempDir),
					ProjectName: "local",
					LineNumber: 1)
			};

			await LocalSyncWorkflow.ExecuteAsync(parseResult, references);

			var content = File.ReadAllText(projectPath);
			content.ShouldContain("13.0.1");
			content.ShouldNotContain("13.0.3");
		}

		private static ParseResult BuildParseResult(
			List<string> paths,
			List<SyncConfig> syncConfigs,
			UpdateConfig? updateConfig)
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
				SyncConfigs: syncConfigs,
				LocalPaths: paths);
		}
	}
}
