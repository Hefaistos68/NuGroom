using NuGroom.Nuget;

using Shouldly;

namespace NuGroom.Tests
{
	[TestFixture]
	public class ExclusionListTests
	{
		[Test]
		public void WhenCreatedWithDefaultsThenDoesNotExcludeArbitraryPackage()
		{
			var list = PackageReferenceExtractor.ExclusionList.CreateDefault();

			list.ShouldExclude("Newtonsoft.Json").ShouldBeFalse();
		}

		[Test]
		public void WhenCreatedEmptyThenExcludesNothing()
		{
			var list = PackageReferenceExtractor.ExclusionList.CreateEmpty();

			list.ShouldExclude("Microsoft.Extensions.Logging").ShouldBeFalse();
		}

		[Test]
		public void WhenAddingPrefixExclusionThenMatchesPackagesWithThatPrefix()
		{
			var list = PackageReferenceExtractor.ExclusionList.CreateEmpty();
			list.AddPrefixExclusion("MyCompany.");

			list.ShouldExclude("MyCompany.Core").ShouldBeTrue();
		}

		[Test]
		public void WhenAddingPrefixExclusionThenDoesNotMatchUnrelatedPackages()
		{
			var list = PackageReferenceExtractor.ExclusionList.CreateEmpty();
			list.AddPrefixExclusion("MyCompany.");

			list.ShouldExclude("OtherCompany.Core").ShouldBeFalse();
		}

		[Test]
		public void WhenAddingExactPackageExclusionThenMatchesExactName()
		{
			var list = PackageReferenceExtractor.ExclusionList.CreateEmpty();
			list.AddPackageExclusion("Newtonsoft.Json");

			list.ShouldExclude("Newtonsoft.Json").ShouldBeTrue();
		}

		[Test]
		public void WhenAddingExactPackageExclusionThenDoesNotMatchPartialName()
		{
			var list = PackageReferenceExtractor.ExclusionList.CreateEmpty();
			list.AddPackageExclusion("Newtonsoft.Json");

			list.ShouldExclude("Newtonsoft.Json.Schema").ShouldBeFalse();
		}

		[Test]
		public void WhenAddingPatternExclusionThenMatchesRegexPattern()
		{
			var list = PackageReferenceExtractor.ExclusionList.CreateEmpty();
			list.AddPatternExclusion(@".*\.Test.*");

			list.ShouldExclude("MyProject.Tests").ShouldBeTrue();
		}

		[Test]
		public void WhenAddingPatternExclusionThenDoesNotMatchNonMatchingPackages()
		{
			var list = PackageReferenceExtractor.ExclusionList.CreateEmpty();
			list.AddPatternExclusion(@".*\.Test.*");

			list.ShouldExclude("MyProject.Core").ShouldBeFalse();
		}

		[Test]
		public void WhenCaseInsensitiveThenMatchesRegardlessOfCase()
		{
			var list = PackageReferenceExtractor.ExclusionList.CreateEmpty();
			list.CaseSensitive = false;
			list.AddPackageExclusion("Newtonsoft.Json");

			list.ShouldExclude("newtonsoft.json").ShouldBeTrue();
		}

		[Test]
		public void WhenCaseSensitiveThenDoesNotMatchDifferentCase()
		{
			var list = PackageReferenceExtractor.ExclusionList.CreateEmpty();
			list.CaseSensitive = true;
			list.AddPackageExclusion("Newtonsoft.Json");

			list.ShouldExclude("newtonsoft.json").ShouldBeFalse();
		}

		[Test]
		public void WhenPackageNameIsNullThenShouldExcludeReturnsTrue()
		{
			var list = PackageReferenceExtractor.ExclusionList.CreateEmpty();

			list.ShouldExclude(null!).ShouldBeTrue();
		}

		[Test]
		public void WhenPackageNameIsEmptyThenShouldExcludeReturnsTrue()
		{
			var list = PackageReferenceExtractor.ExclusionList.CreateEmpty();

			list.ShouldExclude("").ShouldBeTrue();
		}

		[Test]
		public void WhenAddingDuplicatePrefixThenListDoesNotGrow()
		{
			var list = PackageReferenceExtractor.ExclusionList.CreateEmpty();
			list.AddPrefixExclusion("MyCompany.");
			list.AddPrefixExclusion("MyCompany.");

			list.ExcludedPrefixes.Count.ShouldBe(1);
		}
	}
}
