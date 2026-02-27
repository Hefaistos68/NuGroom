# Features

Detailed documentation of NuGroom's analysis and management features.

---

## Central Package Management (CPM)

The tool automatically detects and supports [Central Package Management](https://learn.microsoft.com/nuget/consume-packages/central-package-management) in scanned repositories.

### How It Works

1. When scanning a repository, the tool looks for `Directory.Packages.props` files
2. If the file contains `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`, CPM is active
3. All `<PackageVersion Include="..." Version="..."/>` entries are read into a version lookup
4. Project files with `<PackageReference>` entries that omit a `Version` attribute have their versions populated from the CPM lookup
5. If a project file specifies `VersionOverride`, that version takes precedence over the central version

### Auto-Update Behavior

When CPM is active, package version updates target `Directory.Packages.props` instead of individual project files. This means a single update to the central props file can update the version for all projects in the repository.

No configuration is needed — CPM detection is automatic.

### Migrating to CPM

For repositories that do not yet use Central Package Management, the `--migrate-to-cpm` option generates a `Directory.Packages.props` file and updates all project files to remove inline `Version` attributes.

**Dry-run preview:**

```bash
NuGroom --config settings.json --migrate-to-cpm --dry-run
```

**Apply migration (creates branches and PRs):**

```bash
NuGroom --config settings.json --migrate-to-cpm --update-references
```

#### How Migration Works

1. All scanned project files with explicit `<PackageReference ... Version="..."/>` are collected
2. Package versions are merged across projects — the **highest version** of each package becomes the central version in `Directory.Packages.props`
3. If different projects reference **different versions** of the same package, the project with the lower version receives a `VersionOverride` attribute and a warning is emitted
4. A `Directory.Packages.props` file is created at the repository root (or per project with `--per-project`)
5. Each project file is modified to remove inline `Version` attributes from `<PackageReference>` elements

#### Version Conflict Resolution

When multiple projects reference different versions of the same package, the migration resolves the conflict as follows:

- The **highest version** is used as the central version in `Directory.Packages.props`
- Projects using a **lower version** receive a `VersionOverride` attribute to preserve their current version
- A warning is emitted for each conflict, stating the package name, project, and both versions

For example, given two projects:
- `App1.csproj` references `Newtonsoft.Json` version `13.0.3`
- `App2.csproj` references `Newtonsoft.Json` version `12.0.0`

The migration produces:

**Directory.Packages.props** (central version: highest)

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
</Project>
```

**App1.csproj** (version removed — uses central version)

```xml
<PackageReference Include="Newtonsoft.Json" />
```

**App2.csproj** (gets VersionOverride to preserve lower version)

```xml
<PackageReference Include="Newtonsoft.Json" VersionOverride="12.0.0" />
```

**Warning emitted:**

```
Version conflict: package 'Newtonsoft.Json' in project 'src/App2/App2.csproj'
  uses version 12.0.0 (VersionOverride) while central version is 13.0.3
```

#### Per-Project Mode

By default, `--migrate-to-cpm` creates a single `Directory.Packages.props` at the repository root. Use `--per-project` to create a separate file alongside each project file instead:

```bash
NuGroom --config settings.json --migrate-to-cpm --per-project --dry-run
```

In per-project mode:
- Each project gets its own `Directory.Packages.props` in the same directory
- No version conflicts occur because each project manages its own versions independently
- This is useful for repositories where projects have intentionally different package versions

#### Target Branch Auto-Creation

When the CPM migration creates pull requests and the configured target branch does not exist, the tool automatically creates it from the source branch instead of skipping the repository.

- If `TargetBranchPattern` is an exact branch name (no wildcards), it is created as a new branch from the source branch
- If no target pattern is configured, the repository's default branch name is used
- Wildcard patterns (e.g. `release/*`) that match no branches cannot be auto-created and will still skip

This allows migrations to target branches that do not yet exist, for example when introducing a new branching strategy alongside the CPM migration.

---

## Legacy packages.config Support

For repositories that still use the legacy `packages.config` format (common in .NET Framework projects), the tool can optionally scan and extract package references from these files.

### Enabling

Legacy `packages.config` scanning is opt-in to avoid noise in modern codebases:

**CLI:**

```bash
NuGroom --config settings.json --include-packages-config
```

**Config file:**

```json
{
  "IncludePackagesConfig": true
}
```

### How It Works

1. The tool discovers `packages.config` files in the repository
2. Each file is associated with the co-located project file (`.csproj`, `.vbproj`, or `.fsproj` in the same directory)
3. `<package id="..." version="..."/>` entries are extracted and tagged as `PackagesConfig` source kind
4. Exclusion rules (prefix, exact, pattern) apply as usual
5. Packages already discovered via `<PackageReference>` in the same project are deduplicated

---

## Multi-Feed Resolution

Specify one or more feeds with `--feed`. The resolver queries each feed until a package is found, selecting the first match. The originating feed host is displayed next to each package.

Benefits:
- Combine public + private feeds in a single scan
- Authenticate to private feeds with PAT tokens
- Detect internal packages published to private feeds
- See which feed supplied the version information
- Stops searching after first successful resolution (improved performance)

---

## Version Warnings

The version warning system helps identify version inconsistencies across your repositories.

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
  ⚠ Repository1/Project1.csproj
    Package version 12.0.3 differs from latest available version 13.0.3 (major version difference)

Serilog:
  ⚠ Repository2/Project2.csproj
    Package version 2.10.0 differs from latest used version 2.12.0 (minor version difference)

--------------------------------------------------------------------------------
Total warnings: 2
Packages with warnings: 2
Major version differences: 1
Minor version differences: 1
```

---

## Package Update Recommendations

When version warnings are configured, the tool automatically generates actionable package update recommendations.

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
- Current version → Recommended version
- Reason for the recommendation
- Summary statistics (total recommendations, packages affected, projects impacted)

Recommendations are automatically generated whenever version warnings are configured. No additional configuration is needed. Set `"DefaultLevel": "None"` to disable.

---

## Project File Filtering

Exclude specific project files from analysis using regex patterns.

### Common Use Cases

**Exclude Test Projects:**

```json
{
  "ExcludeProjectPatterns": [
    ".*\\.Test[s]?\\.csproj$",
    ".*\\.UnitTests\\.csproj$",
    ".*\\.IntegrationTests\\.csproj$"
  ]
}
```

**Exclude Specific Project Types:**

```json
{
  "ExcludeProjectPatterns": [
    ".*\\.Benchmarks\\.csproj$",
    ".*\\.Samples\\.csproj$",
    ".*\\.Demo\\.csproj$"
  ]
}
```

**Case-Sensitive Matching:**

```json
{
  "ExcludeProjectPatterns": [".*\\.TEST\\.csproj$"],
  "CaseSensitiveProjectFilters": true
}
```

---

## Health & Risk Indicators

The tool flags packages with risk indicators:

- `[DEPRECATED]` — Package is unlisted on the feed
- `[OUTDATED]` — Your solution uses a version lower than the latest available
- `[VULNERABLE]` — Heuristic detection (keywords in description or very old publish date)

Summary sections list all Deprecated, Outdated, and Vulnerable packages with details.

---

## Internal Package Source Detection

Cross-references packages not found on public feeds with project sources:
- Uses intelligent matching algorithms (exact match, prefix match, fuzzy matching)
- Provides confidence scores for potential source projects
- Identifies internal/private packages and their likely origins
- Helps map dependencies to internal development teams

---

## Debug Logging

Enable detailed diagnostic output for troubleshooting.

### Automatic Enabling

Debug logging is automatically enabled when running with a debugger attached (e.g., from Visual Studio).

### Manual Enabling

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

---

> **See also:** [Automated Updates](automated-updates.md) · [Export Formats](export-formats.md) · [Renovate Compatibility](renovate-compatibility.md)
