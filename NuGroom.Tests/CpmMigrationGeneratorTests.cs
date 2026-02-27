using NuGroom.Nuget;

using Shouldly;

namespace NuGroom.Tests
{
	[TestFixture]
	public class CpmMigrationGeneratorTests
	{
		// ── GenerateDirectoryPackagesProps ────────────────────────────

		[Test]
		public void WhenSinglePackageThenGeneratesValidProps()
		{
			var versions = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["Newtonsoft.Json"] = "13.0.3"
			};

			var result = CpmMigrationGenerator.GenerateDirectoryPackagesProps(versions);

			result.ShouldContain("<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>");
			result.ShouldContain("<PackageVersion Include=\"Newtonsoft.Json\" Version=\"13.0.3\" />");
		}

		[Test]
		public void WhenMultiplePackagesThenAllIncludedAndSorted()
		{
			var versions = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["Serilog"]         = "3.1.1",
				["Newtonsoft.Json"] = "13.0.3",
				["xunit"]           = "2.9.0"
			};

			var result = CpmMigrationGenerator.GenerateDirectoryPackagesProps(versions);

			result.ShouldContain("<PackageVersion Include=\"Newtonsoft.Json\" Version=\"13.0.3\" />");
			result.ShouldContain("<PackageVersion Include=\"Serilog\" Version=\"3.1.1\" />");
			result.ShouldContain("<PackageVersion Include=\"xunit\" Version=\"2.9.0\" />");

