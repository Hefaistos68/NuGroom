using NuGroom.Nuget;

using Shouldly;

namespace NuGroom.Tests
{
	[TestFixture]
	public class PackagesConfigExtractorTests
	{
		[Test]
		public void WhenValidPackagesConfigThenExtractsAllPackages()
		{
			var xml = """
				<?xml version="1.0" encoding="utf-8"?>
				<packages>
				  <package id="Newtonsoft.Json" version="13.0.3" targetFramework="net48" />
				  <package id="Serilog" version="2.12.0" targetFramework="net48" />
				</packages>
				""";

			var result = PackagesConfigExtractor.Extract(xml, "MyRepo", "/src/App/App.csproj", "App");

			result.Count.ShouldBe(2);
			result[0].PackageName.ShouldBe("Newtonsoft.Json");
			result[0].Version.ShouldBe("13.0.3");
			result[1].PackageName.ShouldBe("Serilog");
			result[1].Version.ShouldBe("2.12.0");
		}

		[Test]
		public void WhenValidPackagesConfigThenSourceKindIsPackagesConfig()
		{
			var xml = """
				<?xml version="1.0" encoding="utf-8"?>
				<packages>
				  <package id="Newtonsoft.Json" version="13.0.3" targetFramework="net48" />
				</packages>
				""";

			var result = PackagesConfigExtractor.Extract(xml, "MyRepo", "/src/App/App.csproj", "App");

			result.Count.ShouldBe(1);
			result[0].SourceKind.ShouldBe(PackageSourceKind.PackagesConfig);
		}

		[Test]
		public void WhenValidPackagesConfigThenProjectPathIsSetCorrectly()
		{
			var xml = """
				<?xml version="1.0" encoding="utf-8"?>
				<packages>
				  <package id="Serilog" version="2.12.0" />
				</packages>
				""";

			var result = PackagesConfigExtractor.Extract(xml, "MyRepo", "/src/App/App.csproj", "App");

			result[0].ProjectPath.ShouldBe("/src/App/App.csproj");
			result[0].RepositoryName.ShouldBe("MyRepo");
			result[0].ProjectName.ShouldBe("App");
		}

		[Test]
		public void WhenPackageMissingVersionThenExtractsWithNullVersion()
		{
			var xml = """
				<?xml version="1.0" encoding="utf-8"?>
				<packages>
				  <package id="Newtonsoft.Json" />
				</packages>
				""";

			var result = PackagesConfigExtractor.Extract(xml, "MyRepo", "/src/App/App.csproj", "App");

			result.Count.ShouldBe(1);
			result[0].PackageName.ShouldBe("Newtonsoft.Json");
			result[0].Version.ShouldBeNull();
		}

		[Test]
		public void WhenContentIsNullThenReturnsEmptyList()
		{
			var result = PackagesConfigExtractor.Extract(null, "MyRepo", "/src/App/App.csproj", "App");

			result.Count.ShouldBe(0);
		}

		[Test]
		public void WhenContentIsWhitespaceThenReturnsEmptyList()
		{
			var result = PackagesConfigExtractor.Extract("   ", "MyRepo", "/src/App/App.csproj", "App");

			result.Count.ShouldBe(0);
		}

		[Test]
		public void WhenInvalidXmlThenReturnsEmptyList()
		{
			var result = PackagesConfigExtractor.Extract("<not valid {{{", "MyRepo", "/src/App/App.csproj", "App");

			result.Count.ShouldBe(0);
		}

		[Test]
		public void WhenExclusionListAppliedThenExcludedPackagesFiltered()
		{
			var xml = """
				<?xml version="1.0" encoding="utf-8"?>
				<packages>
				  <package id="Microsoft.Extensions.Logging" version="8.0.0" />
				  <package id="Newtonsoft.Json" version="13.0.3" />
				</packages>
				""";

			var exclusions = new PackageReferenceExtractor.ExclusionList();
			exclusions.AddPrefixExclusion("Microsoft.");

			var result = PackagesConfigExtractor.Extract(xml, "MyRepo", "/src/App/App.csproj", "App", exclusions);

			result.Count.ShouldBe(1);
			result[0].PackageName.ShouldBe("Newtonsoft.Json");
		}

		[Test]
		public void WhenNoExclusionListThenAllPackagesReturned()
		{
			var xml = """
				<?xml version="1.0" encoding="utf-8"?>
				<packages>
				  <package id="Microsoft.Extensions.Logging" version="8.0.0" />
				  <package id="Newtonsoft.Json" version="13.0.3" />
				</packages>
				""";

			var result = PackagesConfigExtractor.Extract(xml, "MyRepo", "/src/App/App.csproj", "App");

			result.Count.ShouldBe(2);
		}

		[Test]
		public void WhenColocatedProjectExistsThenFindColocatedProjectFileReturnsMatch()
		{
			var projectFiles = new[] { "/src/App/App.csproj", "/src/Lib/Lib.csproj" };

			var result = PackagesConfigExtractor.FindColocatedProjectFile("/src/App/packages.config", projectFiles);

			result.ShouldBe("/src/App/App.csproj");
		}

		[Test]
		public void WhenNoColocatedProjectThenFindColocatedProjectFileReturnsNull()
		{
			var projectFiles = new[] { "/src/Lib/Lib.csproj" };

			var result = PackagesConfigExtractor.FindColocatedProjectFile("/src/App/packages.config", projectFiles);

			result.ShouldBeNull();
		}

		[Test]
		public void WhenEmptyProjectFileListThenFindColocatedProjectFileReturnsNull()
		{
			var result = PackagesConfigExtractor.FindColocatedProjectFile("/src/App/packages.config", Array.Empty<string>());

			result.ShouldBeNull();
		}

		[Test]
		public void WhenPackageIdIsEmptyThenEntryIsSkipped()
		{
			var xml = """
				<?xml version="1.0" encoding="utf-8"?>
				<packages>
				  <package id="" version="1.0.0" />
				  <package id="Serilog" version="2.12.0" />
				</packages>
				""";

			var result = PackagesConfigExtractor.Extract(xml, "MyRepo", "/src/App/App.csproj", "App");

			result.Count.ShouldBe(1);
			result[0].PackageName.ShouldBe("Serilog");
		}
	}
}
