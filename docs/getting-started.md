# Getting Started

## Prerequisites

- .NET 10.0 or later
- Azure DevOps Personal Access Token with Code (Read) permissions
- For automatic updates: Azure DevOps PAT with **Code (Read & Write)** and **Pull Request Threads (Read & Write)** permissions
- Internet connection (for package resolution)
- For private feeds: Additional PAT tokens with package read permissions

---

## Basic Usage (NuGet.org only)

```bash
NuGroom --organization "https://dev.azure.com/yourorg" --token "your-pat-token"
```

## Using a Configuration File (Recommended)

```bash
NuGroom --config settings.json
```

See [Configuration](configuration.md) for the full config file format.

---

## Quick Examples

### Private Feed with PAT Authentication (CLI)

```bash
NuGroom -o "https://dev.azure.com/yourorg" -t "your-token" \
  --feed https://pkgs.dev.azure.com/yourorg/_packaging/MyFeed/nuget/v3/index.json \
  --feed-auth "https://pkgs.dev.azure.com/yourorg/_packaging/MyFeed/nuget/v3/index.json|VssSessionToken|your-feed-pat"
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

### Export SBOM (SPDX 3.0.0)

```bash
NuGroom --config settings.json --export-sbom sbom.spdx.json
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

### Increment Project Patch Version on Update

```bash
NuGroom --config settings.json --update-references --increment-project-version
```

### Increment All Project Versions (Major) on Update

```bash
NuGroom --config settings.json --update-references --increment-project-version-all Major
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

### Migrate to Central Package Management (Dry-Run Preview)

```bash
NuGroom --config settings.json --migrate-to-cpm --dry-run
```

### Migrate to Central Package Management (Creates Branches and PRs)

```bash
NuGroom --config settings.json --migrate-to-cpm --update-references
```

### Migrate to CPM with Per-Project Props Files

```bash
NuGroom --config settings.json --migrate-to-cpm --per-project --dry-run
```

### Exclude Test Projects

```bash
NuGroom --config settings.json --exclude-project ".*\.Test[s]?\.csproj$"
```

### Include Legacy packages.config Projects

```bash
NuGroom --config settings.json --include-packages-config
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

---

> **Next:** [CLI Reference](cli-reference.md) · [Configuration](configuration.md) · [Features](features.md)
