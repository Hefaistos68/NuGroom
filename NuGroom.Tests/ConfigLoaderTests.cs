using NuGroom.Configuration;

using Shouldly;

namespace NuGroom.Tests
{
	[TestFixture]
	public class ConfigLoaderTests
	{
		[Test]
		public void WhenPathIsNullThenThrowsArgumentException()
		{
			Should.Throw<ArgumentException>(() => ConfigLoader.Load(null!));
		}

		[Test]
		public void WhenPathIsEmptyThenThrowsArgumentException()
		{
			Should.Throw<ArgumentException>(() => ConfigLoader.Load(""));
		}

		[Test]
		public void WhenPathIsWhitespaceThenThrowsArgumentException()
		{
			Should.Throw<ArgumentException>(() => ConfigLoader.Load("   "));
		}

		[Test]
		public void WhenFileDoesNotExistThenThrowsFileNotFoundException()
		{
			Should.Throw<FileNotFoundException>(() => ConfigLoader.Load("nonexistent-file-12345.json"));
		}

		[Test]
		public void WhenValidJsonFileThenLoadsConfig()
		{
			var path = Path.Combine(Path.GetTempPath(), $"config-test-{Guid.NewGuid()}.json");

			try
			{
				File.WriteAllText(path, """
					{
					  "Organization": "https://dev.azure.com/testorg",
					  "Token": "test-token",
					  "MaxRepos": 50
					}
					""");

				var config = ConfigLoader.Load(path);

				config.Organization.ShouldBe("https://dev.azure.com/testorg");
				config.Token.ShouldBe("test-token");
				config.MaxRepos.ShouldBe(50);
			}
			finally
			{
				File.Delete(path);
			}
		}

		[Test]
		public void WhenJsonHasIgnoreRenovateThenLoadsValue()
		{
			var path = Path.Combine(Path.GetTempPath(), $"config-test-{Guid.NewGuid()}.json");

			try
			{
				File.WriteAllText(path, """
					{
					  "Organization": "https://dev.azure.com/testorg",
					  "Token": "test-token",
					  "IgnoreRenovate": true
					}
					""");

				var config = ConfigLoader.Load(path);

				config.IgnoreRenovate.ShouldBe(true);
			}
			finally
			{
				File.Delete(path);
			}
		}

		[Test]
		public void WhenJsonHasFeedsThenLoadsFeeds()
		{
			var path = Path.Combine(Path.GetTempPath(), $"config-test-{Guid.NewGuid()}.json");

			try
			{
				File.WriteAllText(path, """
					{
					  "Feeds": [
					    { "Name": "NuGet.org", "Url": "https://api.nuget.org/v3/index.json" }
					  ]
					}
					""");

				var config = ConfigLoader.Load(path);

				config.Feeds.ShouldNotBeNull();
				config.Feeds!.Count.ShouldBe(1);
				config.Feeds[0].Name.ShouldBe("NuGet.org");
			}
			finally
			{
				File.Delete(path);
			}
		}

		[Test]
		public void WhenJsonHasUpdateConfigThenLoadsUpdateSettings()
		{
			var path = Path.Combine(Path.GetTempPath(), $"config-test-{Guid.NewGuid()}.json");

			try
			{
				File.WriteAllText(path, """
					{
					  "Update": {
						"Scope": "Minor",
						"DryRun": false,
						"TargetBranchPattern": "main"
					  }
					}
					""");

				var config = ConfigLoader.Load(path);

				config.Update.ShouldNotBeNull();
				config.Update!.Scope.ShouldBe(UpdateScope.Minor);
				config.Update.DryRun.ShouldBeFalse();
				config.Update.TargetBranchPattern.ShouldBe("main");
			}
			finally
			{
				File.Delete(path);
			}
		}

		[Test]
		public void WhenJsonHasPinnedPackagesThenLoadsPinnedPackages()
		{
			var path = Path.Combine(Path.GetTempPath(), $"config-test-{Guid.NewGuid()}.json");

			try
			{
				File.WriteAllText(path, """
					{
					  "Update": {
						"Scope": "Minor",
						"PinnedPackages": [
						  { "PackageName": "EPPlus", "Version": "7.3.1", "Reason": "license" },
						  { "PackageName": "Serilog", "Version": null, "Reason": "migration" }
						]
					  }
					}
					""");

				var config = ConfigLoader.Load(path);

				config.Update.ShouldNotBeNull();
				config.Update!.PinnedPackages.ShouldNotBeNull();
				config.Update.PinnedPackages!.Count.ShouldBe(2);
				config.Update.PinnedPackages[0].PackageName.ShouldBe("EPPlus");
				config.Update.PinnedPackages[0].Version.ShouldBe("7.3.1");
				config.Update.PinnedPackages[0].Reason.ShouldBe("license");
				config.Update.PinnedPackages[1].PackageName.ShouldBe("Serilog");
				config.Update.PinnedPackages[1].Version.ShouldBeNull();
			}
			finally
			{
				File.Delete(path);
			}
		}

