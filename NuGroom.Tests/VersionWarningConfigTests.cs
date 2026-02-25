using NuGroom.Configuration;

using Shouldly;

namespace NuGroom.Tests
{
	[TestFixture]
	public class VersionWarningConfigTests
	{
		[Test]
		public void WhenNoRulesConfiguredThenGetLevelForPackageReturnsDefaultLevel()
		{
			var config = new VersionWarningConfig { DefaultLevel = VersionWarningLevel.Minor };

			config.GetLevelForPackage("Newtonsoft.Json").ShouldBe(VersionWarningLevel.Minor);
		}

		[Test]
		public void WhenPackageRuleExistsThenGetLevelForPackageReturnsRuleLevel()
		{
			var config = new VersionWarningConfig
			{
				DefaultLevel = VersionWarningLevel.Minor,
				PackageRules = new List<PackageWarningRule>
				{
					new() { PackageName = "Newtonsoft.Json", Level = VersionWarningLevel.Major }
				}
			};

			config.GetLevelForPackage("Newtonsoft.Json").ShouldBe(VersionWarningLevel.Major);
		}

		[Test]
		public void WhenPackageRuleExistsWithDifferentCaseThenMatchesCaseInsensitive()
		{
			var config = new VersionWarningConfig
			{
				DefaultLevel = VersionWarningLevel.None,
				PackageRules = new List<PackageWarningRule>
				{
					new() { PackageName = "Newtonsoft.Json", Level = VersionWarningLevel.Major }
				}
			};

			config.GetLevelForPackage("newtonsoft.json").ShouldBe(VersionWarningLevel.Major);
		}

		[Test]
		public void WhenNoMatchingRuleThenGetLevelForPackageReturnsDefault()
		{
			var config = new VersionWarningConfig
			{
				DefaultLevel = VersionWarningLevel.Patch,
				PackageRules = new List<PackageWarningRule>
				{
					new() { PackageName = "Serilog", Level = VersionWarningLevel.None }
				}
			};

			config.GetLevelForPackage("Newtonsoft.Json").ShouldBe(VersionWarningLevel.Patch);
		}

		[Test]
		public void WhenDefaultLevelIsNoneThenGetLevelForPackageReturnsNone()
		{
			var config = new VersionWarningConfig();

			config.GetLevelForPackage("Anything").ShouldBe(VersionWarningLevel.None);
		}

		[Test]
		public void WhenPackageRulesIsNullThenGetLevelForPackageReturnsDefault()
		{
			var config = new VersionWarningConfig
			{
				DefaultLevel = VersionWarningLevel.Patch,
				PackageRules = null
			};

			config.GetLevelForPackage("Anything").ShouldBe(VersionWarningLevel.Patch);
		}
	}
}
