using NuGroom.Configuration;

using Shouldly;

namespace NuGroom.Tests
{
	[TestFixture]
	public class RenovateConfigReaderTests
	{
		[Test]
		public void WhenJsonHasIgnoreDepsThenParsesIgnoredPackages()
		{
			var json = """
				{
				  "ignoreDeps": ["Newtonsoft.Json", "Castle.Core"]
				}
				""";

			var result = RenovateConfigReader.Parse(json, "TestRepo");

			result.IgnoredPackages.Count.ShouldBe(2);
			result.IgnoredPackages.ShouldContain("Newtonsoft.Json");
			result.IgnoredPackages.ShouldContain("Castle.Core");
		}

		[Test]
		public void WhenJsonHasReviewersThenParsesReviewersList()
		{
			var json = """
				{
				  "reviewers": ["lead@company.com", "dev@company.com"]
				}
				""";

			var result = RenovateConfigReader.Parse(json, "TestRepo");

			result.Reviewers.ShouldNotBeNull();
			result.Reviewers!.Count.ShouldBe(2);
			result.Reviewers.ShouldContain("lead@company.com");
		}

		[Test]
		public void WhenJsonHasDisabledPackageRuleThenParsesDisabledPackages()
		{
			var json = """
				{
				  "packageRules": [
				    {
				      "matchPackageNames": ["Serilog", "Serilog.Sinks.Console"],
				      "enabled": false
				    }
				  ]
				}
				""";

			var result = RenovateConfigReader.Parse(json, "TestRepo");

			result.DisabledPackages.Count.ShouldBe(2);
			result.DisabledPackages.ShouldContain("Serilog");
			result.DisabledPackages.ShouldContain("Serilog.Sinks.Console");
		}

		[Test]
		public void WhenJsonHasEnabledPackageRuleThenDoesNotAddToDisabled()
		{
			var json = """
				{
				  "packageRules": [
				    {
				      "matchPackageNames": ["Serilog"],
				      "enabled": true
				    }
				  ]
				}
				""";

			var result = RenovateConfigReader.Parse(json, "TestRepo");

			result.DisabledPackages.ShouldBeEmpty();
		}

		[Test]
		public void WhenJsonHasPackageRuleReviewersThenParsesPerPackageReviewers()
		{
			var json = """
				{
				  "packageRules": [
				    {
				      "matchPackageNames": ["Serilog"],
				      "reviewers": ["security@company.com"]
				    }
				  ]
				}
				""";

			var result = RenovateConfigReader.Parse(json, "TestRepo");

			result.PackageReviewers.ShouldContainKey("Serilog");
			result.PackageReviewers["Serilog"].ShouldContain("security@company.com");
		}

		[Test]
		public void WhenJsonIsEmptyObjectThenReturnsEmptyOverrides()
		{
			var json = "{}";

			var result = RenovateConfigReader.Parse(json, "TestRepo");

			result.IgnoredPackages.ShouldBeEmpty();
			result.DisabledPackages.ShouldBeEmpty();
			result.Reviewers.ShouldBeNull();
			result.PackageReviewers.ShouldBeEmpty();
		}

		[Test]
		public void WhenJsonIsInvalidThenReturnsEmptyOverrides()
		{
			var json = "not valid json {{{";

			var result = RenovateConfigReader.Parse(json, "TestRepo");

			result.IgnoredPackages.ShouldBeEmpty();
			result.DisabledPackages.ShouldBeEmpty();
		}

		[Test]
		public void WhenJsonHasTrailingCommasThenParsesSuccessfully()
		{
			var json = """
				{
				  "ignoreDeps": ["Newtonsoft.Json",],
				}
				""";

			var result = RenovateConfigReader.Parse(json, "TestRepo");

			result.IgnoredPackages.Count.ShouldBe(1);
		}

		[Test]
		public void WhenJsonHasCommentsThenParsesSuccessfully()
		{
			var json = """
				{
				  // This is a comment
				  "ignoreDeps": ["Newtonsoft.Json"]
				}
				""";

			var result = RenovateConfigReader.Parse(json, "TestRepo");

			result.IgnoredPackages.Count.ShouldBe(1);
		}

		[Test]
		public void WhenPackageIsInIgnoredDepsThenIsPackageExcludedReturnsTrue()
		{
			var overrides = new RenovateOverrides(
				new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Newtonsoft.Json" },
				new HashSet<string>(StringComparer.OrdinalIgnoreCase),
				null,
				new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase));

			RenovateConfigReader.IsPackageExcluded("Newtonsoft.Json", overrides).ShouldBeTrue();
		}

		[Test]
		public void WhenPackageIsInDisabledPackagesThenIsPackageExcludedReturnsTrue()
		{
			var overrides = new RenovateOverrides(
				new HashSet<string>(StringComparer.OrdinalIgnoreCase),
				new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Serilog" },
				null,
				new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase));

			RenovateConfigReader.IsPackageExcluded("Serilog", overrides).ShouldBeTrue();
		}

		[Test]
		public void WhenPackageIsNotExcludedThenIsPackageExcludedReturnsFalse()
		{
			var overrides = new RenovateOverrides(
				new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Newtonsoft.Json" },
				new HashSet<string>(StringComparer.OrdinalIgnoreCase),
				null,
				new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase));

			RenovateConfigReader.IsPackageExcluded("Serilog", overrides).ShouldBeFalse();
		}

		[Test]
		public void WhenIgnoredDepsMatchesCaseInsensitivelyThenIsPackageExcludedReturnsTrue()
		{
			var overrides = new RenovateOverrides(
				new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Newtonsoft.Json" },
				new HashSet<string>(StringComparer.OrdinalIgnoreCase),
				null,
				new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase));

			RenovateConfigReader.IsPackageExcluded("newtonsoft.json", overrides).ShouldBeTrue();
		}

		[Test]
		public void WhenJsonHasCombinedConfigThenParsesAllFields()
		{
			var json = """
				{
				  "$schema": "https://docs.renovatebot.com/renovate-schema.json",
				  "ignoreDeps": ["Castle.Core"],
				  "reviewers": ["lead@company.com"],
				  "packageRules": [
				    {
				      "matchPackageNames": ["Serilog"],
				      "enabled": false
				    }
				  ]
				}
				""";

			var result = RenovateConfigReader.Parse(json, "TestRepo");

			result.IgnoredPackages.Count.ShouldBe(1);
			result.DisabledPackages.Count.ShouldBe(1);
			result.Reviewers.ShouldNotBeNull();
			result.Reviewers!.Count.ShouldBe(1);
		}
	}
}
