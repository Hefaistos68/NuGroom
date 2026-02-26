using NuGroom.Configuration;

using NUnit.Framework;
using Shouldly;

namespace NuGroom.Tests.Configuration
{
	[TestFixture]
	public class ConfigLoaderApplyConfigTests
	{
		[Test]
		public void ApplyConfig_WhenMaxReposNotSet_SetsFromConfig()
		{
			var config = new ToolConfig
			{
				MaxRepos = 5
			};

			var context = new ApplyConfigContext();

			ConfigLoader.ApplyConfig(config, context);

			context.MaxRepos.ShouldBe(5);
		}

		[Test]
		public void ApplyConfig_WhenMaxReposAlreadySet_DoesNotOverride()
		{
			var config = new ToolConfig
			{
				MaxRepos = 5
			};

			var context = new ApplyConfigContext
			{
				MaxRepos = 200
			};

			ConfigLoader.ApplyConfig(config, context);

			context.MaxRepos.ShouldBe(200);
		}

		[Test]
		public void ApplyConfig_WhenBooleanFlagsUnset_SetsFromConfig()
		{
			var config = new ToolConfig
			{
				IncludeArchived      = true,
				ResolveNuGet         = false,
				Detailed             = true,
				NoDefaultExclusions  = true,
				CaseSensitive        = true,
				IgnoreRenovate       = true,
				IncludePackagesConfig = true
			};

			var context = new ApplyConfigContext();

			ConfigLoader.ApplyConfig(config, context);

			context.IncludeArchived.ShouldBe(true);
			context.ResolveNuGet.ShouldBe(false);
			context.ShowDetailedInfo.ShouldBe(true);
			context.NoDefaultExclusions.ShouldBe(true);
			context.CaseSensitive.ShouldBe(true);
			context.IgnoreRenovate.ShouldBe(true);
			context.IncludePackagesConfig.ShouldBe(true);
		}

		[Test]
		public void ApplyConfig_WhenListsProvided_MergesWithoutDuplicates()
		{
			var config = new ToolConfig
			{
				ExcludeRepositories   = ["Legacy-.*"],
				IncludeRepositories   = ["Active-.*"],
				ExcludePrefixes       = ["FilePrefix"],
				ExcludePackages       = ["FilePackage"],
				ExcludePatterns       = ["**/obj/**"],
				ExcludeProjectPatterns = ["*.Tests.csproj"],
				Feeds                 = [new Feed("ConfigFeed", "https://config/feed")],
				FeedAuth              = [new FeedAuth("ConfigFeed", "user", "pat")]
			};

			var context = new ApplyConfigContext();

			context.ExcludeRepositories.Add("Legacy-.*");
			context.Feeds.Add(new Feed("ConfigFeed", "https://existing/feed"));

			ConfigLoader.ApplyConfig(config, context);

			context.ExcludeRepositories.ShouldHaveSingleItem();
			context.IncludeRepositories.ShouldContain("Active-.*");
			context.ExcludePrefixes.ShouldContain("FilePrefix");
			context.ExcludePackages.ShouldContain("FilePackage");
			context.ExcludePatterns.ShouldContain("**/obj/**");
			context.ExcludeCsprojPatterns.ShouldContain("*.Tests.csproj");
			context.Feeds.ShouldHaveSingleItem();
			context.FeedAuth.ShouldHaveSingleItem();
		}

		[Test]
		public void ApplyConfig_WhenExportPathsUnset_SetsFromConfig()
		{
			var config = new ToolConfig
			{
				ExportPackages        = "packages.json",
				ExportWarnings        = "warnings.json",
				ExportRecommendations = "recs.json",
				ExportSbom            = "sbom.json",
				ExportFormat          = ExportFormat.Csv
			};

			var context = new ApplyConfigContext();

			ConfigLoader.ApplyConfig(config, context);

			context.ExportPackagesPath.ShouldBe("packages.json");
			context.ExportWarningsPath.ShouldBe("warnings.json");
			context.ExportRecommendationsPath.ShouldBe("recs.json");
			context.ExportSbomPath.ShouldBe("sbom.json");
			context.ExportFormat.ShouldBe(ExportFormat.Csv);
		}
	}
}
