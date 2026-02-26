# NuGroom - Find, list and update packages

A command-line tool that connects to Azure DevOps, searches all repositories for .csproj files, extracts PackageReference lines, and provides comprehensive package analysis including **multi-feed NuGet package information resolution with PAT authentication**.

## Features

- Connects to Azure DevOps using Personal Access Tokens
- Searches all repositories in an organization or specific project
- Finds all .csproj files in repositories
- **Project File Filtering**:
  - Exclude .csproj files by regex pattern (e.g., test projects)
  - Case-sensitive or case-insensitive matching
  - Pre-configured patterns for common test project types
- Extracts PackageReference entries using XML parsing with regex fallback
- **NuGet Package Resolution (multi-feed with authentication)**:
  - Automatically resolves package metadata from one or more NuGet feeds
  - Supports multiple feeds via repeated `--feed` arguments (defaults to NuGet.org)
  - **PAT authentication support for private feeds** (Azure DevOps, GitHub, etc.)
  - Shows package URLs, descriptions, authors, and publication dates
  - Identifies deprecated packages with warnings
  - Displays project URLs, license information, and tags
  - Detects packages not available on configured feeds
- **Version Warning System**:
  - Configurable warnings for version differences (Major, Minor, Patch)
  - Global default level and package-specific overrides
  - Warns about differences from latest available version
  - Warns about differences from latest used version in solution
  - Separate warnings report section with statistics
  - Export warnings to dedicated CSV file
  - **Automated update recommendations** - actionable list of packages to upgrade
  - Recommendations show current version ? recommended version with reasoning
- **Internal Package Source Detection**:
  - Cross-references packages not found on public feeds with project sources
  - Uses intelligent matching algorithms (exact match, prefix match, fuzzy matching)
  - Provides confidence scores for potential source projects
  - Identifies internal/private packages and their likely origins
  - Helps map dependencies to internal development teams
- **Automated Package Updates**:
  - Automatically create feature branches and pull requests with updated package versions
  - Configurable update scope: Patch, Minor, or Major
  - Separate source and target branch patterns with semver-based branch resolution
  - Falls back to repository default branch when no source branch is specified
  - Dry-run mode to preview planned changes without creating branches or PRs
  - Pin specific packages to prevent automatic updates
  - Filter updates to only packages with source code in scanned repositories
  - Projects ordered by dependency count (fewest first) within each repository
  - Optional lightweight git tagging of feature branch commits
  - Skip repositories with existing open NuGroom PRs (`--no-incremental-prs`)
- **Package Sync**:
  - Sync a specific package to an exact version across all repositories with a single command
  - Supports both upgrades and downgrades
  - If no version is specified, the latest available version from configured feeds is used
  - Creates feature branches and pull requests per affected repository
  - Respects Renovate `ignoreDeps` and disabled `packageRules`
  - Uses configured branch patterns, reviewers, and dry-run mode from Update configuration
- **Renovate Compatibility**:
  - Automatically reads `renovate.json` (or `.renovaterc`, `.renovaterc.json`, `.github/renovate.json`) from each repository
  - Respects `ignoreDeps` to exclude packages from scanning and updates per repository
  - Respects `packageRules` with `enabled: false` to skip disabled packages
  - Uses `reviewers` from Renovate config to override required PR reviewers per repository
  - No additional configuration needed — detection is automatic
- **Health & Risk Indicators**:
  - Marks deprecated packages
  - Marks outdated packages (projects using a version older than latest on feed)
  - Marks potentially vulnerable packages (heuristic keyword/date rules)
  - Dedicated summary sections for Deprecated, Outdated, Vulnerable packages
- **Configuration File Support**:
  - Load all settings from JSON config file via `--config` option
  - CLI arguments override config file values
  - Store feed credentials securely in config
  - **Environment variable resolution** for `Token` and `FeedAuth.Pat` fields (`$env:VAR` or `${VAR}` syntax)
- **Export Options**:
  - JSON export with detailed package information and version warnings
  - CSV export for spreadsheet analysis
  - Unified `--export-packages` option with format controlled by `--export-format`
  - Separate export for version warnings and recommendations
- **Configurable exclusion system** with multiple filtering options:
  - Prefix exclusions (e.g., "Microsoft.*", "System.*")
  - Exact package name exclusions (e.g., "Newtonsoft.Json")
  - Regular expression pattern exclusions (e.g., ".*\.Test.*")
  - Case-sensitive or case-insensitive matching
- **Enhanced package summary with version conflict detection**:
  - Total reference count per package
  - Number of different versions found
  - Breakdown of usage per version
  - Visual warnings for version conflicts
  - Overall statistics on version consistency
- **Debug Logging**:
  - Detailed diagnostic output for troubleshooting
  - Automatically enabled when debugger attached
  - Can be manually enabled via `--debug` flag
- Provides detailed output with repository, project, line number, and originating feed information

## Prerequisites

- .NET 10.0 or later
- Azure DevOps Personal Access Token with Code (Read) permissions
- For automatic updates: Azure DevOps PAT with **Code (Read & Write)** and **Pull Request Threads (Read & Write)** permissions
- Internet connection (for package resolution)
- For private feeds: Additional PAT tokens with package read permissions

## Usage

### Basic Usage (NuGet.org only)
```bash
NuGroom --organization "https://dev.azure.com/yourorg" --token "your-pat-token"
```

### Private Feed with PAT Authentication (CLI)
```bash
NuGroom -o "https://dev.azure.com/yourorg" -t "your-token" \
  --feed https://pkgs.dev.azure.com/yourorg/_packaging/MyFeed/nuget/v3/index.json \
  --feed-auth "https://pkgs.dev.azure.com/yourorg/_packaging/MyFeed/nuget/v3/index.json|VssSessionToken|your-feed-pat"
```

