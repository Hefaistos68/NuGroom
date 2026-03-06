using Moq;

using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

using NuGroom.Nuget;

using NUnit.Framework;

using Shouldly;

namespace NuGroom.Tests.Nuget
{
	[TestFixture]
	public class DetectVulnerabilitiesTests
	{
		[Test]
		public void WhenNoVulnerabilitiesThenReturnsEmptyList()
		{
			var metadata = new Mock<IPackageSearchMetadata>();
			metadata.Setup(m => m.Vulnerabilities)
				.Returns((IEnumerable<PackageVulnerabilityMetadata>?)null);

			var result = NuGetPackageResolver.DetectVulnerabilities(metadata.Object);

			result.ShouldBeEmpty();
		}

		[Test]
		public void WhenEmptyVulnerabilitiesThenReturnsEmptyList()
		{
			var metadata = new Mock<IPackageSearchMetadata>();
			metadata.Setup(m => m.Vulnerabilities)
				.Returns(Enumerable.Empty<PackageVulnerabilityMetadata>());

			var result = NuGetPackageResolver.DetectVulnerabilities(metadata.Object);

			result.ShouldBeEmpty();
		}

		[Test]
		public void WhenCriticalVulnerabilityThenReturnsCriticalSeverity()
		{
			var advisoryUrl = new Uri("https://github.com/advisories/GHSA-1234");
			var metadata = CreateMetadataWithVulnerability(advisoryUrl, severity: 3);

			var result = NuGetPackageResolver.DetectVulnerabilities(metadata);

			result.Count.ShouldBe(1);
			result[0].ShouldContain("Critical");
			result[0].ShouldContain("https://github.com/advisories/GHSA-1234");
		}

		[Test]
		public void WhenHighVulnerabilityThenReturnsHighSeverity()
		{
			var metadata = CreateMetadataWithVulnerability(
				new Uri("https://github.com/advisories/GHSA-5678"), severity: 2);

			var result = NuGetPackageResolver.DetectVulnerabilities(metadata);

			result.Count.ShouldBe(1);
			result[0].ShouldContain("High");
		}

		[Test]
		public void WhenModerateVulnerabilityThenReturnsModerateSeverity()
		{
			var metadata = CreateMetadataWithVulnerability(
				new Uri("https://example.com/advisory"), severity: 1);

			var result = NuGetPackageResolver.DetectVulnerabilities(metadata);

			result.Count.ShouldBe(1);
			result[0].ShouldContain("Moderate");
		}

		[Test]
		public void WhenLowVulnerabilityThenReturnsLowSeverity()
		{
			var metadata = CreateMetadataWithVulnerability(
				new Uri("https://example.com/advisory"), severity: 0);

			var result = NuGetPackageResolver.DetectVulnerabilities(metadata);

			result.Count.ShouldBe(1);
			result[0].ShouldContain("Low");
		}

		[Test]
		public void WhenMultipleVulnerabilitiesThenReturnsAll()
		{
			var vulns = new List<PackageVulnerabilityMetadata>
			{
				new(new Uri("https://example.com/1"), 3),
				new(new Uri("https://example.com/2"), 1)
			};
			var metadata = new Mock<IPackageSearchMetadata>();
			metadata.Setup(m => m.Vulnerabilities).Returns(vulns);

			var result = NuGetPackageResolver.DetectVulnerabilities(metadata.Object);

			result.Count.ShouldBe(2);
			result[0].ShouldContain("Critical");
			result[1].ShouldContain("Moderate");
		}

		[TestCase(4)]
		[TestCase(-1)]
		[TestCase(99)]
		public void WhenUnknownSeverityThenReturnsUnknownLabel(int severity)
		{
			var metadata = CreateMetadataWithVulnerability(
				new Uri("https://example.com/advisory"), severity);

			var result = NuGetPackageResolver.DetectVulnerabilities(metadata);

			result.Count.ShouldBe(1);
			result[0].ShouldContain($"Unknown ({severity})");
		}

		/// <summary>
		/// Creates a mock <see cref="IPackageSearchMetadata"/> with a single vulnerability entry.
		/// </summary>
		private static IPackageSearchMetadata CreateMetadataWithVulnerability(Uri advisoryUrl, int severity)
		{
			var vuln = new PackageVulnerabilityMetadata(advisoryUrl, severity);
			var metadata = new Mock<IPackageSearchMetadata>();
			metadata.Setup(m => m.Vulnerabilities).Returns([vuln]);

			return metadata.Object;
		}
	}
}
