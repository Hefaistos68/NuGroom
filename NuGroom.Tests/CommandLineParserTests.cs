using System.Text.Json;

using NuGroom.Configuration;
using NuGroom.Nuget;

using Shouldly;

namespace NuGroom.Tests
{
	[TestFixture]
	public class CommandLineParserTests
	{
		private static readonly string[] MinimalValidArgs =
			["--organization", "https://dev.azure.com/org", "--token", "my-pat"];

		// ── Help & empty args ──────────────────────────────────────────

		[Test]
		public void WhenNoArgsThenReturnsEmptyResult()
		{
			var result = CommandLineParser.Parse([]);

			result.Config.ShouldBeNull();
		}

		[Test]
		public void WhenHelpFlagThenReturnsEmptyResult()
		{
			var result = CommandLineParser.Parse(["--help"]);

			result.Config.ShouldBeNull();
		}

		[Test]
		public void WhenShortHelpFlagThenReturnsEmptyResult()
		{
			var result = CommandLineParser.Parse(["-h"]);

			result.Config.ShouldBeNull();
		}

		// ── Required parameter validation ──────────────────────────────

		[Test]
		public void WhenOrganizationMissingThenConfigIsNull()
		{
			var result = CommandLineParser.Parse(["--token", "my-pat"]);

			result.Config.ShouldBeNull();
		}

		[Test]
		public void WhenTokenMissingThenConfigIsNull()
		{
			var result = CommandLineParser.Parse(["--organization", "https://dev.azure.com/org"]);

			result.Config.ShouldBeNull();
		}

		// ── Minimal valid args ─────────────────────────────────────────

		[Test]
		public void WhenMinimalValidArgsThenConfigIsPopulated()
		{
			var result = CommandLineParser.Parse(MinimalValidArgs);

			result.Config.ShouldNotBeNull();
			result.Config.OrganizationUrl.ShouldBe("https://dev.azure.com/org");
			result.Config.PersonalAccessToken.ShouldBe("my-pat");
		}

		[Test]
		public void WhenMinimalValidArgsThenDefaultsAreApplied()
		{
			var result = CommandLineParser.Parse(MinimalValidArgs);

			result.ResolveNuGet.ShouldBeTrue();
			result.ShowDetailedInfo.ShouldBeFalse();
			result.IgnoreRenovate.ShouldBeFalse();
			result.Feeds.ShouldBeEmpty();
			result.ExportPackagesPath.ShouldBeNull();
			result.FeedAuth.ShouldBeEmpty();
			result.VersionWarningConfig.ShouldBeNull();
			result.UpdateConfig.ShouldBeNull();
			result.SyncConfigs.ShouldBeEmpty();
		}

		// ── Short-form aliases ─────────────────────────────────────────

		[Test]
		public void WhenUsingShortFormArgsThenParsesCorrectly()
		{
			var result = CommandLineParser.Parse(
				["-o", "https://dev.azure.com/org", "-t", "pat", "-p", "MyProject"]);

			result.Config.ShouldNotBeNull();
			result.Config.OrganizationUrl.ShouldBe("https://dev.azure.com/org");
			result.Config.PersonalAccessToken.ShouldBe("pat");
			result.Config.ProjectName.ShouldBe("MyProject");
		}

		// ── Project ────────────────────────────────────────────────────