### Using Configuration File (Recommended for Private Feeds)
```bash
NuGroom --config settings.json
```

### Export Results as JSON (default)
```bash
NuGroom --config settings.json --export-packages report.json
```

### Export Results as CSV
```bash
NuGroom --config settings.json --export-packages packages.csv --export-format csv
```

### Export Warnings and Recommendations Separately
```bash
NuGroom --config settings.json --export-warnings warnings.json --export-recommendations recommendations.json
```

### Export Warnings as CSV
```bash
NuGroom --config settings.json --export-warnings warnings.csv --export-format csv
```

### Dry-Run Update Preview
```bash
NuGroom --config settings.json --dry-run --update-scope Patch
```

### Apply Package Updates (Creates Branches and PRs)
```bash
NuGroom --config settings.json --update-references --update-scope Minor
```

### Update and Tag Commits
```bash
NuGroom --config settings.json --update-references --tag-commits
```

### Skip Repositories with Existing NuGroom PRs
```bash
NuGroom --config settings.json --update-references --no-incremental-prs
```

### Update Only Internal Packages
```bash
NuGroom --config settings.json --update-references --source-packages-only
```

### Update with Separate Source and Target Branches
```bash
NuGroom --config settings.json --update-references \
  --source-branch "release/*" --target-branch "develop/*"
```

### Enable Debug Logging
```bash
NuGroom --config settings.json --debug
```

### Sync a Package to the Latest Version
```bash
NuGroom --config settings.json --sync Newtonsoft.Json
```

### Sync a Package to a Specific Version
```bash
NuGroom --config settings.json --sync Newtonsoft.Json 13.0.1
```

### Sync a Package (Dry-Run Preview)
```bash
NuGroom --config settings.json --sync Newtonsoft.Json 13.0.1 --dry-run
```

### Exclude Test Projects
```bash
NuGroom --config settings.json --exclude-csproj ".*\.Test[s]?\.csproj$"
```

### Exclude Repositories by Name Pattern
```bash
NuGroom --config settings.json --exclude-repo "Legacy-.*" --exclude-repo "Archive\..*"
```

### Multiple Private Feeds with Authentication
```bash
NuGroom -o "https://dev.azure.com/yourorg" -t "your-token" \
  --feed https://pkgs.dev.azure.com/org1/_packaging/Feed1/nuget/v3/index.json \
  --feed https://pkgs.dev.azure.com/org2/_packaging/Feed2/nuget/v3/index.json \
  --feed-auth "https://pkgs.dev.azure.com/org1/_packaging/Feed1/nuget/v3/index.json||pat1" \
  --feed-auth "https://pkgs.dev.azure.com/org2/_packaging/Feed2/nuget/v3/index.json||pat2"
```

## Command Line Options

### Required Options
| Option | Short | Description |
|--------|-------|-------------|
| `--organization` | `-o` | Azure DevOps organization URL |
| `--token` | `-t` | Personal Access Token for authentication |

### Optional Configuration
| Option | Short | Description |
|--------|-------|-------------|
| `--config` | | Load settings from JSON config file |
| `--project` | `-p` | Specific project name to search (searches all if not specified) |
| `--max-repos` | `-m` | Maximum repositories to process (default: 100) |
| `--include-archived` | `-a` | Include archived repositories (default: false) |

### Resolution Options
| Option | Description | Example |
|--------|-------------|---------|
| `--resolve-nuget` | Resolve package info from feeds (default: true) | `--resolve-nuget` |
| `--skip-nuget` | Skip resolution for faster processing | `--skip-nuget` |
| `--feed <url>` | Add a NuGet feed (repeatable) | `--feed https://pkgs.dev.azure.com/.../index.json` |
| `--feed-auth <auth>` | Feed authentication: `feedUrl\|username\|pat` (repeatable) | See examples below |

### Package Exclusion Options
| Option | Description | Example |
|--------|-------------|---------|
| `--exclude-prefix` | Exclude packages starting with prefix | `--exclude-prefix "Microsoft."` |
| `--exclude-package` | Exclude specific package by exact name | `--exclude-package "Newtonsoft.Json"` |
| `--exclude-pattern` | Exclude packages matching regex | `--exclude-pattern ".*\.Test.*"` |
| `--no-default-exclusions` | Include Microsoft.* and System.* | |
| `--case-sensitive` | Case-sensitive matching | |

### Project File Exclusion Options
| Option | Description | Example |
|--------|-------------|---------|
| `--exclude-csproj <pattern>` | Exclude .csproj files matching regex pattern | `--exclude-csproj ".*\.Test[s]?\.csproj$"` |
| `--case-sensitive-csproj` | Use case-sensitive .csproj file matching | |

### Repository Exclusion Options
| Option | Description | Example |
|--------|-------------|---------|
| `--exclude-repo <pattern>` | Exclude repositories matching regex pattern (repeatable, case-insensitive) | `--exclude-repo "Legacy-.*"` |

### Export Options
| Option | Description | Example |
|--------|-------------|---------|
| `--export-packages <path>` | Export package results to file (format via `--export-format`) | `--export-packages report.json` |
| `--export-warnings <path>` | Export version warnings to a separate file | `--export-warnings warnings.json` |
| `--export-recommendations <path>` | Export update recommendations to a separate file | `--export-recommendations recs.json` |
| `--export-format <format>` | Format for all exports: `Json` or `Csv` (default: `Json`) | `--export-format csv` |

### Display Options
| Option | Short | Description |
|--------|-------|-------------|
| `--detailed` | `-d` | Show detailed package info |
| `--debug` | | Enable debug logging (automatically enabled when debugger attached) |

