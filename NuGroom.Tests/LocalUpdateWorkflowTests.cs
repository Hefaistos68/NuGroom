using NuGroom.Configuration;
using NuGroom.Nuget;
using NuGroom.Workflows;

using Shouldly;

namespace NuGroom.Tests
{
	[TestFixture]
	public class LocalUpdateWorkflowTests
	{
		private string _tempDir = string.Empty;

		[SetUp]
		public void SetUp()
		{
			_tempDir = Path.Combine(Path.GetTempPath(), $"NuGroomUpdateTests_{Guid.NewGuid():N}");
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

		private static PackageReferenceExtractor.PackageReference MakeRef(
			string packageName, string version, string latestVersion, string repoName, string projectPath)
		{
			var info = new NuGetPackageResolver.PackageInfo(
				PackageName: packageName,
				ExistsOnNuGetOrg: true,
				PackageUrl: null,
				Description: null,
				Authors: null,
				ProjectUrl: null,
				Published: null,
				DownloadCount: null,
				LicenseUrl: null,
				IconUrl: null,
				Tags: [],
				IsDeprecated: false,
				DeprecationMessage: null,
				SourceProjects: [],
				LatestVersion: latestVersion);

			return new PackageReferenceExtractor.PackageReference(
				PackageName: packageName,
				Version: version,
				ProjectPath: projectPath,
				RepositoryName: repoName,
				ProjectName: "local",
				LineNumber: 1,
				NuGetInfo: info);
		}

		[Test]
		public void WhenDryRunThenFileIsNotModified()
		{
			var csprojContent = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
					<PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
				  </ItemGroup>
				</Project>
				""";

			var projectPath = Path.Combine(_tempDir, "MyApp.csproj");
			File.WriteAllText(projectPath, csprojContent);

			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				MakeRef("Newtonsoft.Json", "13.0.1", "13.0.3", Path.GetFileName(_tempDir), projectPath)
			};

			var updateConfig = new UpdateConfig
			{
				IsRequested = true,
				DryRun      = true,
				Scope       = UpdateScope.Patch
			};

			LocalUpdateWorkflow.Execute(references, updateConfig);

			var result = File.ReadAllText(projectPath);

			result.ShouldContain("13.0.1");
			result.ShouldNotContain("13.0.3");
		}

		[Test]
		public void WhenUpdateRequestedThenFileIsModified()
		{
			var csprojContent = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
					<PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
				  </ItemGroup>
				</Project>
				""";

			var projectPath = Path.Combine(_tempDir, "MyApp.csproj");
			File.WriteAllText(projectPath, csprojContent);

			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				MakeRef("Newtonsoft.Json", "13.0.1", "13.0.3", Path.GetFileName(_tempDir), projectPath)
			};

			var updateConfig = new UpdateConfig
			{
				IsRequested = true,
				DryRun      = false,
				Scope       = UpdateScope.Patch
			};

			LocalUpdateWorkflow.Execute(references, updateConfig);

			var result = File.ReadAllText(projectPath);

			result.ShouldNotContain("13.0.1");
			result.ShouldContain("13.0.3");
		}

		[Test]
		public void WhenNotRequestedThenFileIsNotModified()
		{
			var csprojContent = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
					<PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
				  </ItemGroup>
				</Project>
				""";

			var projectPath = Path.Combine(_tempDir, "MyApp.csproj");
			File.WriteAllText(projectPath, csprojContent);

			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				MakeRef("Newtonsoft.Json", "13.0.1", "13.0.3", Path.GetFileName(_tempDir), projectPath)
			};

			var updateConfig = new UpdateConfig
			{
				IsRequested = false,
				DryRun      = false,
				Scope       = UpdateScope.Patch
			};

			LocalUpdateWorkflow.Execute(references, updateConfig);

			var result = File.ReadAllText(projectPath);

			result.ShouldContain("13.0.1");
		}

		[Test]
		public void WhenFileNotFoundThenOtherFilesStillUpdate()
		{
			var csprojContent = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
					<PackageReference Include="Serilog" Version="3.0.0" />
				  </ItemGroup>
				</Project>
				""";

			var existingPath = Path.Combine(_tempDir, "Exists.csproj");
			var missingPath = Path.Combine(_tempDir, "Missing.csproj");
			File.WriteAllText(existingPath, csprojContent);
			var repoName = Path.GetFileName(_tempDir);

			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				MakeRef("Serilog", "3.0.0", "3.1.1", repoName, missingPath),
				MakeRef("Serilog", "3.0.0", "3.1.1", repoName, existingPath)
			};

			var updateConfig = new UpdateConfig
			{
				IsRequested = true,
				DryRun      = false,
				Scope       = UpdateScope.Minor
			};

			LocalUpdateWorkflow.Execute(references, updateConfig);

			var result = File.ReadAllText(existingPath);

			result.ShouldContain("3.1.1");
			result.ShouldNotContain("3.0.0");
		}

		[Test]
		public void WhenMultiplePackagesThenAllAreUpdated()
		{
			var csprojContent = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
					<PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
					<PackageReference Include="Serilog" Version="3.0.0" />
				  </ItemGroup>
				</Project>
				""";

			var projectPath = Path.Combine(_tempDir, "MyApp.csproj");
			File.WriteAllText(projectPath, csprojContent);
			var repoName = Path.GetFileName(_tempDir);

			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				MakeRef("Newtonsoft.Json", "13.0.1", "13.0.3", repoName, projectPath),
				MakeRef("Serilog", "3.0.0", "3.1.1", repoName, projectPath)
			};

			var updateConfig = new UpdateConfig
			{
				IsRequested = true,
				DryRun      = false,
				Scope       = UpdateScope.Minor
			};

			LocalUpdateWorkflow.Execute(references, updateConfig);

			var result = File.ReadAllText(projectPath);

			result.ShouldContain("13.0.3");
			result.ShouldContain("3.1.1");
			result.ShouldNotContain("13.0.1");
			result.ShouldNotContain("3.0.0");
		}
	}
}
