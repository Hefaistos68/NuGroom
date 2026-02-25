using NuGroom;
using NuGroom.ADO;

using Shouldly;

namespace NuGroom.Tests
{
	[TestFixture]
	public class AzureDevOpsConfigTests
	{
		[Test]
		public void WhenDefaultsThenExcludeRepositoriesIsEmpty()
		{
			var config = new AzureDevOpsConfig
			{
				OrganizationUrl     = "https://dev.azure.com/testorg",
				PersonalAccessToken = "test-token"
			};

			config.ExcludeRepositories.ShouldNotBeNull();
			config.ExcludeRepositories.ShouldBeEmpty();
		}

		[Test]
		public void WhenExcludeRepositoriesSetThenStoresPatterns()
		{
			var config = new AzureDevOpsConfig
			{
				OrganizationUrl     = "https://dev.azure.com/testorg",
				PersonalAccessToken = "test-token",
				ExcludeRepositories = new List<string> { "Legacy-.*", "Test-.*" }
			};

			config.ExcludeRepositories.Count.ShouldBe(2);
		}

		[Test]
		public void WhenOrganizationUrlIsEmptyThenValidateThrowsArgumentException()
		{
			var config = new AzureDevOpsConfig
			{
				OrganizationUrl     = "",
				PersonalAccessToken = "test-token"
			};

			Should.Throw<ArgumentException>(() => config.Validate());
		}

		[Test]
		public void WhenTokenIsEmptyThenValidateThrowsArgumentException()
		{
			var config = new AzureDevOpsConfig
			{
				OrganizationUrl     = "https://dev.azure.com/testorg",
				PersonalAccessToken = ""
			};

			Should.Throw<ArgumentException>(() => config.Validate());
		}

		[Test]
		public void WhenOrganizationUrlIsInvalidThenValidateThrowsArgumentException()
		{
			var config = new AzureDevOpsConfig
			{
				OrganizationUrl     = "not-a-url",
				PersonalAccessToken = "test-token"
			};

			Should.Throw<ArgumentException>(() => config.Validate());
		}

		[Test]
		public void WhenMaxRepositoriesIsZeroThenValidateThrowsArgumentException()
		{
			var config = new AzureDevOpsConfig
			{
				OrganizationUrl     = "https://dev.azure.com/testorg",
				PersonalAccessToken = "test-token",
				MaxRepositories     = 0
			};

			Should.Throw<ArgumentException>(() => config.Validate());
		}

		[Test]
		public void WhenValidConfigThenValidateDoesNotThrow()
		{
			var config = new AzureDevOpsConfig
			{
				OrganizationUrl     = "https://dev.azure.com/testorg",
				PersonalAccessToken = "test-token"
			};

			Should.NotThrow(() => config.Validate());
		}
	}
}