### Update Options
| Option | Description | Default |
|--------|-------------|---------|
| `--update-references` | Enable auto-update mode (creates branches and PRs) | |
| `--dry-run` | Show planned updates without creating branches/PRs | |
| `--update-scope <scope>` | Version update scope: `Patch`, `Minor`, or `Major` | `Patch` |
| `--source-branch <pattern>` | Source branch pattern to branch from | default branch |
| `--target-branch <pattern>` | Target branch pattern for PR destination | `develop/*` |
| `--feature-branch <name>` | Feature branch name prefix | `feature/update-nuget-references` |
| `--source-packages-only` | Only update packages that have source code in scanned repositories | |
| `--required-reviewer <email>` | Add a required PR reviewer (repeatable, must approve) | |
| `--optional-reviewer <email>` | Add an optional PR reviewer (repeatable, notified only) | |
| `--tag-commits` | Create a lightweight git tag on the feature branch commit | |
| `--no-incremental-prs` | Skip repos that already have open NuGroom PRs (warns and skips) | |
| `--ignore-renovate` | Skip reading `renovate.json` from repositories | |

### Sync Options
| Option | Description | Default |
|--------|-------------|---------|
| `--sync <package> [version]` | Sync a specific package to a version across all repositories (creates PRs) | |
|  | If version is omitted, the latest available version from feeds is used | |

### Help
| Option | Short | Description |
|--------|-------|-------------|
| `--help` | `-h` | Show help |

## Private Feed Authentication

### Current User Authentication (Recommended for Azure DevOps)

For Azure DevOps feeds, the tool automatically uses your current Windows/Azure credentials if you're already signed in through Visual Studio or Azure CLI. This is the easiest option:

```json
{
  "Feeds": [
    {
      "Name": "MyCompanyFeed",
      "Url": "https://pkgs.dev.azure.com/yourorg/_packaging/YourFeed/nuget/v3/index.json"
    }
  ],
  "FeedAuth": [
    {
      "FeedName": "MyCompanyFeed",
      "Username": "",
      "Pat": "USE_CURRENT_USER"
    }
  ]
}
```

**Benefits:**
- No need to create or manage PAT tokens
- Uses your existing authentication
- Automatically works if you're signed into Visual Studio or Azure CLI
- More secure (no tokens stored in files)

**Automatic Fallback:**
Azure DevOps feeds without explicit `FeedAuth` configuration will automatically attempt current user authentication.

### PAT Authentication for Azure DevOps Feeds

If current user authentication doesn't work or you need explicit token control:

#### Option 1: Command Line (Quick Testing)
```bash
NuGroom -o "url" -t "token" \
  --feed https://pkgs.dev.azure.com/yourorg/_packaging/YourFeed/nuget/v3/index.json \
  --feed-auth "MyFeedName||your-feed-pat"
```

Format: `--feed-auth "feedName|username|pat"`
- **feedName**: Must match the feed name in configuration
- **username**: Optional, use empty string or "VssSessionToken" for Azure DevOps
- **pat**: Your Personal Access Token with package read permissions, or `USE_CURRENT_USER` for current user auth

#### Option 2: Configuration File (Recommended)
Create a JSON config file (see Configuration File Format below) with FeedAuth entries.

```bash
NuGroom --config my-settings.json
```

### Creating Azure DevOps Feed PAT

1. Go to Azure DevOps ? User Settings ? Personal Access Tokens
2. Create new token with **Packaging (Read)** scope
3. Copy the token value
4. Add to config file or use with `--feed-auth`

### GitHub Packages Authentication

For GitHub packages, use a Personal Access Token with `read:packages` scope:

```bash
--feed https://nuget.pkg.github.com/OWNER/index.json \
--feed-auth "GitHubFeed|USERNAME|ghp_your_token"
```

## Configuration File Format

Create a JSON file (e.g., `settings.json`) with your configuration:

```json
{
  "Organization": "https://dev.azure.com/yourorg",
  "Token": "your-azure-devops-pat",
  "Project": "MyProject",
  "MaxRepos": 500,
  "IncludeArchived": false,
  "ResolveNuGet": true,
  "Detailed": false,
  "Feeds": [
    {
      "Name": "NuGet.org",
      "Url": "https://api.nuget.org/v3/index.json"
    },
    {
      "Name": "InternalFeed",
      "Url": "https://pkgs.dev.azure.com/yourorg/_packaging/InternalFeed/nuget/v3/index.json"
    }
  ],
  "FeedAuth": [
    {
      "FeedName": "InternalFeed",
      "Username": "",
      "Pat": "USE_CURRENT_USER"
    }
  ],
  "VersionWarnings": {
    "DefaultLevel": "Minor",
    "PackageRules": [
      {
        "PackageName": "Newtonsoft.Json",
        "Level": "Major"
      },
      {
        "PackageName": "Serilog",
        "Level": "Patch"
      }
    ]
  },
  "ExcludePrefixes": ["Microsoft.", "System."],
  "ExcludePackages": [],
  "ExcludePatterns": [],
  "ExcludeCsprojPatterns": [
    ".*\\.Test[s]?\\.csproj$",
    ".*\\.UnitTests\\.csproj$",
    ".*\\.IntegrationTests\\.csproj$"
  ],
  "ExcludeRepositories": [
    "Legacy-.*",
    "Archive\\..*"
  ],
  "CaseSensitiveCsprojFilters": false,
  "NoDefaultExclusions": false,
  "CaseSensitive": false,
  "ExportPackages": "report.json",
  "ExportWarnings": "warnings.json",
  "ExportRecommendations": "recommendations.json",
  "ExportFormat": "Json",
  "Update": {
    "Scope": "Patch",
    "FeatureBranchName": "nugroom/update-nuget-references",
    "SourceBranchPattern": "develop/*",
    "TargetBranchPattern": "develop/*",
    "DryRun": true,
    "SourcePackagesOnly": false,
    "PinnedPackages": [
      {
        "PackageName": "Newtonsoft.Json",
        "Version": "13.0.1",
        "Reason": "Breaking changes in newer versions"
      },
      {
        "PackageName": "Serilog",
        "Version": null,
        "Reason": "Keep current version until logging migration is complete"
      }
    ],
    "RequiredReviewers": ["lead@company.com"],
    "OptionalReviewers": ["teammate@company.com"],
    "TagCommits": false,
    "NoIncrementalPrs": false
  }
}
```

