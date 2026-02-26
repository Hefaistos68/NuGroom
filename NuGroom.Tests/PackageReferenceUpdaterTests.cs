using NuGroom.Configuration;
using NuGroom.Nuget;

using Shouldly;

namespace NuGroom.Tests
{
	[TestFixture]
	public class PackageReferenceUpdaterTests
	{
		[Test]
		public void WhenPatchUpdateWithinScopeThenIsUpdateWithinScopeReturnsTrue()
		{
			var updater = new PackageReferenceUpdater(UpdateScope.Patch);

			updater.IsUpdateWithinScope("1.2.3", "1.2.5").ShouldBeTrue();
		}

		[Test]
		public void WhenMinorUpdateWithPatchScopeThenIsUpdateWithinScopeReturnsFalse()
		{
			var updater = new PackageReferenceUpdater(UpdateScope.Patch);

			updater.IsUpdateWithinScope("1.2.3", "1.3.0").ShouldBeFalse();
		}

		[Test]
		public void WhenMajorUpdateWithPatchScopeThenIsUpdateWithinScopeReturnsFalse()
		{
			var updater = new PackageReferenceUpdater(UpdateScope.Patch);

			updater.IsUpdateWithinScope("1.2.3", "2.0.0").ShouldBeFalse();
		}

		[Test]
		public void WhenMinorUpdateWithMinorScopeThenIsUpdateWithinScopeReturnsTrue()
		{
			var updater = new PackageReferenceUpdater(UpdateScope.Minor);

			updater.IsUpdateWithinScope("1.2.3", "1.4.0").ShouldBeTrue();
		}

		[Test]
		public void WhenMajorUpdateWithMinorScopeThenIsUpdateWithinScopeReturnsFalse()
		{
			var updater = new PackageReferenceUpdater(UpdateScope.Minor);

			updater.IsUpdateWithinScope("1.2.3", "2.0.0").ShouldBeFalse();
		}

		[Test]
		public void WhenMajorUpdateWithMajorScopeThenIsUpdateWithinScopeReturnsTrue()
		{
			var updater = new PackageReferenceUpdater(UpdateScope.Major);

			updater.IsUpdateWithinScope("1.2.3", "2.0.0").ShouldBeTrue();
		}

		[Test]
		public void WhenVersionsAreEqualThenIsUpdateWithinScopeReturnsFalse()
		{
			var updater = new PackageReferenceUpdater(UpdateScope.Major);

			updater.IsUpdateWithinScope("1.2.3", "1.2.3").ShouldBeFalse();
		}

		[Test]
		public void WhenCurrentVersionIsNewerThenIsUpdateWithinScopeReturnsFalse()
		{
			var updater = new PackageReferenceUpdater(UpdateScope.Major);

			updater.IsUpdateWithinScope("2.0.0", "1.2.3").ShouldBeFalse();
		}

		[Test]
		public void WhenInvalidVersionStringThenIsUpdateWithinScopeReturnsFalse()
		{
			var updater = new PackageReferenceUpdater(UpdateScope.Major);

			updater.IsUpdateWithinScope("not-a-version", "1.2.3").ShouldBeFalse();
		}

		[Test]
		public void WhenPackageIsPinnedThenIsPinnedReturnsTrue()
		{
			var pinned = new List<PinnedPackage>
			{
				new() { PackageName = "Newtonsoft.Json", Version = "13.0.1" }
			};
			var updater = new PackageReferenceUpdater(UpdateScope.Patch, pinned);

			updater.IsPinned("Newtonsoft.Json").ShouldBeTrue();
		}

		[Test]
		public void WhenPackageIsNotPinnedThenIsPinnedReturnsFalse()
		{
			var updater = new PackageReferenceUpdater(UpdateScope.Patch);

			updater.IsPinned("Newtonsoft.Json").ShouldBeFalse();
		}

		[Test]
		public void WhenPinnedWithDifferentCaseThenIsPinnedReturnsTrueCaseInsensitive()
		{
			var pinned = new List<PinnedPackage>
			{
				new() { PackageName = "Newtonsoft.Json", Version = "13.0.1" }
			};
			var updater = new PackageReferenceUpdater(UpdateScope.Patch, pinned);

			updater.IsPinned("newtonsoft.json").ShouldBeTrue();
		}

		[Test]
		public void WhenUpdateIsWithinScopeThenGetTargetVersionReturnsLatest()
		{
			var updater = new PackageReferenceUpdater(UpdateScope.Patch);

			updater.GetTargetVersion("1.2.3", "1.2.5").ShouldBe("1.2.5");
		}

		[Test]
		public void WhenUpdateIsOutOfScopeThenGetTargetVersionReturnsNull()
		{
			var updater = new PackageReferenceUpdater(UpdateScope.Patch);

			updater.GetTargetVersion("1.2.3", "2.0.0").ShouldBeNull();
		}

