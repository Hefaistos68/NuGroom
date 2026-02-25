using NuGroom.Configuration;

using Shouldly;

namespace NuGroom.Tests
{
	[TestFixture]
	public class SyncConfigTests
	{
		[Test]
		public void WhenCreatedWithPackageNameAndVersionThenStoresBoth()
		{
			var config = new SyncConfig("Newtonsoft.Json", "13.0.1");

			config.PackageName.ShouldBe("Newtonsoft.Json");
			config.TargetVersion.ShouldBe("13.0.1");
		}

		[Test]
		public void WhenCreatedWithPackageNameOnlyThenTargetVersionIsNull()
		{
			var config = new SyncConfig("Newtonsoft.Json", null);

			config.PackageName.ShouldBe("Newtonsoft.Json");
			config.TargetVersion.ShouldBeNull();
		}
	}

	[TestFixture]
	public class UpdateConfigTests
	{
		[Test]
		public void WhenDefaultsThenScopeIsPatch()
		{
			var config = new UpdateConfig();

			config.Scope.ShouldBe(UpdateScope.Patch);
		}

		[Test]
		public void WhenDefaultsThenDryRunIsTrue()
		{
			var config = new UpdateConfig();

			config.DryRun.ShouldBeTrue();
		}

		[Test]
		public void WhenDefaultsThenTargetBranchPatternIsDevelopStar()
		{
			var config = new UpdateConfig();

			config.TargetBranchPattern.ShouldBe("develop/*");
		}

		[Test]
		public void WhenDefaultsThenSourceBranchPatternIsNull()
		{
			var config = new UpdateConfig();

			config.SourceBranchPattern.ShouldBeNull();
		}

		[Test]
		public void WhenDefaultsThenFeatureBranchNameHasExpectedPrefix()
		{
			var config = new UpdateConfig();

			config.FeatureBranchName.ShouldBe("nugroom/update-nuget-references");
		}

		[Test]
		public void WhenReviewersAreDisjointThenValidateReviewersDoesNotThrow()
		{
			var config = new UpdateConfig
			{
				RequiredReviewers = new List<string> { "lead@company.com" },
				OptionalReviewers = new List<string> { "dev@company.com" }
			};

			Should.NotThrow(() => config.ValidateReviewers());
		}

		[Test]
		public void WhenReviewersOverlapThenValidateReviewersThrowsInvalidOperationException()
		{
			var config = new UpdateConfig
			{
				RequiredReviewers = new List<string> { "lead@company.com" },
				OptionalReviewers = new List<string> { "lead@company.com" }
			};

			Should.Throw<InvalidOperationException>(() => config.ValidateReviewers());
		}

		[Test]
		public void WhenRequiredReviewersIsNullThenValidateReviewersDoesNotThrow()
		{
			var config = new UpdateConfig
			{
				RequiredReviewers = null,
				OptionalReviewers = new List<string> { "dev@company.com" }
			};

			Should.NotThrow(() => config.ValidateReviewers());
		}

		[Test]
		public void WhenOptionalReviewersIsNullThenValidateReviewersDoesNotThrow()
		{
			var config = new UpdateConfig
			{
				RequiredReviewers = new List<string> { "lead@company.com" },
				OptionalReviewers = null
			};

			Should.NotThrow(() => config.ValidateReviewers());
		}
	}
}