### Configuration Fields

| Field | Type | Description | Required |
|-------|------|-------------|----------|
| `Organization` | string | Azure DevOps organization URL | Yes |
| `Token` | string | Azure DevOps PAT | Yes |
| `Project` | string | Specific project name | No |
| `MaxRepos` | int | Maximum repositories to scan | No (default: 100) |
| `IncludeArchived` | bool | Include archived repos | No (default: false) |
| `ResolveNuGet` | bool | Resolve package metadata | No (default: true) |
| `Detailed` | bool | Show detailed info | No (default: false) |
| `Feeds` | Feed[] | Named NuGet feeds | No (default: NuGet.org) |
| `FeedAuth` | FeedAuth[] | Feed authentication entries | No |
| `VersionWarnings` | VersionWarningConfig | Version warning configuration | No |
| `Update` | UpdateConfig | Automatic update configuration | No |
| `ExcludePrefixes` | string[] | Package prefix exclusions | No |
| `ExcludePackages` | string[] | Exact package exclusions | No |
| `ExcludePatterns` | string[] | Regex pattern exclusions | No |
| `ExcludeCsprojPatterns` | string[] | .csproj file regex exclusions | No |
| `ExcludeRepositories` | string[] | Repository name regex exclusions (case-insensitive) | No |
| `CaseSensitiveCsprojFilters` | bool | Case-sensitive .csproj matching | No (default: false) |
| `NoDefaultExclusions` | bool | Disable default exclusions | No |
| `CaseSensitive` | bool | Case-sensitive matching | No |
| `ExportPackages` | string | Package export path (format controlled by `ExportFormat`) | No |
| `ExportWarnings` | string | Standalone warnings export path | No |
| `ExportRecommendations` | string | Standalone recommendations export path | No |
| `ExportFormat` | string | Format for all exports: `Json` or `Csv` | No (default: `Json`) |
| `IgnoreRenovate` | bool | Skip reading `renovate.json` from repositories | No (default: false) |

### Feed Object Format

```json
{
  "Name": "MyFeedName",
  "Url": "https://pkgs.dev.azure.com/yourorg/_packaging/Feed/nuget/v3/index.json"
}
```

### FeedAuth Object Format

```json
{
  "FeedName": "MyFeedName",
  "Username": "",
  "Pat": "USE_CURRENT_USER"
}
```

**or with explicit PAT:**
```json
{
  "FeedName": "MyFeedName",
  "Username": "VssSessionToken",
  "Pat": "your-pat-token-here"
}
```

- **FeedName**: Must exactly match the feed name in the `Feeds` array
- **Username**: Optional, use `""` or `"VssSessionToken"` for Azure DevOps
- **Pat**: 
  - `"USE_CURRENT_USER"` - Use current Windows/Azure credentials (recommended for Azure DevOps)
  - `"your-pat-token"` - Explicit Personal Access Token with package read permissions

### Environment Variable Resolution

To avoid storing secrets directly in configuration files, the `Token` and `FeedAuth.Pat` fields support environment variable references. Two syntaxes are supported:

| Syntax | Style | Example |
|--------|-------|---------|
| `$env:VAR_NAME` | PowerShell | `"Token": "$env:ADO_PAT"` |
| `${VAR_NAME}` | Shell | `"Token": "${ADO_PAT}"` |

When the config file is loaded, any value matching one of these patterns is replaced with the corresponding environment variable value. If the variable is not set, the original placeholder is kept as-is.

**Example config using environment variables:**
```json
{
  "Organization": "https://dev.azure.com/yourorg",
  "Token": "$env:ADO_PAT",
  "Feeds": [
    {
      "Name": "InternalFeed",
      "Url": "https://pkgs.dev.azure.com/yourorg/_packaging/Feed/nuget/v3/index.json"
    }
  ],
  "FeedAuth": [
    {
      "FeedName": "InternalFeed",
      "Username": "",
      "Pat": "${FEED_PAT}"
    }
  ]
}
```

**Azure DevOps Pipeline example:**
```yaml
variables:
  ADO_PAT: $(System.AccessToken)
  FEED_PAT: $(System.AccessToken)

steps:
  - script: NuGroom --config settings.json
    env:
      ADO_PAT: $(ADO_PAT)
      FEED_PAT: $(FEED_PAT)
```

This approach keeps secrets out of source control and works naturally with Azure DevOps pipeline variables, GitHub Actions secrets, or any CI/CD system that injects environment variables.

### VersionWarnings Object Format

```json
{
  "DefaultLevel": "Minor",
  "PackageRules": [
    {
      "PackageName": "Newtonsoft.Json",
      "Level": "Major"
    }
  ]
}
```

- **DefaultLevel**: Global warning level for all packages
  - `"None"` - No warnings (default)
  - `"Major"` - Warn only on major version differences
  - `"Minor"` - Warn on major or minor version differences
  - `"Patch"` - Warn on major, minor, or patch version differences