		[Test]
		public void WhenCurrentVersionIsNullThenGetTargetVersionReturnsNull()
		{
			var updater = new PackageReferenceUpdater(UpdateScope.Major);

			updater.GetTargetVersion(null, "1.2.3").ShouldBeNull();
		}

		[Test]
		public void WhenLatestVersionIsNullThenGetTargetVersionReturnsNull()
		{
			var updater = new PackageReferenceUpdater(UpdateScope.Major);

			updater.GetTargetVersion("1.2.3", null).ShouldBeNull();
		}

		[Test]
		public void WhenApplyUpdatesWithSingleUpdateThenReplacesVersion()
		{
			var csproj = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
				    <PackageReference Include="Newtonsoft.Json" Version="12.0.3" />
				  </ItemGroup>
				</Project>
				""";

			var updates = new List<PackageUpdate>
			{
				new("Newtonsoft.Json", "12.0.3", "13.0.3")
			};

			var result = PackageReferenceUpdater.ApplyUpdates(csproj, updates);

			result.ShouldContain("Version=\"13.0.3\"");
			result.ShouldNotContain("Version=\"12.0.3\"");
		}

		[Test]
		public void WhenApplyUpdatesWithMultipleUpdatesThenReplacesAll()
		{
			var csproj = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
				    <PackageReference Include="Newtonsoft.Json" Version="12.0.3" />
				    <PackageReference Include="Serilog" Version="2.10.0" />
				  </ItemGroup>
				</Project>
				""";

			var updates = new List<PackageUpdate>
			{
				new("Newtonsoft.Json", "12.0.3", "13.0.3"),
				new("Serilog", "2.10.0", "2.12.0")
			};

			var result = PackageReferenceUpdater.ApplyUpdates(csproj, updates);

			result.ShouldContain("Newtonsoft.Json\" Version=\"13.0.3\"");
			result.ShouldContain("Serilog\" Version=\"2.12.0\"");
		}

		[Test]
		public void WhenApplyUpdatesWithEmptyListThenReturnsUnchangedContent()
		{
			var csproj = "<Project></Project>";

			var result = PackageReferenceUpdater.ApplyUpdates(csproj, new List<PackageUpdate>());

			result.ShouldBe(csproj);
		}

		[Test]
		public void WhenApplyUpdatesWithNonMatchingPackageThenReturnsUnchangedContent()
		{
			var csproj = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
				    <PackageReference Include="Newtonsoft.Json" Version="12.0.3" />
				  </ItemGroup>
				</Project>
				""";

			var updates = new List<PackageUpdate>
			{
				new("Serilog", "2.10.0", "2.12.0")
			};

			var result = PackageReferenceUpdater.ApplyUpdates(csproj, updates);

			result.ShouldBe(csproj);
		}

		[Test]
		public void WhenApplyUpdatesWithSingleQuotesThenReplacesVersion()
		{
			var csproj = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
				    <PackageReference Include='Newtonsoft.Json' Version='12.0.3' />
				  </ItemGroup>
				</Project>
				""";

			var updates = new List<PackageUpdate>
			{
				new("Newtonsoft.Json", "12.0.3", "13.0.3")
			};

			var result = PackageReferenceUpdater.ApplyUpdates(csproj, updates);

			result.ShouldContain("Version='13.0.3'");
		}

		[Test]
		public void WhenLatestOutOfScopeWithAvailableVersionsThenGetTargetVersionReturnsInScopeVersion()
		{
			var updater = new PackageReferenceUpdater(UpdateScope.Minor);
			var availableVersions = new List<string> { "9.0.8", "9.0.13", "10.0.1", "10.0.3" };

			updater.GetTargetVersion("9.0.8", "10.0.3", availableVersions).ShouldBe("9.0.13");
		}

		[Test]
		public void WhenLatestOutOfScopeWithNoAvailableVersionsThenGetTargetVersionReturnsNull()
		{
			var updater = new PackageReferenceUpdater(UpdateScope.Minor);

			updater.GetTargetVersion("9.0.8", "10.0.3", null).ShouldBeNull();
		}

		[Test]
		public void WhenLatestWithinScopeThenGetTargetVersionReturnsLatestIgnoringAvailableVersions()
		{
			var updater = new PackageReferenceUpdater(UpdateScope.Minor);
			var availableVersions = new List<string> { "9.0.8", "9.0.13", "10.0.1", "10.0.3" };

			updater.GetTargetVersion("10.0.1", "10.0.3", availableVersions).ShouldBe("10.0.3");
		}

		[Test]
		public void WhenPatchScopeLatestOutOfScopeThenGetTargetVersionReturnsInScopePatch()
		{
			var updater = new PackageReferenceUpdater(UpdateScope.Patch);
			var availableVersions = new List<string> { "9.0.8", "9.0.13", "9.1.0", "10.0.3" };

			updater.GetTargetVersion("9.0.8", "10.0.3", availableVersions).ShouldBe("9.0.13");
		}