		[Test]
		public void WhenJsonHasEmptyObjectThenReturnsDefaultConfig()
		{
			var path = Path.Combine(Path.GetTempPath(), $"config-test-{Guid.NewGuid()}.json");

			try
			{
				File.WriteAllText(path, "{}");

				var config = ConfigLoader.Load(path);

				config.ShouldNotBeNull();
				config.Organization.ShouldBeNull();
				config.Token.ShouldBeNull();
			}
			finally
			{
				File.Delete(path);
			}
		}

		[Test]
		public void WhenJsonHasExcludeRepositoriesThenLoadsPatterns()
		{
			var path = Path.Combine(Path.GetTempPath(), $"config-test-{Guid.NewGuid()}.json");

			try
			{
				File.WriteAllText(path, """
					{
					  "ExcludeRepositories": ["Legacy-.*", "Archive\\..*"]
					}
					""");

				var config = ConfigLoader.Load(path);

				config.ExcludeRepositories.ShouldNotBeNull();
				config.ExcludeRepositories!.Count.ShouldBe(2);
				config.ExcludeRepositories[0].ShouldBe("Legacy-.*");
			}
			finally
			{
				File.Delete(path);
			}
		}

		[Test]
		public void WhenTokenUsesEnvColonSyntaxThenResolvesFromEnvironment()
		{
			var varName = $"NUGROOM_TEST_TOKEN_{Guid.NewGuid():N}";
			Environment.SetEnvironmentVariable(varName, "resolved-pat-value");

			try
			{
				var path = Path.Combine(Path.GetTempPath(), $"config-test-{Guid.NewGuid()}.json");
				File.WriteAllText(path, $$"""
					{
					  "Token": "$env:{{varName}}"
					}
					""");

				var config = ConfigLoader.Load(path);

				config.Token.ShouldBe("resolved-pat-value");
				File.Delete(path);
			}
			finally
			{
				Environment.SetEnvironmentVariable(varName, null);
			}
		}

		[Test]
		public void WhenTokenUsesBraceSyntaxThenResolvesFromEnvironment()
		{
			var varName = $"NUGROOM_TEST_TOKEN_{Guid.NewGuid():N}";
			Environment.SetEnvironmentVariable(varName, "brace-pat-value");

			try
			{
				var path = Path.Combine(Path.GetTempPath(), $"config-test-{Guid.NewGuid()}.json");
				File.WriteAllText(path, $$"""
					{
					  "Token": "${{{varName}}}"
					}
					""");

				var config = ConfigLoader.Load(path);

				config.Token.ShouldBe("brace-pat-value");
				File.Delete(path);
			}
			finally
			{
				Environment.SetEnvironmentVariable(varName, null);
			}
		}

		[Test]
		public void WhenEnvVarIsNotSetThenKeepsOriginalPlaceholder()
		{
			var varName = $"NUGROOM_MISSING_{Guid.NewGuid():N}";
			var placeholder = $"$env:{varName}";
			var path = Path.Combine(Path.GetTempPath(), $"config-test-{Guid.NewGuid()}.json");
			File.WriteAllText(path, $$"""
				{
				  "Token": "{{placeholder}}"
				}
				""");

			var config = ConfigLoader.Load(path);

			config.Token.ShouldBe(placeholder);
			File.Delete(path);
		}

		[Test]
		public void WhenTokenIsPlainStringThenRemainsUnchanged()
		{
			var path = Path.Combine(Path.GetTempPath(), $"config-test-{Guid.NewGuid()}.json");
			File.WriteAllText(path, """
				{
				  "Token": "plain-token-value"
				}
				""");

			var config = ConfigLoader.Load(path);

			config.Token.ShouldBe("plain-token-value");
			File.Delete(path);
		}

		[Test]
		public void WhenFeedAuthPatUsesEnvVarThenResolvesFromEnvironment()
		{
			var varName = $"NUGROOM_TEST_PAT_{Guid.NewGuid():N}";
			Environment.SetEnvironmentVariable(varName, "feed-pat-resolved");

			try
			{
				var path = Path.Combine(Path.GetTempPath(), $"config-test-{Guid.NewGuid()}.json");
				File.WriteAllText(path, $$"""
					{
					  "FeedAuth": [
						{ "FeedName": "MyFeed", "Username": null, "Pat": "$env:{{varName}}" }
					  ]
					}
					""");

				var config = ConfigLoader.Load(path);

				config.FeedAuth.ShouldNotBeNull();
				config.FeedAuth![0].Pat.ShouldBe("feed-pat-resolved");
				File.Delete(path);
			}
			finally
			{
				Environment.SetEnvironmentVariable(varName, null);
			}
		}
	}
}