- **PackageRules**: Optional array of package-specific override rules
  - **PackageName**: Exact package name (case-insensitive)
  - **Level**: Warning level for this specific package

## Version Warnings

The version warning system helps identify version inconsistencies across your repositories:

### Warning Types

1. **Version Mismatch (Used)**: Package version differs from the latest version used in other projects
2. **Version Mismatch (Available)**: Package version differs from the latest available version on feeds

### Warning Levels

- **Major**: Only warn about major version differences (e.g., 1.x.x vs 2.x.x)
- **Minor**: Warn about major or minor differences (e.g., 1.1.x vs 1.2.x)
- **Patch**: Warn about any version difference (e.g., 1.1.1 vs 1.1.2)
- **None**: Disable warnings

### Configuration Examples

#### Warn About Minor Version Differences Globally
```json
{
  "VersionWarnings": {
    "DefaultLevel": "Minor"
  }
}
```

#### Package-Specific Rules
```json
{
  "VersionWarnings": {
    "DefaultLevel": "Minor",
    "PackageRules": [
      {
        "PackageName": "Newtonsoft.Json",
        "Level": "Major"
      },
      {
        "PackageName": "Microsoft.Extensions.Logging",
        "Level": "None"
      }
    ]
  }
}
```

### Output Example

```
VERSION WARNINGS
================================================================================

Newtonsoft.Json:
  ? Repository1/Project1.csproj
    Package version 12.0.3 differs from latest available version 13.0.3 (major version difference)

Serilog:
  ? Repository2/Project2.csproj
    Package version 2.10.0 differs from latest used version 2.12.0 (minor version difference)

--------------------------------------------------------------------------------
Total warnings: 2
Packages with warnings: 2
Major version differences: 1
Minor version differences: 1
```

## Project File Filtering

Exclude specific .csproj files from analysis using regex patterns:

### Common Use Cases

**Exclude Test Projects:**
```json
{
  "ExcludeCsprojPatterns": [
    ".*\\.Test[s]?\\.csproj$",
    ".*\\.UnitTests\\.csproj$",
    ".*\\.IntegrationTests\\.csproj$"
  ]
}
```

**Exclude Specific Project Types:**
```json
{
  "ExcludeCsprojPatterns": [
    ".*\\.Benchmarks\\.csproj$",
    ".*\\.Samples\\.csproj$",
    ".*\\.Demo\\.csproj$"
  ]
}
```

**Case-Sensitive Matching:**
```json
{
  "ExcludeCsprojPatterns": [".*\\.TEST\\.csproj$"],
  "CaseSensitiveCsprojFilters": true
}
```

## Multi-Feed Resolution

Specify one or more feeds with `--feed`. The resolver queries each feed until a package is found, selecting the first match. The originating feed host is displayed next to each package.

Benefits:
- Combine public + private feeds in a single scan
- Authenticate to private feeds with PAT tokens
- Detect internal packages published to private feeds
- See which feed supplied the version information
- Stops searching after first successful resolution (improved performance)

## Health & Risk Indicators

The tool flags packages with risk indicators:
- `[DEPRECATED]` Package is unlisted on the feed
- `[OUTDATED]` Your solution uses a version lower than the latest available
- `[VULNERABLE]` Heuristic detection (keywords in description or very old publish date)

Summary sections list all Deprecated, Outdated, and Vulnerable packages with details.

## Export Formats

### JSON Export
Human-readable JSON with:
- Generation timestamp
- Project and package counts
- Detailed package information per project
- Feed source, versions, and status flags
- Version warnings section (when configured)
- Package update recommendations (when version warnings configured)

### CSV Export
Spreadsheet-compatible format with columns:
- Repository, ProjectPath
- PackageName, Version, LatestVersion
- Feed, Deprecated, Outdated, Vulnerable flags
- Status, SourceRepository, SourceProject

When version warnings are configured and the export format is CSV, additional CSV files are automatically generated:
- `packages-warnings.csv` - Contains all version warnings with details
- `packages-recommendations.csv` - Contains package update recommendations with current and target versions

## Package Update Recommendations

When version warnings are configured, the tool automatically generates actionable package update recommendations in a dedicated "RECOMMENDATIONS" section at the end of the report.

### What Gets Recommended

The tool recommends updates for packages where:
1. **Version warnings are configured** (global or package-specific)
2. **A newer version exists** that differs according to the warning level
3. The package should be updated to either:
   - **Latest available version** from configured feeds (preferred)
   - **Latest version already used** in other projects in the solution

### Recommendation Output

The recommendations section shows:
- Package name and recommended version
- All projects that need updating
- Current version ? Recommended version
- Reason for the recommendation
- Summary statistics (total recommendations, packages affected, projects impacted)

### Example Output

```
PACKAGE UPDATE RECOMMENDATIONS
================================================================================
The following projects should update their package versions:

Newtonsoft.Json:
  Recommended version: 13.0.3

  • MyRepository/MyProject.csproj
    Current: 12.0.3 ? Upgrade to: 13.0.3
    Upgrade to latest available version (currently major version behind)

  • MyRepository/AnotherProject.csproj
    Current: 11.0.1 ? Upgrade to: 13.0.3
    Upgrade to latest available version (currently major version behind)

Serilog:
  Recommended version: 2.12.0

  • DifferentRepository/Logger.csproj
    Current: 2.10.0 ? Upgrade to: 2.12.0
    Align with latest version used in solution (currently minor version behind)

--------------------------------------------------------------------------------
Total update recommendations: 3
Packages needing update: 2
Projects affected: 3
--------------------------------------------------------------------------------
```

### Export Formats

