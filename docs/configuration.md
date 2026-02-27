# Configuration

NuGroom supports a JSON configuration file for all settings. Use `--config settings.json` to load it.

---

## Full Configuration Example

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
  "ExcludeProjectPatterns": [
    ".*\\.Test[s]?\\.csproj$",
    ".*\\.UnitTests\\.csproj$",
    ".*\\.IntegrationTests\\.csproj$"
  ],
  "ExcludeRepositories": [
    "Legacy-.*",
    "Archive\\..*"
  ],
  "CaseSensitiveProjectFilters": false,
  "NoDefaultExclusions": false,
  "CaseSensitive": false,
  "ExportPackages": "report.json",
  "ExportWarnings": "warnings.json",
  "ExportRecommendations": "recommendations.json",
  "ExportSbom": "sbom.spdx.json",
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
    "NoIncrementalPrs": false,
    "VersionIncrement": {
      "IncrementVersion": true,
      "IncrementAssemblyVersion": true,
      "IncrementFileVersion": true,
      "Scope": "Patch"
    }
  }
}
```

---

## Configuration Fields

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
| `ExcludeProjectPatterns` | string[] | Project file regex exclusions | No |
| `ExcludeRepositories` | string[] | Repository name regex exclusions (case-insensitive) | No |
| `IncludeRepositories` | string[] | Repository name regex inclusions (case-insensitive) | No |
| `CaseSensitiveProjectFilters` | bool | Case-sensitive project file matching | No (default: false) |
| `NoDefaultExclusions` | bool | Disable default exclusions | No |
| `CaseSensitive` | bool | Case-sensitive matching | No |
| `ExportPackages` | string | Package export path (format controlled by `ExportFormat`) | No |
| `ExportWarnings` | string | Standalone warnings export path | No |
| `ExportRecommendations` | string | Standalone recommendations export path | No |
| `ExportFormat` | string | Format for all exports: `Json` or `Csv` | No (default: `Json`) |
| `ExportSbom` | string | SPDX 3.0.0 SBOM export path (always JSON-LD) | No |
| `IgnoreRenovate` | bool | Skip reading `renovate.json` from repositories | No (default: false) |
| `IncludePackagesConfig` | bool | Also scan legacy `packages.config` files | No (default: false) |

---

## Feed Object Format

```json
{
  "Name": "MyFeedName",
  "Url": "https://pkgs.dev.azure.com/yourorg/_packaging/Feed/nuget/v3/index.json"
}
```

---

## FeedAuth Object Format

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
  - `"USE_CURRENT_USER"` — Use current Windows/Azure credentials (recommended for Azure DevOps)
  - `"your-pat-token"` — Explicit Personal Access Token with package read permissions

---

## Private Feed Authentication

### Current User Authentication (Recommended for Azure DevOps)

For Azure DevOps feeds, the tool automatically uses your current Windows/Azure credentials if you're already signed in through Visual Studio or Azure CLI:

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

Create a JSON config file with FeedAuth entries and run:

```bash
NuGroom --config my-settings.json
```

### Creating Azure DevOps Feed PAT

1. Go to Azure DevOps → User Settings → Personal Access Tokens
2. Create new token with **Packaging (Read)** scope
3. Copy the token value
4. Add to config file or use with `--feed-auth`

### GitHub Packages Authentication

For GitHub packages, use a Personal Access Token with `read:packages` scope:

```bash
--feed https://nuget.pkg.github.com/OWNER/index.json \
--feed-auth "GitHubFeed|USERNAME|ghp_your_token"
```

---

## Environment Variable Resolution

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

---

## VersionWarnings Object Format

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
  - `"None"` — No warnings (default)
  - `"Major"` — Warn only on major version differences
  - `"Minor"` — Warn on major or minor version differences
  - `"Patch"` — Warn on major, minor, or patch version differences
- **PackageRules**: Optional array of package-specific override rules
  - **PackageName**: Exact package name (case-insensitive)
  - **Level**: Warning level for this specific package

---

## Update Configuration

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
| `VersionIncrement` | VersionIncrementConfig? | Project version increment configuration | `null` |

### VersionIncrementConfig Object Format

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `IncrementVersion` | bool | Increment `<Version>` property | `false` |
| `IncrementAssemblyVersion` | bool | Increment `<AssemblyVersion>` property | `false` |
| `IncrementFileVersion` | bool | Increment `<FileVersion>` property | `false` |
| `Scope` | string | Component to increment: `Patch`, `Minor`, or `Major` | `Patch` |

### PinnedPackage Object Format

| Field | Type | Description |
|-------|------|-------------|
| `PackageName` | string | Package ID to pin |
| `Version` | string? | Version to pin to (`null` = keep current) |
| `Reason` | string? | Optional reason for pinning |

---

> **See also:** [CLI Reference](cli-reference.md) · [Features](features.md) · [Automated Updates](automated-updates.md)
