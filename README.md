# NuGroom - Find, list and update packages

A command-line tool that connects to Azure DevOps, searches all repositories for C#, Visual Basic and F# project files, extracts PackageReference lines, and provides comprehensive package analysis including **multi-feed NuGet package information resolution with PAT authentication**.

---

## Features

- **Repository Scanning** — connects to Azure DevOps and discovers all project files across repositories
- **Local File Scanning** — scans local files and folders with `--paths`, no Azure DevOps required
- **NuGet Resolution** — resolves package metadata from multiple feeds with PAT authentication
- **Vulnerability Scanning** — checks packages against NuGet feed advisories and the OSV.dev database with local caching
- **Central Package Management** — automatic CPM detection, updates, and migration (`--migrate-to-cpm`)
- **Automated Updates** — creates feature branches and pull requests for outdated packages, or updates local files directly
- **Package Sync** — force a specific package to an exact version across repositories or local files
- **Version Warnings** — configurable warnings for version differences with actionable recommendations
- **Health Indicators** — flags deprecated, outdated, and vulnerable packages
- **Internal Package Detection** — identifies internal packages and their likely source projects
- **Export** — JSON, CSV, and SPDX 3.0.0 SBOM export
- **Renovate Compatibility** — respects `ignoreDeps`, disabled `packageRules`, and `reviewers`
- **Flexible Filtering** — exclude packages, projects, and repositories by prefix, name, or regex
- **Configuration File** — store all settings in JSON with environment variable support for secrets

---

## Quick Start

```bash
# Install as a global dotnet tool
dotnet tool install --global NuGroom

# Basic scan
nugroom --organization "https://dev.azure.com/yourorg" --token "your-pat-token"

# Using a config file (recommended)
nugroom --config settings.json

# Dry-run package updates
nugroom --config settings.json --dry-run --update-scope Minor

# Apply updates (creates branches and PRs)
nugroom --config settings.json --update-references --update-scope Patch

# Scan local files (no Azure DevOps needed)
nugroom --paths ./src --paths MyApp.csproj

# Dry-run local updates
nugroom --paths ./src --dry-run --update-scope Minor

# Apply local updates directly to files on disk
nugroom --paths ./src --update-references --update-scope Patch

# Sync one package version across local files
nugroom --paths ./src --sync Newtonsoft.Json 13.0.3

# Migrate local projects to Central Package Management
nugroom --paths ./src --migrate-to-cpm

# Scan only web projects, excluding test projects
nugroom --paths ./src --include-project ".*\.Web\.csproj$" --exclude-project ".*\.Tests\.csproj$"
```

### Prerequisites

- .NET 10.0 or later
- For Azure DevOps mode: Personal Access Token with Code (Read) permissions
- For automatic updates via PRs: PAT with **Code (Read & Write)** and **Pull Request Threads (Read & Write)** permissions
- For local mode (`--paths`): no Azure DevOps credentials required

---

## Documentation

| Document | Description |
|----------|-------------|
| [Getting Started](docs/getting-started.md) | Prerequisites, installation, and usage examples |
| [CLI Reference](docs/cli-reference.md) | Complete list of all command line options |
| [Configuration](docs/configuration.md) | Config file format, fields, feed authentication, and environment variables |
| [Features](docs/features.md) | CPM, version warnings, filtering, health indicators, and more |
| [Vulnerability Scanning](docs/vulnerability.md) | NuGet advisories, OSV.dev integration, caching, and configuration |
| [Automated Updates](docs/automated-updates.md) | Package updates, sync, version increment, and PR workflow |
| [Renovate Compatibility](docs/renovate-compatibility.md) | Integration with Renovate configuration |
| [Export Formats](docs/export-formats.md) | JSON, CSV, and SPDX 3.0.0 SBOM export |
| [Output Examples](docs/samples.md) | Sample console output for common operations |
| [Azure DevOps Pipelines](docs/azure-devops-pipeline.md) | Installation, pipeline examples, and `System.AccessToken` setup |
| [Troubleshooting](docs/troubleshooting.md) | Debugging, security, performance, and known issues |

---

## Roadmap

- ✅ Version warning system with configurable levels
- ✅ Automated package update recommendations
- ✅ Automated package updates with branch creation and PR workflow
- ✅ Renovate compatibility (ignoreDeps, packageRules, reviewers)
- ✅ Central Package Management (CPM) detection, updates, and migration
- ✅ Integration with official vulnerability databases (NuGet advisories + OSV.dev)
- Graph visualization of internal dependencies
- Support for Azure Key Vault credential storage
