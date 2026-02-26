using NuGroom.Configuration;

using Shouldly;

namespace NuGroom.Tests
{
	[TestFixture]
	public class ProjectVersionIncrementerTests
	{
		[Test]
		public void WhenPatchScopeThenIncrementVersionIncrementsPatch()
		{
			var result = ProjectVersionIncrementer.IncrementVersion("1.2.3", VersionIncrementScope.Patch);

			result.ShouldBe("1.2.4");
		}

		[Test]
		public void WhenMinorScopeThenIncrementVersionIncrementsMinorAndResetsPatch()
		{
			var result = ProjectVersionIncrementer.IncrementVersion("1.2.3", VersionIncrementScope.Minor);

			result.ShouldBe("1.3.0");
		}

		[Test]
		public void WhenMajorScopeThenIncrementVersionIncrementsMajorAndResetsMinorAndPatch()
		{
			var result = ProjectVersionIncrementer.IncrementVersion("1.2.3", VersionIncrementScope.Major);

			result.ShouldBe("2.0.0");
		}

		[Test]
		public void WhenFourPartVersionWithPatchScopeThenIncrementsThirdSegment()
		{
			var result = ProjectVersionIncrementer.IncrementVersion("1.2.3.4", VersionIncrementScope.Patch);

			result.ShouldBe("1.2.4.0");
		}

		[Test]
		public void WhenFourPartVersionWithMinorScopeThenIncrementsMinorAndResetsLower()
		{
			var result = ProjectVersionIncrementer.IncrementVersion("1.2.3.4", VersionIncrementScope.Minor);

			result.ShouldBe("1.3.0.0");
		}

		[Test]
		public void WhenFourPartVersionWithMajorScopeThenIncrementsMajorAndResetsAll()
		{
			var result = ProjectVersionIncrementer.IncrementVersion("1.2.3.4", VersionIncrementScope.Major);

			result.ShouldBe("2.0.0.0");
		}

		[Test]
		public void WhenNullVersionThenIncrementVersionReturnsNull()
		{
			var result = ProjectVersionIncrementer.IncrementVersion(null!, VersionIncrementScope.Patch);

			result.ShouldBeNull();
		}

		[Test]
		public void WhenEmptyVersionThenIncrementVersionReturnsNull()
		{
			var result = ProjectVersionIncrementer.IncrementVersion("", VersionIncrementScope.Patch);

			result.ShouldBeNull();
		}

		[Test]
		public void WhenTwoPartVersionThenIncrementVersionReturnsNull()
		{
			var result = ProjectVersionIncrementer.IncrementVersion("1.2", VersionIncrementScope.Patch);

			result.ShouldBeNull();
		}

		[Test]
		public void WhenNonNumericVersionThenIncrementVersionReturnsNull()
		{
			var result = ProjectVersionIncrementer.IncrementVersion("1.2.beta", VersionIncrementScope.Patch);

			result.ShouldBeNull();
		}

		[Test]
		public void WhenVersionPropertyExistsThenIncrementPropertyUpdatesIt()
		{
			var csproj = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <PropertyGroup>
				    <Version>1.0.0</Version>
				  </PropertyGroup>
				</Project>
				""";

			var result = ProjectVersionIncrementer.IncrementProperty(csproj, "Version", VersionIncrementScope.Patch);

			result.ShouldContain("<Version>1.0.1</Version>");
		}

		[Test]
		public void WhenAssemblyVersionPropertyExistsThenIncrementPropertyUpdatesIt()
		{
			var csproj = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <PropertyGroup>
				    <AssemblyVersion>2.3.1.0</AssemblyVersion>
				  </PropertyGroup>
				</Project>
				""";

			var result = ProjectVersionIncrementer.IncrementProperty(csproj, "AssemblyVersion", VersionIncrementScope.Minor);

			result.ShouldContain("<AssemblyVersion>2.4.0.0</AssemblyVersion>");
		}

		[Test]
		public void WhenPropertyDoesNotExistThenIncrementPropertyReturnsUnchanged()
		{
			var csproj = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <PropertyGroup>
				    <TargetFramework>net10.0</TargetFramework>
				  </PropertyGroup>
				</Project>
				""";

			var result = ProjectVersionIncrementer.IncrementProperty(csproj, "Version", VersionIncrementScope.Patch);

			result.ShouldBe(csproj);
		}

		[Test]
		public void WhenConfigDisabledThenApplyVersionIncrementsReturnsUnchanged()
		{
			var csproj = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <PropertyGroup>
				    <Version>1.0.0</Version>
				  </PropertyGroup>
				</Project>
				""";
			var config = new VersionIncrementConfig();

			var result = ProjectVersionIncrementer.ApplyVersionIncrements(csproj, config);

			result.ShouldBe(csproj);
		}

