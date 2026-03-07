using NuGroom.Configuration;
using NuGroom.Nuget;

using Shouldly;

namespace NuGroom.Tests
{
	[TestFixture]
	public class LocalScanWorkflowTests
	{
		private string _tempDir = string.Empty;

		[SetUp]
		public void SetUp()
		{
			_tempDir = Path.Combine(Path.GetTempPath(), $"NuGroomTests_{Guid.NewGuid():N}");
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

		private static ParseResult BuildLocalParseResult(List<string> paths)
		{
			var exclusionList = PackageReferenceExtractor.ExclusionList.CreateEmpty();

			return new ParseResult(
				Config: null,
				ExclusionList: exclusionList,
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
				LocalPaths: paths);
		}

		[Test]
		public async Task WhenDirectoryWithCsprojThenPackagesAreExtracted()
		{
			var csprojContent = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
				    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
				    <PackageReference Include="NUnit" Version="4.0.1" />
				  </ItemGroup>
				</Project>
				""";

			var projectPath = Path.Combine(_tempDir, "MyApp.csproj");
			await File.WriteAllTextAsync(projectPath, csprojContent);

			var parseResult = BuildLocalParseResult([_tempDir]);
			var result = await NuGroom.Workflows.LocalScanWorkflow.ExecuteAsync(parseResult);

			result.ShouldNotBeNull();
			result.References.ShouldNotBeNull();
			result.References.Count.ShouldBe(2);
			result.References.ShouldContain(r => r.PackageName == "Newtonsoft.Json" && r.Version == "13.0.3");
			result.References.ShouldContain(r => r.PackageName == "NUnit" && r.Version == "4.0.1");
		}

		[Test]
		public async Task WhenSingleCsprojFileThenPackagesAreExtracted()
		{
			var csprojContent = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
				    <PackageReference Include="Serilog" Version="3.1.1" />
				  </ItemGroup>
				</Project>
				""";

			var projectPath = Path.Combine(_tempDir, "MyLib.csproj");
			await File.WriteAllTextAsync(projectPath, csprojContent);

			var parseResult = BuildLocalParseResult([projectPath]);
			var result = await NuGroom.Workflows.LocalScanWorkflow.ExecuteAsync(parseResult);

			result.References.Count.ShouldBe(1);
			result.References[0].PackageName.ShouldBe("Serilog");
			result.References[0].Version.ShouldBe("3.1.1");
		}

		[Test]
		public async Task WhenEmptyDirectoryThenNoPackagesFound()
		{
			var parseResult = BuildLocalParseResult([_tempDir]);
			var result = await NuGroom.Workflows.LocalScanWorkflow.ExecuteAsync(parseResult);

			result.References.ShouldNotBeNull();
			result.References.ShouldBeEmpty();
		}

		[Test]
		public async Task WhenDirectoryPackagesPropsThenCpmVersionsAreMerged()
		{
			var propsContent = """
				<Project>
				  <PropertyGroup>
				    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
				  </PropertyGroup>
				  <ItemGroup>
				    <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
				  </ItemGroup>
				</Project>
				""";

			var csprojContent = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
				    <PackageReference Include="Newtonsoft.Json" />
				  </ItemGroup>
				</Project>
				""";

			await File.WriteAllTextAsync(Path.Combine(_tempDir, "Directory.Packages.props"), propsContent);
			await File.WriteAllTextAsync(Path.Combine(_tempDir, "MyApp.csproj"), csprojContent);

			var parseResult = BuildLocalParseResult([_tempDir]);
			var result = await NuGroom.Workflows.LocalScanWorkflow.ExecuteAsync(parseResult);

			result.References.Count.ShouldBe(1);
			result.References[0].PackageName.ShouldBe("Newtonsoft.Json");
			result.References[0].Version.ShouldBe("13.0.3");
		}

		[Test]
		public async Task WhenMultiplePathsThenAllProjectFilesAreScanned()
		{
			var dir2 = Path.Combine(_tempDir, "sub");
			Directory.CreateDirectory(dir2);

			await File.WriteAllTextAsync(Path.Combine(_tempDir, "A.csproj"), """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
				    <PackageReference Include="PackageA" Version="1.0.0" />
				  </ItemGroup>
				</Project>
				""");

			await File.WriteAllTextAsync(Path.Combine(dir2, "B.csproj"), """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
				    <PackageReference Include="PackageB" Version="2.0.0" />
				  </ItemGroup>
				</Project>
				""");

			var parseResult = BuildLocalParseResult([_tempDir, dir2]);
			var result = await NuGroom.Workflows.LocalScanWorkflow.ExecuteAsync(parseResult);

			// _tempDir is enumerated recursively so both files would be found from it alone,
			// but the second path being a subdirectory of the first means B.csproj appears only once
			// thanks to the duplicate-prevention logic.
			result.References.ShouldContain(r => r.PackageName == "PackageA");
			result.References.ShouldContain(r => r.PackageName == "PackageB");
		}

		[Test]
		public async Task WhenNonExistentPathThenNoExceptionIsThrown()
		{
			var parseResult = BuildLocalParseResult([Path.Combine(_tempDir, "nonexistent")]);
			var result = await NuGroom.Workflows.LocalScanWorkflow.ExecuteAsync(parseResult);

			result.References.ShouldBeEmpty();
		}

		[Test]
		public void WhenLocalPathsIsNullThenThrowsArgumentNullException()
		{
			var action = async () => await NuGroom.Workflows.LocalScanWorkflow.ExecuteAsync(null!);

			action.ShouldThrowAsync<ArgumentNullException>();
		}
	}
}