**JSON Export:**
Includes a `recommendations` section with:
- Total recommendations count
- Packages needing update count
- Projects affected count
- Detailed list of all recommendations

**CSV Export:**
Automatically creates `packages-recommendations.csv` with columns:
- PackageName
- Repository
- ProjectPath
- CurrentVersion
- RecommendedVersion
- RecommendationType (latest-available or latest-used)
- Reason

### Configuration

Recommendations are automatically generated whenever version warnings are configured. No additional configuration is needed.

```json
{
  "VersionWarnings": {
    "DefaultLevel": "Minor"
  }
}
```

To disable recommendations, set the warning level to `"None"`:

```json
{
  "VersionWarnings": {
    "DefaultLevel": "None"
  }
}
```

## Automated Package Updates

The tool can automatically create feature branches and pull requests to update outdated package references across your Azure DevOps repositories.

### How It Works

1. **Scan** — repositories are scanned and NuGet metadata is resolved (as usual)
2. **Plan** — the tool compares used versions against latest available and builds an update plan within the configured scope
3. **Preview** (dry-run) — the plan is displayed showing what branches and PRs would be created
4. **Apply** — feature branches are created from the source branch, updated `.csproj` files are pushed, and PRs are opened against the target branch

Projects within each repository are processed in order of dependency count (fewest dependencies first).

### Update Scope

The scope controls the maximum version change that will be applied. Scopes are cumulative:

| Scope | Allowed Updates | Example |
|-------|----------------|---------|
| `Patch` | Patch only | `1.2.3` ? `1.2.5` |
| `Minor` | Minor and patch | `1.2.3` ? `1.4.0` |
| `Major` | Any version | `1.2.3` ? `2.0.0` |

### Branch Resolution

- **Source branch** — the branch to read current file content from and create the feature branch from. Resolved via pattern matching with semver ordering (e.g., `develop/*` picks the latest `develop/1.2.3` branch). If not specified, the repository's **default branch** is used.
- **Target branch** — the PR destination. Also resolved via pattern matching with semver ordering.
- **Feature branch** — created with a timestamp suffix, e.g., `feature/update-nuget-references-20250224-155601`.

### Dry-Run Mode

Dry-run is enabled by default in configuration (`"DryRun": true`). Use `--dry-run` on the CLI or set `"DryRun": false` / `--update-references` to apply changes.

In dry-run mode the tool shows:
- All planned package version updates per repository and project
- The feature branch name that would be created
- The source branch it would branch from
- The PR title and target branch
- A total count of branches and PRs that would be created

### Pinned Packages

Pin packages to prevent automatic updates:

```json
{
  "Update": {
    "PinnedPackages": [
      {
        "PackageName": "Newtonsoft.Json",
        "Version": "13.0.1",
        "Reason": "Breaking changes in newer versions"
      },
      {
        "PackageName": "Serilog",
        "Version": null,
        "Reason": "Keep current version until logging migration is complete"
      }
    ]
  }
}
```

- **Version set to a value**: the package stays at that exact version
- **Version set to null**: the package keeps whatever version is currently used

### Source Packages Only

Use `--source-packages-only` or `"SourcePackagesOnly": true` to restrict updates to packages that have identified source projects in the scanned repositories. This is useful when you only want to update internal/private packages and leave third-party packages unchanged.

### PR Reviewers

Add required and optional reviewers to created pull requests:

```json
{
  "Update": {
    "RequiredReviewers": ["lead@company.com", "security@company.com"],
    "OptionalReviewers": ["teammate@company.com"]
  }
}
```

Or via CLI (both are repeatable):
```bash
--required-reviewer "lead@company.com" --optional-reviewer "teammate@company.com"
```

- **Required reviewers** must approve before the PR can be completed
- **Optional reviewers** are notified but their approval is not required
- The same identity **cannot** appear in both lists — the tool validates this before making any API calls
- Reviewers are resolved by email address or Azure DevOps unique name via the Identity API

### Update Configuration

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `Scope` | string | Update scope: `Patch`, `Minor`, or `Major` | `Patch` |
| `FeatureBranchName` | string | Feature branch name prefix | `nugroom/update-nuget-references` |
| `SourceBranchPattern` | string? | Source branch pattern (e.g., `develop/*`) | default branch |
| `TargetBranchPattern` | string | Target branch pattern for PRs | `develop/*` |
| `DryRun` | bool | Preview only, no branches/PRs created | `true` |
| `SourcePackagesOnly` | bool | Only update packages with source code | `false` |
| `PinnedPackages` | PinnedPackage[] | Packages to exclude from updates | `[]` |
| `RequiredReviewers` | string[]? | Email addresses or unique names of required PR reviewers | `null` |
| `OptionalReviewers` | string[]? | Email addresses or unique names of optional PR reviewers | `null` |
| `TagCommits` | bool | Create a lightweight git tag on each feature branch commit | `false` |
| `NoIncrementalPrs` | bool | Skip repositories that already have open NuGroom PRs | `false` |

### PinnedPackage Object Format

| Field | Type | Description |
|-------|------|-------------|
| `PackageName` | string | Package ID to pin |
| `Version` | string? | Version to pin to (`null` = keep current) |
| `Reason` | string? | Optional reason for pinning |

### Example Output (Dry-Run)

