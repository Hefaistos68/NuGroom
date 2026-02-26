using NuGroom.Nuget;

using NUnit.Framework;
using Shouldly;

namespace NuGroom.Tests.Nuget
{
	[TestFixture]
	public class NuGetPackageResolverTests
	{
		[TestCase("2.0.0", "1.0.0", "major")]
		[TestCase("1.2.0", "1.1.0", "minor")]
		[TestCase("1.1.1", "1.1.0", "patch")]
		[TestCase("1.0.0", "1.0.0", "none")]
		[TestCase("1.0.0", null, "unknown")]
		[TestCase("invalid", "1.0.0", "unknown")]
		public void GetVersionDifference_ReturnsExpectedResult(string? v1, string? v2, string expected)
		{
			var result = NuGetPackageResolver.GetVersionDifference(v1, v2);

			result.ShouldBe(expected);
		}
	}
}