		[Test]
		public void WhenApplyCpmUpdatesWithSingleUpdateThenReplacesPackageVersion()
		{
			var props = """
				<Project>
				  <ItemGroup>
					<PackageVersion Include="Newtonsoft.Json" Version="12.0.3" />
				  </ItemGroup>
				</Project>
				""";

			var updates = new List<PackageUpdate>
			{
				new("Newtonsoft.Json", "12.0.3", "13.0.3")
			};

			var result = PackageReferenceUpdater.ApplyCpmUpdates(props, updates);

			result.ShouldContain("Version=\"13.0.3\"");
			result.ShouldNotContain("Version=\"12.0.3\"");
		}

		[Test]
		public void WhenApplyCpmUpdatesWithMultipleUpdatesThenReplacesAll()
		{
			var props = """
				<Project>
				  <ItemGroup>
					<PackageVersion Include="Newtonsoft.Json" Version="12.0.3" />
					<PackageVersion Include="Serilog" Version="2.10.0" />
				  </ItemGroup>
				</Project>
				""";

			var updates = new List<PackageUpdate>
			{
				new("Newtonsoft.Json", "12.0.3", "13.0.3"),
				new("Serilog", "2.10.0", "3.1.1")
			};

			var result = PackageReferenceUpdater.ApplyCpmUpdates(props, updates);

			result.ShouldContain("Newtonsoft.Json\" Version=\"13.0.3\"");
			result.ShouldContain("Serilog\" Version=\"3.1.1\"");
		}

		[Test]
		public void WhenApplyCpmUpdatesWithEmptyListThenReturnsUnchangedContent()
		{
			var props = "<Project></Project>";

			var result = PackageReferenceUpdater.ApplyCpmUpdates(props, new List<PackageUpdate>());

			result.ShouldBe(props);
		}

		[Test]
		public void WhenApplyCpmUpdatesDoesNotAffectPackageReferenceElements()
		{
			var props = """
				<Project>
				  <ItemGroup>
					<PackageReference Include="Newtonsoft.Json" Version="12.0.3" />
					<PackageVersion Include="Serilog" Version="2.10.0" />
				  </ItemGroup>
				</Project>
				""";

			var updates = new List<PackageUpdate>
			{
				new("Newtonsoft.Json", "12.0.3", "13.0.3"),
				new("Serilog", "2.10.0", "3.1.1")
			};

			var result = PackageReferenceUpdater.ApplyCpmUpdates(props, updates);

			// PackageVersion for Serilog should be updated
			result.ShouldContain("Serilog\" Version=\"3.1.1\"");
			// PackageReference for Newtonsoft.Json should NOT be updated by ApplyCpmUpdates
			result.ShouldContain("<PackageReference Include=\"Newtonsoft.Json\" Version=\"12.0.3\"");
		}

		[Test]
		public void WhenApplyPackagesConfigUpdatesWithSingleUpdateThenReplacesVersion()
		{
			var pkgConfig = """
				<?xml version="1.0" encoding="utf-8"?>
				<packages>
				  <package id="Newtonsoft.Json" version="12.0.3" targetFramework="net48" />
				</packages>
				""";

			var updates = new List<PackageUpdate>
			{
				new("Newtonsoft.Json", "12.0.3", "13.0.3")
			};

			var result = PackageReferenceUpdater.ApplyPackagesConfigUpdates(pkgConfig, updates);

			result.ShouldContain("version=\"13.0.3\"");
			result.ShouldNotContain("version=\"12.0.3\"");
		}

		[Test]
		public void WhenApplyPackagesConfigUpdatesWithMultipleUpdatesThenReplacesAll()
		{
			var pkgConfig = """
				<?xml version="1.0" encoding="utf-8"?>
				<packages>
				  <package id="Newtonsoft.Json" version="12.0.3" targetFramework="net48" />
				  <package id="Serilog" version="2.10.0" targetFramework="net48" />
				</packages>
				""";

			var updates = new List<PackageUpdate>
			{
				new("Newtonsoft.Json", "12.0.3", "13.0.3"),
				new("Serilog", "2.10.0", "3.1.1")
			};

			var result = PackageReferenceUpdater.ApplyPackagesConfigUpdates(pkgConfig, updates);

			result.ShouldContain("Newtonsoft.Json\" version=\"13.0.3\"");
			result.ShouldContain("Serilog\" version=\"3.1.1\"");
		}

		[Test]
		public void WhenApplyPackagesConfigUpdatesWithEmptyListThenReturnsUnchangedContent()
		{
			var pkgConfig = "<packages></packages>";

			var result = PackageReferenceUpdater.ApplyPackagesConfigUpdates(pkgConfig, new List<PackageUpdate>());

			result.ShouldBe(pkgConfig);
		}