```
================================================================================
DRY RUN - UPDATE PLAN (no changes will be made)
================================================================================
Update scope: Patch
Source branch: develop/*
Target branch pattern: develop/*
Feature branch prefix: feature/update-nuget-references
Required reviewers: lead@company.com
Optional reviewers: teammate@company.com

Repository: MyRepository
--------------------------------------------------
  /src/Core/Core.csproj (5 total dependencies)
    Azure.Storage.Blobs: 12.26.0 ? 12.27.0
  /src/Api/Api.csproj (12 total dependencies)
    Azure.Storage.Blobs: 12.26.0 ? 12.27.0
    CloudConvert.API: 1.4.0 ? 1.4.1
  [Would create] Branch: feature/update-nuget-references-{timestamp} from develop/*
  [Would create] PR: "chore: update 3 NuGet package reference(s) (Patch scope)" ? develop/*

Total: 3 update(s) across 1 repository(ies)
Would create 1 feature branch(es) and 1 pull request(s).
Run with --update-references (without --dry-run) to apply changes.
```

### CLI Precedence

CLI arguments always override config file values. When both are provided, the CLI value wins. This applies to all settings including update options, boolean flags, and branch patterns.

## Package Sync

The `--sync` option lets you force a specific package to an exact version across all repositories in a single operation. Unlike `--update-references` which updates many packages within a scope, `--sync` targets one package and supports both upgrades and downgrades.

### Usage

```bash
# Sync to latest available version
NuGroom --config settings.json --sync Newtonsoft.Json

# Sync to a specific version (upgrade or downgrade)
NuGroom --config settings.json --sync Newtonsoft.Json 13.0.1

# Preview what would change
NuGroom --config settings.json --sync Newtonsoft.Json 13.0.1 --dry-run
```

### How It Works

1. If no version is provided, the latest stable version is resolved from configured feeds
2. All repositories are scanned for `.csproj` files referencing the specified package
3. Projects already at the target version are skipped
4. For each affected repository, a feature branch is created, changes are pushed, and a PR is opened

### Behavior

- **Upgrades and downgrades** — unlike `--update-references`, `--sync` has no scope restriction. It always sets the exact target version.
- **Dry-run** — uses the `DryRun` setting from `UpdateConfig`. Pass `--dry-run` or set `"DryRun": true` to preview changes.
- **Branch patterns** — uses `SourceBranchPattern` and `TargetBranchPattern` from `UpdateConfig`.
- **Reviewers** — uses `RequiredReviewers` / `OptionalReviewers` from `UpdateConfig`, with Renovate `reviewers` override per repository.
- **Renovate** — respects `ignoreDeps` and disabled `packageRules`. If the package is excluded by Renovate in a repository, that repository is skipped.

### Example Output (Dry-Run)

```
================================================================================
SYNC: Newtonsoft.Json ? 13.0.1
================================================================================
Scanning 50 repository(ies) for Newtonsoft.Json...

Repository: MyRepository
--------------------------------------------------
  /src/Core/Core.csproj: 12.0.3 ? 13.0.1
  /src/Api/Api.csproj: 11.0.1 ? 13.0.1
  [Would create] Branch + PR to sync Newtonsoft.Json to 13.0.1

--------------------------------------------------------------------------------
DRY RUN: Would sync 1 repository(ies). 49 already at 13.0.1.
Run without --dry-run (or set "DryRun": false) to apply changes.
```

## Renovate Compatibility

