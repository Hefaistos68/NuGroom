using NuGroom.Nuget;

using Shouldly;

namespace NuGroom.Tests
{
	[TestFixture]
	public class CpmPackageExtractorTests
	{
		[Test]
		public void WhenValidCpmFileThenManagePackageVersionsCentrallyIsTrue()
		{
			var xml = """
				<Project>
				  <PropertyGroup>
				    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
				  </PropertyGroup>
				  <ItemGroup>
				    <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
				  </ItemGroup>
				</Project>
				""";

			var result = CpmPackageExtractor.Parse(xml);

			result.ManagePackageVersionsCentrally.ShouldBeTrue();
		}

		[Test]
		public void WhenCpmFlagIsFalseThenManagePackageVersionsCentrallyIsFalse()
		{
			var xml = """
				<Project>
				  <PropertyGroup>
				    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
				  </PropertyGroup>
				  <ItemGroup>
				    <PackageVersion Include="Serilog" Version="3.1.1" />
				  </ItemGroup>
				</Project>
				""";

			var result = CpmPackageExtractor.Parse(xml);

			result.ManagePackageVersionsCentrally.ShouldBeFalse();
		}

		[Test]
		public void WhenNoCpmFlagThenManagePackageVersionsCentrallyIsFalse()
		{
			var xml = """
				<Project>
				  <ItemGroup>
				    <PackageVersion Include="Serilog" Version="3.1.1" />
				  </ItemGroup>
				</Project>
				""";

			var result = CpmPackageExtractor.Parse(xml);

			result.ManagePackageVersionsCentrally.ShouldBeFalse();
		}

		[Test]
		public void WhenMultiplePackageVersionsThenAllExtracted()
		{
			var xml = """
				<Project>
				  <PropertyGroup>
				    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
				  </PropertyGroup>
				  <ItemGroup>
				    <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
				    <PackageVersion Include="Serilog" Version="3.1.1" />
				    <PackageVersion Include="xunit" Version="2.9.0" />
				  </ItemGroup>
				</Project>
				""";

			var result = CpmPackageExtractor.Parse(xml);

			result.PackageVersions.Count.ShouldBe(3);
			result.PackageVersions["Newtonsoft.Json"].ShouldBe("13.0.3");
			result.PackageVersions["Serilog"].ShouldBe("3.1.1");
			result.PackageVersions["xunit"].ShouldBe("2.9.0");
		}

		[Test]
		public void WhenPackageVersionMissingVersionAttributeThenSkipped()
		{
			var xml = """
				<Project>
				  <ItemGroup>
				    <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
				    <PackageVersion Include="Serilog" />
				  </ItemGroup>
				</Project>
				""";

			var result = CpmPackageExtractor.Parse(xml);

			result.PackageVersions.Count.ShouldBe(1);
			result.PackageVersions.ShouldContainKey("Newtonsoft.Json");
		}

		[Test]
		public void WhenContentIsNullThenReturnsEmptyResult()
		{
			var result = CpmPackageExtractor.Parse(null);

			result.ManagePackageVersionsCentrally.ShouldBeFalse();
			result.PackageVersions.Count.ShouldBe(0);
		}

		[Test]
		public void WhenContentIsWhitespaceThenReturnsEmptyResult()
		{
			var result = CpmPackageExtractor.Parse("   ");

			result.ManagePackageVersionsCentrally.ShouldBeFalse();
			result.PackageVersions.Count.ShouldBe(0);
		}

		[Test]
		public void WhenInvalidXmlThenReturnsEmptyResult()
		{
			var result = CpmPackageExtractor.Parse("<not valid xml {{{");

			result.ManagePackageVersionsCentrally.ShouldBeFalse();
			result.PackageVersions.Count.ShouldBe(0);
		}

		[Test]
		public void WhenPackageVersionLookupIsCaseInsensitiveThenMatchesDifferentCase()
		{
			var xml = """
				<Project>
				  <ItemGroup>
				    <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
				  </ItemGroup>
				</Project>
				""";

			var result = CpmPackageExtractor.Parse(xml);

			result.PackageVersions.ShouldContainKey("newtonsoft.json");
		}

		[Test]
		public void WhenProjectReferenceHasNoVersionThenMergeCpmVersionsFillsFromCpm()
		{
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "Newtonsoft.Json",
					Version: null,
					ProjectPath: "src/App/App.csproj",
					RepositoryName: "App",
					ProjectName: "App",
					LineNumber: 5)
			};