			// Verify alphabetical ordering
			var newtonIndex = result.IndexOf("Newtonsoft.Json");
			var serilogIndex = result.IndexOf("Serilog");
			var xunitIndex = result.IndexOf("xunit");
			newtonIndex.ShouldBeLessThan(serilogIndex);
			serilogIndex.ShouldBeLessThan(xunitIndex);
		}

		// ── RemoveVersionAttributes ──────────────────────────────────

		[Test]
		public void WhenNoOverridesThenVersionAttributeIsRemoved()
		{
			var content = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
				    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
				  </ItemGroup>
				</Project>
				""";

			var result = CpmMigrationGenerator.RemoveVersionAttributes(
				content, new Dictionary<string, string>());

			result.ShouldContain("<PackageReference Include=\"Newtonsoft.Json\" />");
			result.ShouldNotContain("Version=\"13.0.3\"");
		}

		[Test]
		public void WhenOverrideThenVersionOverrideAttributeIsAdded()
		{
			var content = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
				    <PackageReference Include="Newtonsoft.Json" Version="12.0.0" />
				  </ItemGroup>
				</Project>
				""";

			var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["Newtonsoft.Json"] = "12.0.0"
			};

			var result = CpmMigrationGenerator.RemoveVersionAttributes(content, overrides);

			result.ShouldContain("VersionOverride=\"12.0.0\"");
			result.ShouldNotContain("Version=\"12.0.0\"");
		}

		[Test]
		public void WhenMixOfOverrideAndNormalThenHandledCorrectly()
		{
			var content = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
				    <PackageReference Include="Newtonsoft.Json" Version="12.0.0" />
				    <PackageReference Include="Serilog" Version="3.1.1" />
				  </ItemGroup>
				</Project>
				""";

			var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["Newtonsoft.Json"] = "12.0.0"
			};

			var result = CpmMigrationGenerator.RemoveVersionAttributes(content, overrides);

			result.ShouldContain("VersionOverride=\"12.0.0\"");
			result.ShouldContain("<PackageReference Include=\"Serilog\" />");
		}

		[Test]
		public void WhenMultiplePackagesThenAllVersionsRemoved()
		{
			var content = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
				    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
				    <PackageReference Include="Serilog" Version="3.1.1" />
				    <PackageReference Include="xunit" Version="2.9.0" />
				  </ItemGroup>
				</Project>
				""";

			var result = CpmMigrationGenerator.RemoveVersionAttributes(
				content, new Dictionary<string, string>());

			result.ShouldContain("<PackageReference Include=\"Newtonsoft.Json\" />");
			result.ShouldContain("<PackageReference Include=\"Serilog\" />");
			result.ShouldContain("<PackageReference Include=\"xunit\" />");
		}

		// ── Migrate (per-repository) ─────────────────────────────────

		[Test]
		public void WhenSingleProjectSinglePackageThenMigratesCorrectly()
		{
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "Newtonsoft.Json",
					Version: "13.0.3",
					ProjectPath: "src/App/App.csproj",
					RepositoryName: "MyRepo",
					ProjectName: "App",
					LineNumber: 5)
			};

			var projectContents = new Dictionary<string, string>
			{
				["src/App/App.csproj"] = """
					<Project Sdk="Microsoft.NET.Sdk">
					  <ItemGroup>
					    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
					  </ItemGroup>
					</Project>
					"""
			};

			var result = CpmMigrationGenerator.Migrate(references, projectContents, perProject: false);

			result.FileChanges.ShouldNotBeEmpty();
			result.Conflicts.ShouldBeEmpty();

			var propsChange = result.FileChanges.First(f => f.FilePath == "Directory.Packages.props");
			propsChange.IsNew.ShouldBeTrue();
			propsChange.Content.ShouldContain("<PackageVersion Include=\"Newtonsoft.Json\" Version=\"13.0.3\" />");

			var projChange = result.FileChanges.First(f => f.FilePath == "src/App/App.csproj");
			projChange.IsNew.ShouldBeFalse();
			projChange.Content.ShouldContain("<PackageReference Include=\"Newtonsoft.Json\" />");
			projChange.Content.ShouldNotContain("Version=\"13.0.3\"");
		}

		[Test]
		public void WhenConflictingVersionsThenHighestIsUsedAndOverrideAdded()
		{
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "Newtonsoft.Json",
					Version: "13.0.3",
					ProjectPath: "src/App1/App1.csproj",
					RepositoryName: "MyRepo",
					ProjectName: "App1",
					LineNumber: 5),
				new(
					PackageName: "Newtonsoft.Json",
					Version: "12.0.0",
					ProjectPath: "src/App2/App2.csproj",
					RepositoryName: "MyRepo",
					ProjectName: "App2",
					LineNumber: 5)
			};

			var projectContents = new Dictionary<string, string>
			{
				["src/App1/App1.csproj"] = """
					<Project Sdk="Microsoft.NET.Sdk">
					  <ItemGroup>
					    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
					  </ItemGroup>
					</Project>
					""",
				["src/App2/App2.csproj"] = """
					<Project Sdk="Microsoft.NET.Sdk">
					  <ItemGroup>
					    <PackageReference Include="Newtonsoft.Json" Version="12.0.0" />
					  </ItemGroup>
					</Project>
					"""
			};

			var result = CpmMigrationGenerator.Migrate(references, projectContents, perProject: false);

			// Central version should be the highest
			var propsChange = result.FileChanges.First(f => f.FilePath == "Directory.Packages.props");
			propsChange.Content.ShouldContain("<PackageVersion Include=\"Newtonsoft.Json\" Version=\"13.0.3\" />");

			// Project with highest version should have version removed
			var app1Change = result.FileChanges.First(f => f.FilePath == "src/App1/App1.csproj");
			app1Change.Content.ShouldContain("<PackageReference Include=\"Newtonsoft.Json\" />");
			app1Change.Content.ShouldNotContain("VersionOverride");

			// Project with lowest version should get VersionOverride
			var app2Change = result.FileChanges.First(f => f.FilePath == "src/App2/App2.csproj");
			app2Change.Content.ShouldContain("VersionOverride=\"12.0.0\"");

			// Conflict warning should be emitted
			result.Conflicts.Count.ShouldBe(1);
			result.Conflicts[0].PackageName.ShouldBe("Newtonsoft.Json");
			result.Conflicts[0].ProjectPath.ShouldBe("src/App2/App2.csproj");
			result.Conflicts[0].OverrideVersion.ShouldBe("12.0.0");
			result.Conflicts[0].CentralVersion.ShouldBe("13.0.3");
		}

		[Test]
		public void WhenNoEligibleReferencesThenReturnsEmptyResult()
		{
			// CPM-managed references (no explicit version) should be skipped
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "Newtonsoft.Json",
					Version: null,
					ProjectPath: "src/App/App.csproj",
					RepositoryName: "MyRepo",
					ProjectName: "App",
					LineNumber: 5,
					SourceKind: PackageSourceKind.CentralPackageManagement)
			};

			var projectContents = new Dictionary<string, string>();

			var result = CpmMigrationGenerator.Migrate(references, projectContents, perProject: false);

			result.FileChanges.ShouldBeEmpty();
			result.Conflicts.ShouldBeEmpty();
		}

		[Test]
		public void WhenThreeProjectsWithThreeVersionsThenConflictsReportedForTwoLowest()
		{
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "Serilog",
					Version: "3.0.0",
					ProjectPath: "src/A/A.csproj",
					RepositoryName: "Repo",
					ProjectName: "A",
					LineNumber: 1),
				new(
					PackageName: "Serilog",
					Version: "3.1.0",
					ProjectPath: "src/B/B.csproj",
					RepositoryName: "Repo",
					ProjectName: "B",
					LineNumber: 1),
				new(
					PackageName: "Serilog",
					Version: "3.2.0",
					ProjectPath: "src/C/C.csproj",
					RepositoryName: "Repo",
					ProjectName: "C",
					LineNumber: 1)
			};

			var projectContents = new Dictionary<string, string>
			{
				["src/A/A.csproj"] = "<Project><ItemGroup><PackageReference Include=\"Serilog\" Version=\"3.0.0\" /></ItemGroup></Project>",
				["src/B/B.csproj"] = "<Project><ItemGroup><PackageReference Include=\"Serilog\" Version=\"3.1.0\" /></ItemGroup></Project>",
				["src/C/C.csproj"] = "<Project><ItemGroup><PackageReference Include=\"Serilog\" Version=\"3.2.0\" /></ItemGroup></Project>"
			};

			var result = CpmMigrationGenerator.Migrate(references, projectContents, perProject: false);

			var propsChange = result.FileChanges.First(f => f.FilePath == "Directory.Packages.props");
			propsChange.Content.ShouldContain("Version=\"3.2.0\"");

			// Two conflicts: A (3.0.0) and B (3.1.0) differ from highest (3.2.0)
			result.Conflicts.Count.ShouldBe(2);
			result.Conflicts.ShouldContain(c => c.ProjectPath == "src/A/A.csproj" && c.OverrideVersion == "3.0.0");
			result.Conflicts.ShouldContain(c => c.ProjectPath == "src/B/B.csproj" && c.OverrideVersion == "3.1.0");
		}

		// ── Migrate (per-project) ────────────────────────────────────

		[Test]
		public void WhenPerProjectThenEachProjectGetsOwnPropsFile()
		{
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "Newtonsoft.Json",
					Version: "13.0.3",
					ProjectPath: "src/App1/App1.csproj",
					RepositoryName: "MyRepo",
					ProjectName: "App1",
					LineNumber: 5),
				new(
					PackageName: "Serilog",
					Version: "3.1.1",
					ProjectPath: "src/App2/App2.csproj",
					RepositoryName: "MyRepo",
					ProjectName: "App2",
					LineNumber: 5)
			};

			var projectContents = new Dictionary<string, string>
			{
				["src/App1/App1.csproj"] = """
					<Project Sdk="Microsoft.NET.Sdk">
					  <ItemGroup>
					    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
					  </ItemGroup>
					</Project>
					""",
				["src/App2/App2.csproj"] = """
					<Project Sdk="Microsoft.NET.Sdk">
					  <ItemGroup>
					    <PackageReference Include="Serilog" Version="3.1.1" />
					  </ItemGroup>
					</Project>
					"""
			};

			var result = CpmMigrationGenerator.Migrate(references, projectContents, perProject: true);

			// Should have 4 file changes: 2 props + 2 modified projects
			result.FileChanges.Count.ShouldBe(4);

			var props1 = result.FileChanges.First(f => f.FilePath == "src/App1/Directory.Packages.props");
			props1.IsNew.ShouldBeTrue();
			props1.Content.ShouldContain("Newtonsoft.Json");
			props1.Content.ShouldNotContain("Serilog");

			var props2 = result.FileChanges.First(f => f.FilePath == "src/App2/Directory.Packages.props");
			props2.IsNew.ShouldBeTrue();
			props2.Content.ShouldContain("Serilog");
			props2.Content.ShouldNotContain("Newtonsoft.Json");

			// No conflicts for per-project mode
			result.Conflicts.ShouldBeEmpty();
		}

		[Test]
		public void WhenPerProjectWithSameVersionsDifferentProjectsThenNoConflicts()
		{
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "Newtonsoft.Json",
					Version: "13.0.3",
					ProjectPath: "src/App1/App1.csproj",
					RepositoryName: "MyRepo",
					ProjectName: "App1",
					LineNumber: 5),
				new(
					PackageName: "Newtonsoft.Json",
					Version: "12.0.0",
					ProjectPath: "src/App2/App2.csproj",
					RepositoryName: "MyRepo",
					ProjectName: "App2",
					LineNumber: 5)
			};

			var projectContents = new Dictionary<string, string>
			{
				["src/App1/App1.csproj"] = "<Project><ItemGroup><PackageReference Include=\"Newtonsoft.Json\" Version=\"13.0.3\" /></ItemGroup></Project>",
				["src/App2/App2.csproj"] = "<Project><ItemGroup><PackageReference Include=\"Newtonsoft.Json\" Version=\"12.0.0\" /></ItemGroup></Project>"
			};

			var result = CpmMigrationGenerator.Migrate(references, projectContents, perProject: true);

			// Per-project: each project gets its own version, no conflicts
			result.Conflicts.ShouldBeEmpty();

			var props1 = result.FileChanges.First(f => f.FilePath == "src/App1/Directory.Packages.props");
			props1.Content.ShouldContain("Version=\"13.0.3\"");

			var props2 = result.FileChanges.First(f => f.FilePath == "src/App2/Directory.Packages.props");
			props2.Content.ShouldContain("Version=\"12.0.0\"");
		}

		[Test]
		public void WhenProjectAtRootLevelThenPropsFileIsAtRoot()
		{
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "xunit",
					Version: "2.9.0",
					ProjectPath: "App.csproj",
					RepositoryName: "MyRepo",
					ProjectName: "App",
					LineNumber: 3)
			};

			var projectContents = new Dictionary<string, string>
			{
				["App.csproj"] = "<Project><ItemGroup><PackageReference Include=\"xunit\" Version=\"2.9.0\" /></ItemGroup></Project>"
			};

			var result = CpmMigrationGenerator.Migrate(references, projectContents, perProject: true);

			var propsChange = result.FileChanges.First(f => f.FilePath == "Directory.Packages.props");
			propsChange.IsNew.ShouldBeTrue();
		}

		// ── Edge cases ───────────────────────────────────────────────

		[Test]
		public void WhenProjectContentNotAvailableThenSkipped()
		{
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "Newtonsoft.Json",
					Version: "13.0.3",
					ProjectPath: "src/App/App.csproj",
					RepositoryName: "MyRepo",
					ProjectName: "App",
					LineNumber: 5)
			};

			// No content for the project
			var projectContents = new Dictionary<string, string>();

			var result = CpmMigrationGenerator.Migrate(references, projectContents, perProject: false);

			// Props file should still be generated
			var propsChange = result.FileChanges.First(f => f.FilePath == "Directory.Packages.props");
			propsChange.Content.ShouldContain("Newtonsoft.Json");

			// No project file change since content was not available
			result.FileChanges.ShouldNotContain(f => f.FilePath == "src/App/App.csproj");
		}

		[Test]
		public void WhenReferencesFromPackagesConfigThenSkipped()
		{
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "Newtonsoft.Json",
					Version: "13.0.3",
					ProjectPath: "src/App/App.csproj",
					RepositoryName: "MyRepo",
					ProjectName: "App",
					LineNumber: 5,
					SourceKind: PackageSourceKind.PackagesConfig)
			};

			var projectContents = new Dictionary<string, string>();

			var result = CpmMigrationGenerator.Migrate(references, projectContents, perProject: false);

			result.FileChanges.ShouldBeEmpty();
			result.Conflicts.ShouldBeEmpty();
		}

		[Test]
		public void WhenSameVersionAcrossProjectsThenNoConflict()
		{
			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new(
					PackageName: "Newtonsoft.Json",
					Version: "13.0.3",
					ProjectPath: "src/App1/App1.csproj",
					RepositoryName: "MyRepo",
					ProjectName: "App1",
					LineNumber: 5),
				new(
					PackageName: "Newtonsoft.Json",
					Version: "13.0.3",
					ProjectPath: "src/App2/App2.csproj",
					RepositoryName: "MyRepo",
					ProjectName: "App2",
					LineNumber: 5)
			};

			var projectContents = new Dictionary<string, string>
			{
				["src/App1/App1.csproj"] = "<Project><ItemGroup><PackageReference Include=\"Newtonsoft.Json\" Version=\"13.0.3\" /></ItemGroup></Project>",
				["src/App2/App2.csproj"] = "<Project><ItemGroup><PackageReference Include=\"Newtonsoft.Json\" Version=\"13.0.3\" /></ItemGroup></Project>"
			};

			var result = CpmMigrationGenerator.Migrate(references, projectContents, perProject: false);

			result.Conflicts.ShouldBeEmpty();

			var propsChange = result.FileChanges.First(f => f.FilePath == "Directory.Packages.props");
			propsChange.Content.ShouldContain("Version=\"13.0.3\"");
		}
	}
}