		[Test]
		public void WhenApplyPackagesConfigUpdatesWithNonMatchingPackageThenReturnsUnchangedContent()
		{
			var pkgConfig = """
				<?xml version="1.0" encoding="utf-8"?>
				<packages>
				  <package id="Newtonsoft.Json" version="12.0.3" targetFramework="net48" />
				</packages>
				""";

			var updates = new List<PackageUpdate>
			{
				new("Serilog", "2.10.0", "3.1.1")
			};

			var result = PackageReferenceUpdater.ApplyPackagesConfigUpdates(pkgConfig, updates);

			result.ShouldBe(pkgConfig);
		}

		[Test]
		public void WhenApplyUpdatesWithVersionBeforeIncludeThenReplacesVersion()
		{
			var csproj = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <ItemGroup>
					<PackageReference Version="12.0.3" Include="Newtonsoft.Json" />
				  </ItemGroup>
				</Project>
				""";

			var updates = new List<PackageUpdate>
			{
				new("Newtonsoft.Json", "12.0.3", "13.0.3")
			};

			var result = PackageReferenceUpdater.ApplyUpdates(csproj, updates);

			result.ShouldContain("Version=\"13.0.3\"");
			result.ShouldNotContain("Version=\"12.0.3\"");
		}

		[Test]
		public void WhenApplyCpmUpdatesWithVersionBeforeIncludeThenReplacesVersion()
		{
			var props = """
				<Project>
				  <ItemGroup>
					<PackageVersion Version="12.0.3" Include="Newtonsoft.Json" />
				  </ItemGroup>
				</Project>
				""";

			var updates = new List<PackageUpdate>
			{
				new("Newtonsoft.Json", "12.0.3", "13.0.3")
			};

			var result = PackageReferenceUpdater.ApplyCpmUpdates(props, updates);

			result.ShouldContain("Version=\"13.0.3\"");
			result.ShouldNotContain("Version=\"12.0.3\"");
		}

		[Test]
		public void WhenApplyPackagesConfigUpdatesWithVersionBeforeIdThenReplacesVersion()
		{
			var pkgConfig = """
				<?xml version="1.0" encoding="utf-8"?>
				<packages>
				  <package version="12.0.3" id="Newtonsoft.Json" targetFramework="net48" />
				</packages>
				""";

			var updates = new List<PackageUpdate>
			{
				new("Newtonsoft.Json", "12.0.3", "13.0.3")
			};

			var result = PackageReferenceUpdater.ApplyPackagesConfigUpdates(pkgConfig, updates);

			result.ShouldContain("version=\"13.0.3\"");
			result.ShouldNotContain("version=\"12.0.3\"");
		}

		[Test]
		public void WhenCpmRefsSpanMultiplePropsFilesThenBuildUpdatePlansCreatesOneFileUpdatePerPropsFile()
		{
			var updater = new PackageReferenceUpdater(UpdateScope.Major);

			var nugetInfoA = new NuGetPackageResolver.PackageInfo(
				"PkgA", true, null, null, null, null, null, null, null, null,
				[], false, null, [], LatestVersion: "2.0.0");

			var nugetInfoB = new NuGetPackageResolver.PackageInfo(
				"PkgB", true, null, null, null, null, null, null, null, null,
				[], false, null, [], LatestVersion: "3.0.0");

			var references = new List<PackageReferenceExtractor.PackageReference>
			{
				new("PkgA", "1.0.0", "/src/ProjectA/ProjectA.csproj", "MyRepo", "ProjectA", 5,
					PackageSourceKind.CentralPackageManagement, nugetInfoA,
					CpmFilePath: "/Directory.Packages.props"),
				new("PkgB", "2.0.0", "/src/sub/ProjectB/ProjectB.csproj", "MyRepo", "ProjectB", 8,
					PackageSourceKind.CentralPackageManagement, nugetInfoB,
					CpmFilePath: "/src/sub/Directory.Packages.props")
			};

			var plans = updater.BuildUpdatePlans(references);

			plans.Count.ShouldBe(1);
			var fileUpdates = plans[0].FileUpdates;
			fileUpdates.Count.ShouldBe(2);
			fileUpdates[0].ProjectPath.ShouldBe("/Directory.Packages.props");
			fileUpdates[0].Updates.Count.ShouldBe(1);
			fileUpdates[0].Updates[0].PackageName.ShouldBe("PkgA");
			fileUpdates[1].ProjectPath.ShouldBe("/src/sub/Directory.Packages.props");
			fileUpdates[1].Updates.Count.ShouldBe(1);
			fileUpdates[1].Updates[0].PackageName.ShouldBe("PkgB");
		}
	}
}