			var cpmVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["Newtonsoft.Json"] = "13.0.3"
			};

			var merged = CpmPackageExtractor.MergeCpmVersions(references, cpmVersions);

			merged.Count.ShouldBe(1);
			merged[0].Version.ShouldBe("13.0.3");
			merged[0].SourceKind.ShouldBe(PackageSourceKind.CentralPackageManagement);
		}

		[Test]
		public void WhenProjectReferenceHasVersionOverrideThenMergeCpmVersionsKeepsOverride()
		{
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "Newtonsoft.Json",
					Version: "12.0.3",
					ProjectPath: "src/App/App.csproj",
					RepositoryName: "App",
					ProjectName: "App",
					LineNumber: 5)
			};

			var cpmVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["Newtonsoft.Json"] = "13.0.3"
			};

			var merged = CpmPackageExtractor.MergeCpmVersions(references, cpmVersions);

			merged.Count.ShouldBe(1);
			merged[0].Version.ShouldBe("12.0.3");
			merged[0].SourceKind.ShouldBe(PackageSourceKind.ProjectFile);
		}

		[Test]
		public void WhenCpmVersionsIsEmptyThenMergeCpmVersionsReturnsOriginal()
		{
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "Serilog",
					Version: null,
					ProjectPath: "src/App/App.csproj",
					RepositoryName: "App",
					ProjectName: "App",
					LineNumber: 3)
			};

			var merged = CpmPackageExtractor.MergeCpmVersions(
				references,
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

			merged.ShouldBeSameAs(references);
		}

		[Test]
		public void WhenPackageNotInCpmThenMergeCpmVersionsKeepsNullVersion()
		{
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "UnknownPackage",
					Version: null,
					ProjectPath: "src/App/App.csproj",
					RepositoryName: "App",
					ProjectName: "App",
					LineNumber: 7)
			};

			var cpmVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["Newtonsoft.Json"] = "13.0.3"
			};

			var merged = CpmPackageExtractor.MergeCpmVersions(references, cpmVersions);

			merged.Count.ShouldBe(1);
			merged[0].Version.ShouldBeNull();
		}

		[Test]
		public void WhenVersionOverrideExtractedThenMergeCpmVersionsPreservesOverrideAndKeepsProjectFileKind()
		{
			// Simulates extraction result where VersionOverride="14.0.0" was read into Version
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "Newtonsoft.Json",
					Version: "14.0.0",
					ProjectPath: "src/App/App.csproj",
					RepositoryName: "App",
					ProjectName: "App",
					LineNumber: 5)
			};

			var cpmVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["Newtonsoft.Json"] = "13.0.3"
			};

			var merged = CpmPackageExtractor.MergeCpmVersions(references, cpmVersions);

			merged.Count.ShouldBe(1);
			merged[0].Version.ShouldBe("14.0.0");
			merged[0].SourceKind.ShouldBe(PackageSourceKind.ProjectFile);
		}

		[Test]
		public void WhenMixOfOverrideAndCpmManagedThenMergeCpmVersionsHandlesBothCorrectly()
		{
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "Newtonsoft.Json",
					Version: "14.0.0",
					ProjectPath: "src/App/App.csproj",
					RepositoryName: "App",
					ProjectName: "App",
					LineNumber: 5),
				new(
					PackageName: "Serilog",
					Version: null,
					ProjectPath: "src/App/App.csproj",
					RepositoryName: "App",
					ProjectName: "App",
					LineNumber: 6)
			};

			var cpmVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["Newtonsoft.Json"] = "13.0.3",
				["Serilog"]         = "3.1.1"
			};

			var merged = CpmPackageExtractor.MergeCpmVersions(references, cpmVersions);

			merged.Count.ShouldBe(2);

			// Newtonsoft.Json has VersionOverride — kept as ProjectFile
			var nj = merged.First(r => r.PackageName == "Newtonsoft.Json");
			nj.Version.ShouldBe("14.0.0");
			nj.SourceKind.ShouldBe(PackageSourceKind.ProjectFile);

			// Serilog has no version — filled from CPM
			var sl = merged.First(r => r.PackageName == "Serilog");
			sl.Version.ShouldBe("3.1.1");
			sl.SourceKind.ShouldBe(PackageSourceKind.CentralPackageManagement);
		}
	}
}