		[Test]
		public void WhenProjectSpecifiedThenConfigContainsProject()
		{
			var args = MinimalValidArgs.Concat(["--project", "MyProject"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.Config.ShouldNotBeNull();
			result.Config.ProjectName.ShouldBe("MyProject");
		}

		// ── Max repos ──────────────────────────────────────────────────

		[Test]
		public void WhenMaxReposSpecifiedThenConfigUsesValue()
		{
			var args = MinimalValidArgs.Concat(["--max-repos", "50"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.Config.ShouldNotBeNull();
			result.Config.MaxRepositories.ShouldBe(50);
		}

		[Test]
		public void WhenMaxReposShortFormThenConfigUsesValue()
		{
			var args = MinimalValidArgs.Concat(["-m", "25"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.Config.ShouldNotBeNull();
			result.Config.MaxRepositories.ShouldBe(25);
		}

		[Test]
		public void WhenMaxReposInvalidThenDefaultIsUsed()
		{
			var args = MinimalValidArgs.Concat(["--max-repos", "notanumber"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.Config.ShouldNotBeNull();
			result.Config.MaxRepositories.ShouldBe(100);
		}

		// ── Boolean flags ──────────────────────────────────────────────

		[Test]
		public void WhenIncludeArchivedThenConfigReflectsIt()
		{
			var args = MinimalValidArgs.Concat(["--include-archived"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.Config.ShouldNotBeNull();
			result.Config.IncludeArchivedRepositories.ShouldBeTrue();
		}

		[Test]
		public void WhenExcludeArchivedThenConfigReflectsIt()
		{
			var args = MinimalValidArgs.Concat(["--exclude-archived"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.Config.ShouldNotBeNull();
			result.Config.IncludeArchivedRepositories.ShouldBeFalse();
		}

		[Test]
		public void WhenSkipNuGetThenResolveNuGetIsFalse()
		{
			var args = MinimalValidArgs.Concat(["--skip-nuget"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.ResolveNuGet.ShouldBeFalse();
		}

		[Test]
		public void WhenResolveNuGetThenResolveNuGetIsTrue()
		{
			var args = MinimalValidArgs.Concat(["--resolve-nuget"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.ResolveNuGet.ShouldBeTrue();
		}

		[Test]
		public void WhenDetailedFlagThenShowDetailedInfoIsTrue()
		{
			var args = MinimalValidArgs.Concat(["--detailed"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.ShowDetailedInfo.ShouldBeTrue();
		}

		[Test]
		public void WhenShortDetailedFlagThenShowDetailedInfoIsTrue()
		{
			var args = MinimalValidArgs.Concat(["-d"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.ShowDetailedInfo.ShouldBeTrue();
		}

		[Test]
		public void WhenNoDetailedFlagThenShowDetailedInfoIsFalse()
		{
			var args = MinimalValidArgs.Concat(["--no-detailed"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.ShowDetailedInfo.ShouldBeFalse();
		}

		[Test]
		public void WhenIgnoreRenovateFlagThenIgnoreRenovateIsTrue()
		{
			var args = MinimalValidArgs.Concat(["--ignore-renovate"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.IgnoreRenovate.ShouldBeTrue();
		}

		[Test]
		public void WhenDebugFlagThenDebugLoggingIsEnabled()
		{
			var previous = Logger.EnableDebugLogging;

			try
			{
				Logger.EnableDebugLogging = false;
				var args = MinimalValidArgs.Concat(["--debug"]).ToArray();

				CommandLineParser.Parse(args);

				Logger.EnableDebugLogging.ShouldBeTrue();
			}
			finally
			{
				Logger.EnableDebugLogging = previous;
			}
		}

		// ── Exclusion arguments ────────────────────────────────────────

		[Test]
		public void WhenExcludePrefixThenExclusionListContainsPrefix()
		{
			var args = MinimalValidArgs.Concat(["--exclude-prefix", "MyCompany."]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.ExclusionList.ExcludedPrefixes.ShouldContain("MyCompany.");
		}

		[Test]
		public void WhenExcludePackageThenExclusionListContainsPackage()
		{
			var args = MinimalValidArgs.Concat(["--exclude-package", "SomePackage"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.ExclusionList.ExcludedPackages.ShouldContain("SomePackage");
		}

		[Test]
		public void WhenExcludePatternThenExclusionListContainsPattern()
		{
			var args = MinimalValidArgs.Concat(["--exclude-pattern", "^Test\\..*"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.ExclusionList.ExcludedPatterns.ShouldContain("^Test\\..*");
		}

		[Test]
		public void WhenExcludeProjectThenConfigContainsPattern()
		{
			var args = MinimalValidArgs.Concat(["--exclude-project", ".*\\.Tests\\.csproj"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.Config.ShouldNotBeNull();
			result.Config.ExcludeProjectPatterns.ShouldContain(".*\\.Tests\\.csproj");
		}

		[Test]
		public void WhenExcludeRepoThenConfigContainsPattern()
		{
			var args = MinimalValidArgs.Concat(["--exclude-repo", "Legacy-.*"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.Config.ShouldNotBeNull();
			result.Config.ExcludeRepositories.ShouldContain("Legacy-.*");
		}

		[Test]
		public void WhenIncludeRepoThenConfigContainsPattern()
		{
			var args = MinimalValidArgs.Concat(["--include-repo", "Active-.*"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.Config.ShouldNotBeNull();
			result.Config.IncludeRepositories.ShouldContain("Active-.*");
		}

		[Test]
		public void WhenNoDefaultExclusionsThenExclusionListIsEmpty()
		{
			var args = MinimalValidArgs.Concat(["--no-default-exclusions"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.ExclusionList.ExcludedPrefixes.ShouldBeEmpty();
		}

		[Test]
		public void WhenCaseSensitiveThenExclusionListIsCaseSensitive()
		{
			var args = MinimalValidArgs.Concat(["--case-sensitive"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.ExclusionList.CaseSensitive.ShouldBeTrue();
		}

		[Test]
		public void WhenCaseSensitiveProjectThenConfigReflectsIt()
		{
			var args = MinimalValidArgs.Concat(["--case-sensitive-project"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.Config.ShouldNotBeNull();
			result.Config.CaseSensitiveProjectFilters.ShouldBeTrue();
		}

		// ── Feed arguments ─────────────────────────────────────────────

		[Test]
		public void WhenFeedSpecifiedThenFeedsListContainsFeed()
		{
			var args = MinimalValidArgs.Concat(["--feed", "https://api.nuget.org/v3/index.json"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.Feeds.Count.ShouldBe(1);
			result.Feeds[0].Url.ShouldBe("https://api.nuget.org/v3/index.json");
			result.Feeds[0].Name.ShouldBe("CLIFeed1");
		}

		[Test]
		public void WhenMultipleFeedsThenFeedsListContainsAll()
		{
			var args = MinimalValidArgs.Concat(
				["--feed", "https://feed1/index.json", "--nuget-feed", "https://feed2/index.json"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.Feeds.Count.ShouldBe(2);
			result.Feeds[0].Name.ShouldBe("CLIFeed1");
			result.Feeds[1].Name.ShouldBe("CLIFeed2");
		}

		// ── Export arguments ───────────────────────────────────────────

		[Test]
		public void WhenExportPackagesCliSpecifiedThenPathIsSet()
		{
			var args = MinimalValidArgs.Concat(["--export-packages", "output.json"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.ExportPackagesPath.ShouldBe("output.json");
		}

		// ── Feed auth arguments ────────────────────────────────────────

		[Test]
		public void WhenFeedAuthValidFormatThenAuthIsAdded()
		{
			var args = MinimalValidArgs.Concat(["--feed-auth", "MyFeed|user|pat123"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.FeedAuth.Count.ShouldBe(1);
			result.FeedAuth[0].FeedName.ShouldBe("MyFeed");
			result.FeedAuth[0].Username.ShouldBe("user");
			result.FeedAuth[0].Pat.ShouldBe("pat123");
		}

		[Test]
		public void WhenFeedAuthEmptyUsernameThenUsernameIsNull()
		{
			var args = MinimalValidArgs.Concat(["--feed-auth", "MyFeed||pat123"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.FeedAuth.Count.ShouldBe(1);
			result.FeedAuth[0].Username.ShouldBeNull();
		}

		[Test]
		public void WhenFeedAuthInvalidFormatThenAuthIsNotAdded()
		{
			var args = MinimalValidArgs.Concat(["--feed-auth", "invalidformat"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.FeedAuth.ShouldBeEmpty();
		}

		// ── Update arguments ───────────────────────────────────────────

		[Test]
		public void WhenUpdateReferencesSpecifiedThenUpdateConfigExists()
		{
			var args = MinimalValidArgs.Concat(["--update-references"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.DryRun.ShouldBeFalse();
		}

		[Test]
		public void WhenDryRunSpecifiedThenDryRunIsTrue()
		{
			var args = MinimalValidArgs.Concat(["--dry-run"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.DryRun.ShouldBeTrue();
		}

		[Test]
		public void WhenDryRunAndUpdateReferencesThenDryRunWins()
		{
			var args = MinimalValidArgs.Concat(["--update-references", "--dry-run"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.DryRun.ShouldBeTrue();
		}

		[Test]
		public void WhenDryRunBeforeUpdateReferencesThenDryRunStillWins()
		{
			var args = MinimalValidArgs.Concat(["--dry-run", "--update-references"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.DryRun.ShouldBeTrue();
		}

		[TestCase("Patch", UpdateScope.Patch)]
		[TestCase("Minor", UpdateScope.Minor)]
		[TestCase("Major", UpdateScope.Major)]
		[TestCase("patch", UpdateScope.Patch)]
		public void WhenUpdateScopeSpecifiedThenScopeIsSet(string scopeArg, UpdateScope expected)
		{
			var args = MinimalValidArgs.Concat(["--dry-run", "--update-scope", scopeArg]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.Scope.ShouldBe(expected);
		}

		[Test]
		public void WhenUpdateScopeInvalidThenDefaultScopeIsUsed()
		{
			var args = MinimalValidArgs.Concat(["--dry-run", "--update-scope", "Invalid"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.Scope.ShouldBe(UpdateScope.Patch);
		}

		[Test]
		public void WhenSourceBranchSpecifiedThenUpdateConfigContainsIt()
		{
			var args = MinimalValidArgs.Concat(["--dry-run", "--source-branch", "main"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.SourceBranchPattern.ShouldBe("main");
		}

		[Test]
		public void WhenTargetBranchSpecifiedThenUpdateConfigContainsIt()
		{
			var args = MinimalValidArgs.Concat(["--dry-run", "--target-branch", "release/*"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.TargetBranchPattern.ShouldBe("release/*");
		}

		[Test]
		public void WhenFeatureBranchSpecifiedThenUpdateConfigContainsIt()
		{
			var args = MinimalValidArgs.Concat(["--dry-run", "--feature-branch", "feature/my-update"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.FeatureBranchName.ShouldBe("feature/my-update");
		}

		[Test]
		public void WhenSourcePackagesOnlyThenFlagIsSet()
		{
			var args = MinimalValidArgs.Concat(["--dry-run", "--source-packages-only"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.SourcePackagesOnly.ShouldBeTrue();
		}

		[Test]
		public void WhenRequiredReviewerSpecifiedThenListContainsReviewer()
		{
			var args = MinimalValidArgs.Concat(["--dry-run", "--required-reviewer", "user@example.com"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.RequiredReviewers.ShouldNotBeNull();
			result.UpdateConfig.RequiredReviewers.ShouldContain("user@example.com");
		}

		[Test]
		public void WhenOptionalReviewerSpecifiedThenListContainsReviewer()
		{
			var args = MinimalValidArgs.Concat(["--dry-run", "--optional-reviewer", "reviewer@example.com"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.OptionalReviewers.ShouldNotBeNull();
			result.UpdateConfig.OptionalReviewers.ShouldContain("reviewer@example.com");
		}

		// ── Sync arguments ─────────────────────────────────────────────

		[Test]
		public void WhenSyncWithVersionThenSyncConfigIsPopulated()
		{
			var args = MinimalValidArgs.Concat(["--sync", "Newtonsoft.Json", "13.0.3"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.SyncConfigs.Count.ShouldBe(1);
			result.SyncConfigs[0].PackageName.ShouldBe("Newtonsoft.Json");
			result.SyncConfigs[0].TargetVersion.ShouldBe("13.0.3");
		}

		[Test]
		public void WhenSyncWithoutVersionThenVersionIsNull()
		{
			var args = MinimalValidArgs.Concat(["--sync", "Newtonsoft.Json"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.SyncConfigs.Count.ShouldBe(1);
			result.SyncConfigs[0].PackageName.ShouldBe("Newtonsoft.Json");
			result.SyncConfigs[0].TargetVersion.ShouldBeNull();
		}

		[Test]
		public void WhenMultipleSyncsThenAllAreAdded()
		{
			var args = MinimalValidArgs.Concat(
				["--sync", "PackageA", "1.0.0", "--sync", "PackageB"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.SyncConfigs.Count.ShouldBe(2);
		}

		// ── Config file loading ────────────────────────────────────────

		[Test]
		public void WhenConfigFileSpecifiedThenFileDefaultsAreApplied()
		{
			var configPath = Path.Combine(Path.GetTempPath(), $"nugroom-test-{Guid.NewGuid()}.json");
			var config = new ToolConfig
			{
				Organization = "https://dev.azure.com/fileorg",
				Token        = "file-token",
				Project      = "FileProject",
				MaxRepos     = 42,
				Detailed     = true
			};
			File.WriteAllText(configPath, JsonSerializer.Serialize(config));

			var result = CommandLineParser.Parse(["--config", configPath]);

			result.Config.ShouldNotBeNull();
			result.Config.OrganizationUrl.ShouldBe("https://dev.azure.com/fileorg");
			result.Config.PersonalAccessToken.ShouldBe("file-token");
			result.Config.ProjectName.ShouldBe("FileProject");
			result.Config.MaxRepositories.ShouldBe(42);
			result.ShowDetailedInfo.ShouldBeTrue();
		}

		[Test]
		public void WhenConfigFileAndCliArgsThenCliTakesPriority()
		{
			var configPath = Path.Combine(Path.GetTempPath(), $"nugroom-test-{Guid.NewGuid()}.json");
			var config = new ToolConfig
			{
				Organization = "https://dev.azure.com/fileorg",
				Token        = "file-token",
				Project      = "FileProject"
			};
			File.WriteAllText(configPath, JsonSerializer.Serialize(config));

			var result = CommandLineParser.Parse(
				["--config", configPath, "--organization", "https://dev.azure.com/cliorg", "--token", "cli-token"]);

			result.Config.ShouldNotBeNull();
			result.Config.OrganizationUrl.ShouldBe("https://dev.azure.com/cliorg");
			result.Config.PersonalAccessToken.ShouldBe("cli-token");
			result.Config.ProjectName.ShouldBe("FileProject");
		}

		[Test]
		public void WhenConfigFileMissingThenParsingContinuesWithoutError()
		{
			var result = CommandLineParser.Parse(
				["--config", "nonexistent-file.json", "--organization", "https://dev.azure.com/org", "--token", "pat"]);

			result.Config.ShouldNotBeNull();
		}

		[Test]
		public void WhenConfigFileHasFeedsThenFeedsAreMerged()
		{
			var configPath = Path.Combine(Path.GetTempPath(), $"nugroom-test-{Guid.NewGuid()}.json");
			var config = new ToolConfig
			{
				Organization = "https://dev.azure.com/org",
				Token        = "token",
				Feeds        = [new Feed("ConfigFeed", "https://configfeed/index.json")]
			};
			File.WriteAllText(configPath, JsonSerializer.Serialize(config));

			var result = CommandLineParser.Parse(
				["--config", configPath, "--feed", "https://clifeed/index.json"]);

			result.Feeds.Count.ShouldBe(2);
			result.Feeds.ShouldContain(f => f.Url == "https://clifeed/index.json");
			result.Feeds.ShouldContain(f => f.Name == "ConfigFeed");
		}

		[Test]
		public void WhenConfigFileHasExclusionsThenExclusionsAreMerged()
		{
			var configPath = Path.Combine(Path.GetTempPath(), $"nugroom-test-{Guid.NewGuid()}.json");
			var config = new ToolConfig
			{
				Organization    = "https://dev.azure.com/org",
				Token           = "token",
				ExcludePrefixes = ["FilePrefix."],
				ExcludePackages = ["FilePackage"]
			};
			File.WriteAllText(configPath, JsonSerializer.Serialize(config));

			var result = CommandLineParser.Parse(
				["--config", configPath, "--exclude-prefix", "CliPrefix."]);

			result.ExclusionList.ExcludedPrefixes.ShouldContain("FilePrefix.");
			result.ExclusionList.ExcludedPrefixes.ShouldContain("CliPrefix.");
			result.ExclusionList.ExcludedPackages.ShouldContain("FilePackage");
		}

		[Test]
		public void WhenConfigFileHasUpdateConfigThenItIsApplied()
		{
			var configPath = Path.Combine(Path.GetTempPath(), $"nugroom-test-{Guid.NewGuid()}.json");
			var config = new ToolConfig
			{
				Organization = "https://dev.azure.com/org",
				Token        = "token",
				Update       = new UpdateConfig
				{
					Scope              = UpdateScope.Minor,
					DryRun             = false,
					SourcePackagesOnly = true
				}
			};
			File.WriteAllText(configPath, JsonSerializer.Serialize(config));

			var result = CommandLineParser.Parse(["--config", configPath]);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.Scope.ShouldBe(UpdateScope.Minor);
			result.UpdateConfig.SourcePackagesOnly.ShouldBeTrue();
		}

		[Test]
		public void WhenConfigFileHasFeedAuthThenAuthIsMerged()
		{
			var configPath = Path.Combine(Path.GetTempPath(), $"nugroom-test-{Guid.NewGuid()}.json");
			var config = new ToolConfig
			{
				Organization = "https://dev.azure.com/org",
				Token        = "token",
				FeedAuth     = [new FeedAuth("FileFeed", "user", "pat")]
			};
			File.WriteAllText(configPath, JsonSerializer.Serialize(config));

			var result = CommandLineParser.Parse(["--config", configPath]);

			result.FeedAuth.Count.ShouldBe(1);
			result.FeedAuth[0].FeedName.ShouldBe("FileFeed");
		}

		[Test]
		public void WhenConfigFileHasRepoFiltersThenTheyAreApplied()
		{
			var configPath = Path.Combine(Path.GetTempPath(), $"nugroom-test-{Guid.NewGuid()}.json");
			var config = new ToolConfig
			{
				Organization        = "https://dev.azure.com/org",
				Token               = "token",
				ExcludeRepositories = ["Legacy-.*"],
				IncludeRepositories = ["Active-.*"]
			};
			File.WriteAllText(configPath, JsonSerializer.Serialize(config));

			var result = CommandLineParser.Parse(["--config", configPath]);

			result.Config.ShouldNotBeNull();
			result.Config.ExcludeRepositories.ShouldContain("Legacy-.*");
			result.Config.IncludeRepositories.ShouldContain("Active-.*");
		}

		[Test]
		public void WhenConfigFileHasBooleanSettingsThenTheyAreApplied()
		{
			var configPath = Path.Combine(Path.GetTempPath(), $"nugroom-test-{Guid.NewGuid()}.json");
			var config = new ToolConfig
			{
				Organization           = "https://dev.azure.com/org",
				Token                  = "token",
				IncludeArchived        = true,
				ResolveNuGet           = false,
				NoDefaultExclusions    = true,
				CaseSensitive          = true,
				CaseSensitiveProjectFilters = true,
				IgnoreRenovate         = true
			};
			File.WriteAllText(configPath, JsonSerializer.Serialize(config));

			var result = CommandLineParser.Parse(["--config", configPath]);

			result.Config.ShouldNotBeNull();
			result.Config.IncludeArchivedRepositories.ShouldBeTrue();
			result.ResolveNuGet.ShouldBeFalse();
			result.IgnoreRenovate.ShouldBeTrue();
			result.Config.CaseSensitiveProjectFilters.ShouldBeTrue();
			result.ExclusionList.CaseSensitive.ShouldBeTrue();
			result.ExclusionList.ExcludedPrefixes.ShouldBeEmpty();
		}

		[Test]
		public void WhenConfigFileCliDryRunOverridesFileUpdateConfig()
		{
			var configPath = Path.Combine(Path.GetTempPath(), $"nugroom-test-{Guid.NewGuid()}.json");
			var config = new ToolConfig
			{
				Organization = "https://dev.azure.com/org",
				Token        = "token",
				Update       = new UpdateConfig { DryRun = false, Scope = UpdateScope.Major }
			};
			File.WriteAllText(configPath, JsonSerializer.Serialize(config));

			var result = CommandLineParser.Parse(
				["--config", configPath, "--dry-run"]);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.DryRun.ShouldBeTrue();
		}

		[Test]
		public void WhenConfigFileCliUpdateScopeOverridesFileScope()
		{
			var configPath = Path.Combine(Path.GetTempPath(), $"nugroom-test-{Guid.NewGuid()}.json");
			var config = new ToolConfig
			{
				Organization = "https://dev.azure.com/org",
				Token        = "token",
				Update       = new UpdateConfig { Scope = UpdateScope.Major }
			};
			File.WriteAllText(configPath, JsonSerializer.Serialize(config));

			var result = CommandLineParser.Parse(
				["--config", configPath, "--dry-run", "--update-scope", "Patch"]);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.Scope.ShouldBe(UpdateScope.Patch);
		}

		[Test]
		public void WhenConfigFileHasBranchNamesAndCliCreatesDryRunThenConfigBranchNamesAreMerged()
		{
			var configPath = Path.Combine(Path.GetTempPath(), $"nugroom-test-{Guid.NewGuid()}.json");
			var config = new ToolConfig
			{
				Organization = "https://dev.azure.com/org",
				Token        = "token",
				Update       = new UpdateConfig
				{
					FeatureBranchName  = "nugroom/custom-branch",
					SourceBranchPattern = "develop/*",
					TargetBranchPattern = "release/*",
					SourcePackagesOnly  = true
				}
			};
			File.WriteAllText(configPath, JsonSerializer.Serialize(config));

			var result = CommandLineParser.Parse(
				["--config", configPath, "--dry-run"]);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.FeatureBranchName.ShouldBe("nugroom/custom-branch");
			result.UpdateConfig.SourceBranchPattern.ShouldBe("develop/*");
			result.UpdateConfig.TargetBranchPattern.ShouldBe("release/*");
			result.UpdateConfig.SourcePackagesOnly.ShouldBeTrue();
		}

		[Test]
		public void WhenCliBranchArgsOverrideConfigFileBranchNames()
		{
			var configPath = Path.Combine(Path.GetTempPath(), $"nugroom-test-{Guid.NewGuid()}.json");
			var config = new ToolConfig
			{
				Organization = "https://dev.azure.com/org",
				Token        = "token",
				Update       = new UpdateConfig
				{
					FeatureBranchName   = "nugroom/config-branch",
					SourceBranchPattern = "develop/*",
					TargetBranchPattern = "release/*"
				}
			};
			File.WriteAllText(configPath, JsonSerializer.Serialize(config));

			var result = CommandLineParser.Parse(
				["--config", configPath, "--dry-run",
				 "--feature-branch", "custom/cli-branch",
				 "--source-branch", "main",
				 "--target-branch", "master"]);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.FeatureBranchName.ShouldBe("custom/cli-branch");
			result.UpdateConfig.SourceBranchPattern.ShouldBe("main");
			result.UpdateConfig.TargetBranchPattern.ShouldBe("master");
		}

		[Test]
		public void WhenConfigFileHasExportPackagesPathThenItIsApplied()
		{
			var configPath = Path.Combine(Path.GetTempPath(), $"nugroom-test-{Guid.NewGuid()}.json");
			var config = new ToolConfig
			{
				Organization    = "https://dev.azure.com/org",
				Token           = "token",
				ExportPackages  = "file-output.json"
			};
			File.WriteAllText(configPath, JsonSerializer.Serialize(config));

			var result = CommandLineParser.Parse(["--config", configPath]);

			result.ExportPackagesPath.ShouldBe("file-output.json");
		}

		// ── Exclusion list building ────────────────────────────────────

		[Test]
		public void WhenDefaultExclusionsThenExclusionListUsesDefaults()
		{
			var result = CommandLineParser.Parse(MinimalValidArgs);

			var expected = PackageReferenceExtractor.ExclusionList.CreateDefault();
			result.ExclusionList.ExcludedPrefixes.Count.ShouldBe(expected.ExcludedPrefixes.Count);
		}

		[Test]
		public void WhenMultipleExclusionTypesThenAllAreApplied()
		{
			var args = MinimalValidArgs.Concat(
			[
				"--no-default-exclusions",
				"--exclude-prefix", "Custom.",
				"--exclude-package", "ExactPkg",
				"--exclude-pattern", "^Test\\..*"
			]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.ExclusionList.ShouldExclude("Custom.Something").ShouldBeTrue();
			result.ExclusionList.ShouldExclude("ExactPkg").ShouldBeTrue();
			result.ExclusionList.ShouldExclude("Test.Unit").ShouldBeTrue();
			result.ExclusionList.ShouldExclude("UnrelatedPackage").ShouldBeFalse();
		}

		// ── Include-archived short form ────────────────────────────────

		[Test]
		public void WhenShortIncludeArchivedThenConfigReflectsIt()
		{
			var args = MinimalValidArgs.Concat(["-a"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.Config.ShouldNotBeNull();
			result.Config.IncludeArchivedRepositories.ShouldBeTrue();
		}

		// ── Sync without package name ──────────────────────────────────

		[Test]
		public void WhenSyncWithoutPackageNameThenSyncConfigsIsEmpty()
		{
			var args = MinimalValidArgs.Concat(["--sync"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.SyncConfigs.ShouldBeEmpty();
		}

		// ── Sync with -sync alias ──────────────────────────────────────

		[Test]
		public void WhenSyncWithDashAliaThenSyncConfigIsPopulated()
		{
			var args = MinimalValidArgs.Concat(["-sync", "MyPackage", "2.0.0"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.SyncConfigs.Count.ShouldBe(1);
			result.SyncConfigs[0].PackageName.ShouldBe("MyPackage");
			result.SyncConfigs[0].TargetVersion.ShouldBe("2.0.0");
		}

		// ── Unknown argument ───────────────────────────────────────────

		[Test]
		public void WhenUnknownArgumentThenParsingContinues()
		{
			var args = MinimalValidArgs.Concat(["--unknown-arg"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.Config.ShouldNotBeNull();
		}

		// ── Config file merge with CLI-created UpdateConfig ────────────

		[Test]
		public void WhenCliSourceBranchAndFileUpdateConfigThenFieldsAreMerged()
		{
			var configPath = Path.Combine(Path.GetTempPath(), $"nugroom-test-{Guid.NewGuid()}.json");
			var config = new ToolConfig
			{
				Organization = "https://dev.azure.com/org",
				Token        = "token",
				Update       = new UpdateConfig
				{
					Scope              = UpdateScope.Minor,
					RequiredReviewers  = ["file-reviewer@example.com"]
				}
			};
			File.WriteAllText(configPath, JsonSerializer.Serialize(config));

			var result = CommandLineParser.Parse(
				["--config", configPath, "--source-branch", "main"]);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.SourceBranchPattern.ShouldBe("main");
			result.UpdateConfig.Scope.ShouldBe(UpdateScope.Minor);
			result.UpdateConfig.RequiredReviewers!.ShouldContain("file-reviewer@example.com");
		}

		// ── Export args without path ───────────────────────────────────

		[Test]
		public void WhenExportPackagesSpecifiedThenPathIsSet()
		{
			var args = MinimalValidArgs.Concat(["--export-packages", "packages.json"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.ExportPackagesPath.ShouldBe("packages.json");
		}

		[Test]
		public void WhenExportPackagesWithoutPathThenPathIsNull()
		{
			var args = MinimalValidArgs.Concat(["--export-packages"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.ExportPackagesPath.ShouldBeNull();
		}

		// ── Config file with VersionWarnings ───────────────────────────

		[Test]
		public void WhenConfigFileHasVersionWarningsThenTheyAreApplied()
		{
			var configPath = Path.Combine(Path.GetTempPath(), $"nugroom-test-{Guid.NewGuid()}.json");
			var config = new ToolConfig
			{
				Organization    = "https://dev.azure.com/org",
				Token           = "token",
				VersionWarnings = new VersionWarningConfig()
			};
			File.WriteAllText(configPath, JsonSerializer.Serialize(config));

			var result = CommandLineParser.Parse(["--config", configPath]);

			result.VersionWarningConfig.ShouldNotBeNull();
		}

		// ── Config file with ExcludeProjectPatterns ─────────────────────

		[Test]
		public void WhenConfigFileHasCsprojExclusionsThenTheyAreApplied()
		{
			var configPath = Path.Combine(Path.GetTempPath(), $"nugroom-test-{Guid.NewGuid()}.json");
			var config = new ToolConfig
			{
				Organization          = "https://dev.azure.com/org",
				Token                 = "token",
				ExcludeProjectPatterns = [".*\\.Tests\\.csproj"]
			};
			File.WriteAllText(configPath, JsonSerializer.Serialize(config));

			var result = CommandLineParser.Parse(["--config", configPath]);

			result.Config.ShouldNotBeNull();
			result.Config.ExcludeProjectPatterns.ShouldContain(".*\\.Tests\\.csproj");
		}

		// ── Config file with ExcludePatterns ───────────────────────────

		[Test]
		public void WhenConfigFileHasExcludePatternsThenTheyAreApplied()
		{
			var configPath = Path.Combine(Path.GetTempPath(), $"nugroom-test-{Guid.NewGuid()}.json");
			var config = new ToolConfig
			{
				Organization    = "https://dev.azure.com/org",
				Token           = "token",
				ExcludePatterns = ["^Test\\..*"]
			};
			File.WriteAllText(configPath, JsonSerializer.Serialize(config));

			var result = CommandLineParser.Parse(["--config", configPath]);

			result.ExclusionList.ExcludedPatterns.ShouldContain("^Test\\..*");
		}

		// ── Tag commits ───────────────────────────────────────────────

		[Test]
		public void WhenTagCommitsSpecifiedThenTagCommitsIsTrue()
		{
			var args = MinimalValidArgs.Concat(["--dry-run", "--tag-commits"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.TagCommits.ShouldBeTrue();
		}

		[Test]
		public void WhenTagCommitsNotSpecifiedThenTagCommitsIsFalse()
		{
			var args = MinimalValidArgs.Concat(["--dry-run"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.TagCommits.ShouldBeFalse();
		}

		[Test]
		public void WhenConfigFileHasTagCommitsThenItIsApplied()
		{
			var configPath = Path.Combine(Path.GetTempPath(), $"nugroom-test-{Guid.NewGuid()}.json");
			var config = new ToolConfig
			{
				Organization = "https://dev.azure.com/org",
				Token        = "token",
				Update       = new UpdateConfig { TagCommits = true }
			};
			File.WriteAllText(configPath, JsonSerializer.Serialize(config));

			var result = CommandLineParser.Parse(["--config", configPath]);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.TagCommits.ShouldBeTrue();
		}

		[Test]
		public void WhenCliTagCommitsAndFileTagCommitsFalseThenCliWins()
		{
			var configPath = Path.Combine(Path.GetTempPath(), $"nugroom-test-{Guid.NewGuid()}.json");
			var config = new ToolConfig
			{
				Organization = "https://dev.azure.com/org",
				Token        = "token",
				Update       = new UpdateConfig { TagCommits = false }
			};
			File.WriteAllText(configPath, JsonSerializer.Serialize(config));

			var result = CommandLineParser.Parse(
				["--config", configPath, "--tag-commits"]);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.TagCommits.ShouldBeTrue();
		}

		// ── Export warnings / recommendations / format ─────────────────

		[Test]
		public void WhenExportWarningsSpecifiedThenPathIsSet()
		{
			var args = MinimalValidArgs.Concat(["--export-warnings", "warnings.json"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.ExportWarningsPath.ShouldBe("warnings.json");
		}

		[Test]
		public void WhenExportWarningsWithoutPathThenPathIsNull()
		{
			var args = MinimalValidArgs.Concat(["--export-warnings"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.ExportWarningsPath.ShouldBeNull();
		}

		[Test]
		public void WhenExportRecommendationsSpecifiedThenPathIsSet()
		{
			var args = MinimalValidArgs.Concat(["--export-recommendations", "recs.json"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.ExportRecommendationsPath.ShouldBe("recs.json");
		}

		[Test]
		public void WhenExportRecommendationsWithoutPathThenPathIsNull()
		{
			var args = MinimalValidArgs.Concat(["--export-recommendations"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.ExportRecommendationsPath.ShouldBeNull();
		}

		[Test]
		public void WhenExportFormatJsonThenFormatIsJson()
		{
			var args = MinimalValidArgs.Concat(["--export-format", "Json"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.ExportFormat.ShouldBe(ExportFormat.Json);
		}

		[Test]
		public void WhenExportFormatCsvThenFormatIsCsv()
		{
			var args = MinimalValidArgs.Concat(["--export-format", "csv"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.ExportFormat.ShouldBe(ExportFormat.Csv);
		}

		[Test]
		public void WhenExportFormatNotSpecifiedThenDefaultIsJson()
		{
			var result = CommandLineParser.Parse(MinimalValidArgs);

			result.ExportFormat.ShouldBe(ExportFormat.Json);
		}

		[Test]
		public void WhenExportFormatInvalidThenDefaultIsJson()
		{
			var args = MinimalValidArgs.Concat(["--export-format", "xml"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.ExportFormat.ShouldBe(ExportFormat.Json);
		}

		[Test]
		public void WhenConfigFileHasExportWarningsThenPathIsApplied()
		{
			var configPath = Path.Combine(Path.GetTempPath(), $"nugroom-test-{Guid.NewGuid()}.json");
			var config = new ToolConfig
			{
				Organization       = "https://dev.azure.com/org",
				Token              = "token",
				ExportWarnings     = "file-warnings.json",
				ExportRecommendations = "file-recs.json",
				ExportFormat       = ExportFormat.Csv
			};
			File.WriteAllText(configPath, JsonSerializer.Serialize(config));

			var result = CommandLineParser.Parse(["--config", configPath]);

			result.ExportWarningsPath.ShouldBe("file-warnings.json");
			result.ExportRecommendationsPath.ShouldBe("file-recs.json");
			result.ExportFormat.ShouldBe(ExportFormat.Csv);
		}

		[Test]
		public void WhenCliExportFormatOverridesConfigFileThenCliWins()
		{
			var configPath = Path.Combine(Path.GetTempPath(), $"nugroom-test-{Guid.NewGuid()}.json");
			var config = new ToolConfig
			{
				Organization = "https://dev.azure.com/org",
				Token        = "token",
				ExportFormat = ExportFormat.Csv
			};
			File.WriteAllText(configPath, JsonSerializer.Serialize(config));

			var result = CommandLineParser.Parse(
				["--config", configPath, "--export-format", "Json"]);

			result.ExportFormat.ShouldBe(ExportFormat.Json);
		}

		// ── Include packages.config ───────────────────────────────────

		[Test]
		public void WhenIncludePackagesConfigFlagThenIncludePackagesConfigIsTrue()
		{
			var args = MinimalValidArgs.Concat(["--include-packages-config"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.IncludePackagesConfig.ShouldBeTrue();
		}

		[Test]
		public void WhenIncludePackagesConfigNotSpecifiedThenDefaultIsFalse()
		{
			var result = CommandLineParser.Parse(MinimalValidArgs);

			result.IncludePackagesConfig.ShouldBeFalse();
		}

		// ── Export SBOM ───────────────────────────────────────────────

		[Test]
		public void WhenExportSbomSpecifiedThenPathIsSet()
		{
			var args = MinimalValidArgs.Concat(["--export-sbom", "sbom.spdx.json"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.ExportSbomPath.ShouldBe("sbom.spdx.json");
		}

		[Test]
		public void WhenExportSbomWithoutPathThenPathIsNull()
		{
			var args = MinimalValidArgs.Concat(["--export-sbom"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.ExportSbomPath.ShouldBeNull();
		}

		// ── No incremental PRs ────────────────────────────────────────

		[Test]
		public void WhenNoIncrementalPrsFlagThenNoIncrementalPrsIsTrue()
		{
			var args = MinimalValidArgs.Concat(["--dry-run", "--no-incremental-prs"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.NoIncrementalPrs.ShouldBeTrue();
		}

		// ── Version increment options ─────────────────────────────────

		[Test]
		public void WhenIncrementProjectVersionThenVersionIncrementIsConfigured()
		{
			var args = MinimalValidArgs.Concat(["--dry-run", "--increment-project-version"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.VersionIncrement.ShouldNotBeNull();
			result.UpdateConfig.VersionIncrement.IncrementVersion.ShouldBeTrue();
			result.UpdateConfig.VersionIncrement.IncrementAssemblyVersion.ShouldBeFalse();
			result.UpdateConfig.VersionIncrement.IncrementFileVersion.ShouldBeFalse();
		}

		[Test]
		public void WhenIncrementProjectVersionWithScopeThenScopeIsApplied()
		{
			var args = MinimalValidArgs.Concat(["--dry-run", "--increment-project-version", "Minor"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.VersionIncrement.ShouldNotBeNull();
			result.UpdateConfig.VersionIncrement.IncrementVersion.ShouldBeTrue();
			result.UpdateConfig.VersionIncrement.Scope.ShouldBe(VersionIncrementScope.Minor);
		}

		[Test]
		public void WhenIncrementProjectAssemblyVersionThenFlagIsSet()
		{
			var args = MinimalValidArgs.Concat(["--dry-run", "--increment-project-assemblyversion"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.VersionIncrement.ShouldNotBeNull();
			result.UpdateConfig.VersionIncrement.IncrementAssemblyVersion.ShouldBeTrue();
			result.UpdateConfig.VersionIncrement.IncrementVersion.ShouldBeFalse();
			result.UpdateConfig.VersionIncrement.IncrementFileVersion.ShouldBeFalse();
		}

		[Test]
		public void WhenIncrementProjectFileVersionThenFlagIsSet()
		{
			var args = MinimalValidArgs.Concat(["--dry-run", "--increment-project-fileversion"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.VersionIncrement.ShouldNotBeNull();
			result.UpdateConfig.VersionIncrement.IncrementFileVersion.ShouldBeTrue();
			result.UpdateConfig.VersionIncrement.IncrementVersion.ShouldBeFalse();
			result.UpdateConfig.VersionIncrement.IncrementAssemblyVersion.ShouldBeFalse();
		}

		[Test]
		public void WhenIncrementProjectVersionAllThenAllFlagsAreSet()
		{
			var args = MinimalValidArgs.Concat(["--dry-run", "--increment-project-version-all"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.VersionIncrement.ShouldNotBeNull();
			result.UpdateConfig.VersionIncrement.IncrementVersion.ShouldBeTrue();
			result.UpdateConfig.VersionIncrement.IncrementAssemblyVersion.ShouldBeTrue();
			result.UpdateConfig.VersionIncrement.IncrementFileVersion.ShouldBeTrue();
		}

		[Test]
		public void WhenIncrementProjectVersionAllWithScopeThenScopeIsApplied()
		{
			var args = MinimalValidArgs.Concat(["--dry-run", "--increment-project-version-all", "Major"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.VersionIncrement.ShouldNotBeNull();
			result.UpdateConfig.VersionIncrement.IsEnabled.ShouldBeTrue();
			result.UpdateConfig.VersionIncrement.Scope.ShouldBe(VersionIncrementScope.Major);
		}

		[Test]
		public void WhenIncrementProjectVersionWithoutScopeThenDefaultScopeIsPatch()
		{
			var args = MinimalValidArgs.Concat(["--dry-run", "--increment-project-version"]).ToArray();

			var result = CommandLineParser.Parse(args);

			result.UpdateConfig.ShouldNotBeNull();
			result.UpdateConfig.VersionIncrement.ShouldNotBeNull();
			result.UpdateConfig.VersionIncrement.Scope.ShouldBe(VersionIncrementScope.Patch);
		}
	}
}