		[Test]
		public void WhenIncrementVersionEnabledThenApplyVersionIncrementsUpdatesVersion()
		{
			var csproj = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <PropertyGroup>
				    <Version>1.0.0</Version>
				  </PropertyGroup>
				</Project>
				""";
			var config = new VersionIncrementConfig { IncrementVersion = true };

			var result = ProjectVersionIncrementer.ApplyVersionIncrements(csproj, config);

			result.ShouldContain("<Version>1.0.1</Version>");
		}

		[Test]
		public void WhenIncrementAllEnabledThenApplyVersionIncrementsUpdatesAllProperties()
		{
			var csproj = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <PropertyGroup>
				    <Version>1.2.3</Version>
				    <AssemblyVersion>1.2.3.0</AssemblyVersion>
				    <FileVersion>1.2.3.0</FileVersion>
				  </PropertyGroup>
				</Project>
				""";
			var config = new VersionIncrementConfig();
			config.EnableAll();

			var result = ProjectVersionIncrementer.ApplyVersionIncrements(csproj, config);

			result.ShouldContain("<Version>1.2.4</Version>");
			result.ShouldContain("<AssemblyVersion>1.2.4.0</AssemblyVersion>");
			result.ShouldContain("<FileVersion>1.2.4.0</FileVersion>");
		}

		[Test]
		public void WhenMajorScopeAndAllEnabledThenApplyVersionIncrementsIncrementsMajorOnAll()
		{
			var csproj = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <PropertyGroup>
				    <Version>1.2.3</Version>
				    <AssemblyVersion>1.2.3.0</AssemblyVersion>
				    <FileVersion>1.2.3.0</FileVersion>
				  </PropertyGroup>
				</Project>
				""";
			var config = new VersionIncrementConfig { Scope = VersionIncrementScope.Major };
			config.EnableAll();

			var result = ProjectVersionIncrementer.ApplyVersionIncrements(csproj, config);

			result.ShouldContain("<Version>2.0.0</Version>");
			result.ShouldContain("<AssemblyVersion>2.0.0.0</AssemblyVersion>");
			result.ShouldContain("<FileVersion>2.0.0.0</FileVersion>");
		}

		[Test]
		public void WhenOnlyFileVersionEnabledThenApplyVersionIncrementsLeavesOthersUnchanged()
		{
			var csproj = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <PropertyGroup>
				    <Version>1.2.3</Version>
				    <AssemblyVersion>1.2.3.0</AssemblyVersion>
				    <FileVersion>1.2.3.0</FileVersion>
				  </PropertyGroup>
				</Project>
				""";
			var config = new VersionIncrementConfig { IncrementFileVersion = true };

			var result = ProjectVersionIncrementer.ApplyVersionIncrements(csproj, config);

			result.ShouldContain("<Version>1.2.3</Version>");
			result.ShouldContain("<AssemblyVersion>1.2.3.0</AssemblyVersion>");
			result.ShouldContain("<FileVersion>1.2.4.0</FileVersion>");
		}

		[Test]
		public void WhenVersionZeroThenIncrementVersionWorksCorrectly()
		{
			var result = ProjectVersionIncrementer.IncrementVersion("0.0.0", VersionIncrementScope.Patch);

			result.ShouldBe("0.0.1");
		}

		[Test]
		public void WhenMinorScopeThenIncrementPropertyUpdatesInPlace()
		{
			var csproj = """
				<Project Sdk="Microsoft.NET.Sdk">
				  <PropertyGroup>
				    <Version>3.5.2</Version>
				    <TargetFramework>net10.0</TargetFramework>
				  </PropertyGroup>
				</Project>
				""";

			var result = ProjectVersionIncrementer.IncrementProperty(csproj, "Version", VersionIncrementScope.Minor);

			result.ShouldContain("<Version>3.6.0</Version>");
			result.ShouldContain("<TargetFramework>net10.0</TargetFramework>");
		}
	}
}
