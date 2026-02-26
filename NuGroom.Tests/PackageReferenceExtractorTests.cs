using NuGroom.Nuget;

using Shouldly;

namespace NuGroom.Tests
{
	[TestFixture]
	public class PackageReferenceExtractorTests
	{
		private const string SimpleCsproj = """
			<Project Sdk="Microsoft.NET.Sdk">
			  <ItemGroup>
			    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
			    <PackageReference Include="Serilog" Version="2.12.0" />
			  </ItemGroup>
			</Project>
			""";

		private const string CsprojWithCondition = """
			<Project Sdk="Microsoft.NET.Sdk">
			  <ItemGroup>
			    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
			    <PackageReference Include="xunit" Version="2.5.0" Condition="'$(Configuration)'=='Debug'" />
			  </ItemGroup>
			</Project>
			""";

		private const string EmptyCsproj = """
			<Project Sdk="Microsoft.NET.Sdk">
			  <PropertyGroup>
			    <TargetFramework>net10.0</TargetFramework>
			  </PropertyGroup>
			</Project>
			""";

		[Test]
		public void WhenCsprojHasPackageReferencesThenExtractsAll()
		{
			var extractor = new PackageReferenceExtractor(
				PackageReferenceExtractor.ExclusionList.CreateEmpty());

			var result = extractor.ExtractPackageReferences(
				SimpleCsproj, "TestRepo", "/src/Project.csproj");

			result.Count.ShouldBe(2);
		}

		[Test]
		public void WhenCsprojHasPackageReferencesThenExtractsCorrectNames()
		{
			var extractor = new PackageReferenceExtractor(
				PackageReferenceExtractor.ExclusionList.CreateEmpty());

			var result = extractor.ExtractPackageReferences(
				SimpleCsproj, "TestRepo", "/src/Project.csproj");

			result.ShouldContain(r => r.PackageName == "Newtonsoft.Json");
			result.ShouldContain(r => r.PackageName == "Serilog");
		}

		[Test]
		public void WhenCsprojHasPackageReferencesThenExtractsCorrectVersions()
		{
			var extractor = new PackageReferenceExtractor(
				PackageReferenceExtractor.ExclusionList.CreateEmpty());

			var result = extractor.ExtractPackageReferences(
				SimpleCsproj, "TestRepo", "/src/Project.csproj");

			result.First(r => r.PackageName == "Newtonsoft.Json").Version.ShouldBe("13.0.3");
			result.First(r => r.PackageName == "Serilog").Version.ShouldBe("2.12.0");
		}

		[Test]
		public void WhenCsprojHasPackageReferencesThenSetsRepositoryName()
		{
			var extractor = new PackageReferenceExtractor(
				PackageReferenceExtractor.ExclusionList.CreateEmpty());

			var result = extractor.ExtractPackageReferences(
				SimpleCsproj, "TestRepo", "/src/Project.csproj");

			result.ShouldAllBe(r => r.RepositoryName == "TestRepo");
		}

		[Test]
		public void WhenCsprojHasPackageReferencesThenSetsProjectPath()
		{
			var extractor = new PackageReferenceExtractor(
				PackageReferenceExtractor.ExclusionList.CreateEmpty());

			var result = extractor.ExtractPackageReferences(
				SimpleCsproj, "TestRepo", "/src/Project.csproj");

			result.ShouldAllBe(r => r.ProjectPath == "/src/Project.csproj");
		}

		[Test]
		public void WhenCsprojHasNoPackageReferencesThenReturnsEmptyList()
		{
			var extractor = new PackageReferenceExtractor(
				PackageReferenceExtractor.ExclusionList.CreateEmpty());

			var result = extractor.ExtractPackageReferences(
				EmptyCsproj, "TestRepo", "/src/Project.csproj");

			result.ShouldBeEmpty();
		}

		[Test]
		public void WhenContentIsEmptyThenReturnsEmptyList()
		{
			var extractor = new PackageReferenceExtractor(
				PackageReferenceExtractor.ExclusionList.CreateEmpty());

			var result = extractor.ExtractPackageReferences(
				"", "TestRepo", "/src/Project.csproj");

			result.ShouldBeEmpty();
		}

		[Test]
		public void WhenExclusionListMatchesThenExcludesMatchingPackages()
		{
			var exclusionList = PackageReferenceExtractor.ExclusionList.CreateEmpty();
			exclusionList.AddPackageExclusion("Serilog");
			var extractor = new PackageReferenceExtractor(exclusionList);

			var result = extractor.ExtractPackageReferences(
				SimpleCsproj, "TestRepo", "/src/Project.csproj");

			result.Count.ShouldBe(1);
			result[0].PackageName.ShouldBe("Newtonsoft.Json");
		}

		[Test]
		public void WhenCsprojHasConditionalReferencesThenExtractsBoth()
		{
			var extractor = new PackageReferenceExtractor(
				PackageReferenceExtractor.ExclusionList.CreateEmpty());

			var result = extractor.ExtractPackageReferences(
				CsprojWithCondition, "TestRepo", "/src/Project.csproj");

			result.Count.ShouldBe(2);
		}

		[TestCase("Newtonsoft.Json", "13.0.3")]
		[TestCase("Serilog", "2.12.0")]
		public void WhenExtractingThenVersionMatchesExpected(string packageName, string expectedVersion)
		{
			var extractor = new PackageReferenceExtractor(
				PackageReferenceExtractor.ExclusionList.CreateEmpty());

			var result = extractor.ExtractPackageReferences(
				SimpleCsproj, "TestRepo", "/src/Project.csproj");

			result.First(r => r.PackageName == packageName).Version.ShouldBe(expectedVersion);
		}

		[Test]
		public void WhenPackageHasVersionOverrideThenExtractsVersionOverrideValue()
		{
			var csproj = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
					<PackageReference Include="Newtonsoft.Json" VersionOverride="14.0.0" />
				  </ItemGroup>
				</Project>
				""";

			var extractor = new PackageReferenceExtractor(
				PackageReferenceExtractor.ExclusionList.CreateEmpty());

			var result = extractor.ExtractPackageReferences(
				csproj, "TestRepo", "/src/Project.csproj");

			result.Count.ShouldBe(1);
			result[0].Version.ShouldBe("14.0.0");
		}

		[Test]
		public void WhenPackageHasBothVersionAndVersionOverrideThenVersionOverrideWins()
		{
			var csproj = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
					<PackageReference Include="Serilog" Version="2.12.0" VersionOverride="3.0.0" />
				  </ItemGroup>
				</Project>
				""";

			var extractor = new PackageReferenceExtractor(
				PackageReferenceExtractor.ExclusionList.CreateEmpty());

			var result = extractor.ExtractPackageReferences(
				csproj, "TestRepo", "/src/Project.csproj");

			result.Count.ShouldBe(1);
			result[0].Version.ShouldBe("3.0.0");
		}

		[Test]
		public void WhenPackageHasNoVersionOrVersionOverrideThenVersionIsNull()
		{
			var csproj = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
					<PackageReference Include="Newtonsoft.Json" />
				  </ItemGroup>
				</Project>
				""";

			var extractor = new PackageReferenceExtractor(
				PackageReferenceExtractor.ExclusionList.CreateEmpty());

			var result = extractor.ExtractPackageReferences(
				csproj, "TestRepo", "/src/Project.csproj");

			result.Count.ShouldBe(1);
			result[0].Version.ShouldBeNull();
		}
	}
}
