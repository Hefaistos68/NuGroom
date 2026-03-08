using NuGroom.Configuration;
using NuGroom.Nuget;
using NuGroom.Workflows;

using Shouldly;

namespace NuGroom.Tests
{
	[TestFixture]
	public class LocalScanProjectFilterTests
	{
		private string _tempDir = string.Empty;

		[SetUp]
		public void SetUp()
		{
			_tempDir = Path.Combine(Path.GetTempPath(), $"NuGroomFilter_{Guid.NewGuid():N}");
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
		public async Task WhenExcludeProjectPatternThenMatchingProjectIsSkipped()
		{
			CreateProject("App.csproj", "Newtonsoft.Json", "13.0.3");
			CreateProject("App.Tests.csproj", "Moq", "4.20.0");

			var parseResult = BuildParseResult(
				paths: [_tempDir],
				excludeProjectPatterns: [@".*\.Tests\.csproj$"]);

			var result = await LocalScanWorkflow.ExecuteAsync(parseResult);

			result.References.ShouldNotBeNull();
			result.References!.ShouldAllBe(r => r.PackageName == "Newtonsoft.Json");
		}

		[Test]
		public async Task WhenIncludeProjectPatternThenOnlyMatchingProjectIsScanned()
		{
			CreateProject("App.Web.csproj", "Newtonsoft.Json", "13.0.3");
			CreateProject("App.Console.csproj", "Serilog", "3.0.0");

			var parseResult = BuildParseResult(
				paths: [_tempDir],
				includeProjectPatterns: [@".*\.Web\.csproj$"]);

			var result = await LocalScanWorkflow.ExecuteAsync(parseResult);

			result.References.ShouldNotBeNull();
			result.References!.ShouldAllBe(r => r.PackageName == "Newtonsoft.Json");
		}

		[Test]
		public async Task WhenBothIncludeAndExcludeThenExcludeTakesPrecedence()
		{
			CreateProject("App.Web.csproj", "Newtonsoft.Json", "13.0.3");
			CreateProject("App.Web.Tests.csproj", "Moq", "4.20.0");
			CreateProject("App.Console.csproj", "Serilog", "3.0.0");

			var parseResult = BuildParseResult(
				paths: [_tempDir],
				includeProjectPatterns: [@".*\.Web.*\.csproj$"],
				excludeProjectPatterns: [@".*\.Tests\.csproj$"]);

			var result = await LocalScanWorkflow.ExecuteAsync(parseResult);

			result.References.ShouldNotBeNull();
			result.References!.ShouldAllBe(r => r.PackageName == "Newtonsoft.Json");
		}

		[Test]
		public async Task WhenNoFiltersThenAllProjectsAreScanned()
		{
			CreateProject("App.csproj", "Newtonsoft.Json", "13.0.3");
			CreateProject("Lib.csproj", "Serilog", "3.0.0");

			var parseResult = BuildParseResult(paths: [_tempDir]);

			var result = await LocalScanWorkflow.ExecuteAsync(parseResult);

			result.References.ShouldNotBeNull();
			result.References!.Count.ShouldBe(2);
		}

		private void CreateProject(string fileName, string packageName, string version)
		{
			var content = $"""
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
				    <PackageReference Include="{packageName}" Version="{version}" />
				  </ItemGroup>
				</Project>
				""";
			File.WriteAllText(Path.Combine(_tempDir, fileName), content);
		}

		private static ParseResult BuildParseResult(
			List<string> paths,
			List<string>? excludeProjectPatterns = null,
			List<string>? includeProjectPatterns = null)
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
				UpdateConfig: null,
				SyncConfigs: new List<SyncConfig>(),
				LocalPaths: paths,
				ExcludeProjectPatterns: excludeProjectPatterns,
				IncludeProjectPatterns: includeProjectPatterns);
		}
	}
}