The tool automatically detects and respects [Renovate](https://docs.renovatebot.com/) configuration files in scanned repositories. This allows teams already using Renovate to maintain a single source of truth for dependency management rules.

### Detection

The following file paths are checked in priority order:
1. `/renovate.json`
2. `/.renovaterc`
3. `/.renovaterc.json`
4. `/.github/renovate.json`

The first file found is used. If no Renovate config exists, the repository is processed normally.

To disable Renovate detection entirely, use `--ignore-renovate` on the CLI or `"IgnoreRenovate": true` in the config file.

### Supported Fields

| Renovate Field | Effect |
|----------------|--------|
| `ignoreDeps` | Listed packages are excluded from scanning and update plans for this repository |
| `packageRules[].matchPackageNames` + `enabled: false` | Matched packages are excluded from scanning and update plans |
| `reviewers` | Overrides the tool's `RequiredReviewers` for PRs created against this repository |
| `packageRules[].matchPackageNames` + `reviewers` | Stored per-package for future use |

### Example `renovate.json`

```json
{
  "$schema": "https://docs.renovatebot.com/renovate-schema.json",
  "ignoreDeps": [
    "Newtonsoft.Json",
    "Castle.Core"
  ],
  "reviewers": [
    "lead@company.com",
    "security@company.com"
  ],
  "packageRules": [
    {
      "matchPackageNames": ["Serilog", "Serilog.Sinks.Console"],
      "enabled": false
    }
  ]
}
```

With this configuration:
- `Newtonsoft.Json` and `Castle.Core` are completely excluded from the scan
- `Serilog` and `Serilog.Sinks.Console` are excluded from the scan
- PRs for this repository use `lead@company.com` and `security@company.com` as required reviewers instead of the tool's configured reviewers

### Precedence

- **ignoreDeps / packageRules** — applied during scanning, before NuGet resolution. These packages won't appear in reports or update plans.
- **reviewers** — applied during PR creation. Renovate reviewers **replace** (not merge with) the tool's `RequiredReviewers` for that repository. `OptionalReviewers` from the tool config are still added.
- Repositories without a Renovate config use the tool's configured reviewers as usual.

### Console Output

When a Renovate config is detected, the scan log shows:
```
[1/50] Scanning repository: MyRepository
  Renovate config found (2 ignored, 2 disabled)
```

## Debug Logging

Enable detailed diagnostic output for troubleshooting:

### Automatic Enabling
Debug logging is automatically enabled when running with a debugger attached (e.g., from Visual Studio).

### Manual Enabling
Use the `--debug` flag to enable debug logging in production:

```bash
NuGroom --config settings.json --debug
```

### Debug Output Includes
- Feed initialization details
- Authentication method used
- Package resolution attempts
- Cache hits/misses
- API call results
- Error details and stack traces

Debug messages are displayed in gray color with `[DEBUG]` prefix and timestamp.

## Output Example
```
NuGet feeds:
  - NuGet.org: https://api.nuget.org/v3/index.json
  - InternalFeed: https://pkgs.dev.azure.com/yourorg/_packaging/YourFeed/nuget/v3/index.json

Version warning level: Minor
Package-specific rules: 2 configured

Repository: MyRepository
--------------------------------------------------
  Project: /src/MyProject/MyProject.csproj
    - Serilog (Version: 2.12.0) [Feed: NuGet.org] ? [https://www.nuget.org/packages/Serilog]
    - AutoMapper (Version: 11.0.1) [Feed: NuGet.org] ? [OUTDATED]
    - MyCompany.Core (Version: 1.2.0) [Feed: InternalFeed] ?
    - InternalLib (Version: 1.0.0) [Not found on feeds]
      ?? Source: InternalLib in CoreLibraries (95% match)

PACKAGE SUMMARY
----------------
Serilog: 3 reference(s) across 1 version ? [Feed: NuGet.org]
AutoMapper: 5 reference(s) across 2 versions ? [OUTDATED] [Feed: NuGet.org]
  ?? v11.0.1: 3 reference(s)
  ?? v10.0.0: 2 reference(s)
  ?? Multiple versions detected for AutoMapper
MyCompany.Core: 4 reference(s) across 1 version ? [Feed: InternalFeed]
InternalLib: 2 reference(s) across 1 version [INTERNAL]

Total unique packages: 4
Available on NuGet.org: 2
Available on other feeds: 1
Internal packages (with source): 1
Outdated packages: 1

VERSION WARNINGS
================================================================================

AutoMapper:
  ? MyRepository/Project1.csproj
    Package version 10.0.0 differs from latest used version 11.0.1 (minor version difference)
  ? MyRepository/Project2.csproj
    Package version 10.0.0 differs from latest available version 12.0.0 (major version difference)

--------------------------------------------------------------------------------
Total warnings: 2
Packages with warnings: 1
Major version differences: 1
Minor version differences: 1

PACKAGE UPDATE RECOMMENDATIONS
================================================================================
The following projects should update their package versions:

AutoMapper:
  Recommended version: 12.0.0

  • MyRepository/Project1.csproj
    Current: 10.0.0 ? Upgrade to: 12.0.0
    Upgrade to latest available version (currently major version behind)

  • MyRepository/Project2.csproj
    Current: 11.0.1 ? Upgrade to: 12.0.0
    Upgrade to latest available version (currently minor version behind)

--------------------------------------------------------------------------------
Total update recommendations: 2
Packages needing update: 1
Projects affected: 2
--------------------------------------------------------------------------------
```

## Performance & Concurrency

Resolution is parallelized (default 5 concurrent requests) and results are cached per run. Feeds are queried in order until a package is found, then searching stops for improved performance.

## Security Best Practices

1. **Store PATs securely**: Use config files with appropriate file permissions
2. **Use least-privilege tokens**: Only grant package read permissions
3. **Rotate tokens regularly**: Update config files when tokens expire
4. **Don't commit tokens**: Add config files with credentials to `.gitignore`
5. **Use environment variables**: Consider loading tokens from environment for CI/CD
6. **Review debug output**: Debug logs may contain sensitive information; use carefully in production

## Troubleshooting

### Enable Debug Logging
For detailed diagnostic information, add the `--debug` flag:
```bash
NuGroom --config settings.json --debug
```

### Private Feed Authentication Fails
- Verify feed URL exactly matches between `Feeds` and `FeedAuth`
- Ensure PAT has **Packaging (Read)** permissions
- Check PAT hasn't expired
- For Azure DevOps, try username `"VssSessionToken"` or empty string
- Enable debug logging to see authentication details

### Package Not Found on Private Feed
- Confirm package exists in the feed
- Verify authentication is working (check feed URL in output with `--debug`)
- Ensure feed URL uses v3 API format: `.../nuget/v3/index.json`

### JSON Configuration File Fails to Load
- Verify JSON syntax is valid
- Check enum values are strings (e.g., `"Minor"` not `Minor`)
- Ensure feed names in `FeedAuth` match names in `Feeds` array exactly
- Enable debug logging to see detailed error messages

### Version Warnings Not Appearing
- Verify `VersionWarnings.DefaultLevel` is not `"None"`
- Check that `ResolveNuGet` is `true` (default)
- Ensure packages are being resolved successfully (check feed access)
- Review package-specific rules for conflicting configurations

## Notes
- Vulnerability detection is heuristic; integrate with security advisories for production use
- Feed authentication uses NuGet's built-in credential system
- Credentials are stored in memory only and never logged (even in debug mode)
- Version warnings compare semantic versions; invalid version strings are ignored
- Feed resolution stops at first successful match for improved performance
- Update workflow requires Azure DevOps PAT with **Code (Read & Write)** and **Pull Request Threads (Read & Write)** permissions
- Dry-run mode is enabled by default to prevent accidental changes
- Pinned packages are never updated, even if a newer version is within scope
- Renovate `ignoreDeps` and disabled `packageRules` are applied per-repository during scanning
- Renovate `reviewers` override `RequiredReviewers` per-repository; `OptionalReviewers` are unaffected

## Roadmap
- ? Version warning system with configurable levels
- ? Automated package update recommendations
- ? Automated package updates with branch creation and PR workflow
- ? Renovate compatibility (ignoreDeps, packageRules, reviewers)
- Integration with official vulnerability databases
- Graph visualization of internal dependencies
- Support for Azure Key Vault credential storage
