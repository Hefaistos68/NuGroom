# NuGroom - Find, list and update packages

A command-line tool that connects to Azure DevOps, searches all repositories for C#, Visual Basic and F# project files, extracts PackageReference lines, and provides comprehensive package analysis including **multi-feed NuGet package information resolution with PAT authentication**.

---

## Features

- **Repository Scanning** — connects to Azure DevOps and discovers all project files across repositories
- **NuGet Resolution** — resolves package metadata from multiple feeds with PAT authentication
- **Central Package Management** — automatic CPM detection, updates, and migration (`--migrate-to-cpm`)
- **Automated Updates** — creates feature branches and pull requests for outdated packages
- **Package Sync** — force a specific package to an exact version across all repositories
- **Version Warnings** — configurable warnings for version differences with actionable recommendations
- **Health Indicators** — flags deprecated, outdated, and potentially vulnerable packages
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
```

### Prerequisites

- .NET 10.0 or later
- Azure DevOps Personal Access Token with Code (Read) permissions
- For automatic updates: PAT with **Code (Read & Write)** and **Pull Request Threads (Read & Write)** permissions

---

## Documentation

| Document | Description |
|----------|-------------|
| [Getting Started](docs/getting-started.md) | Prerequisites, installation, and usage examples |
| [CLI Reference](docs/cli-reference.md) | Complete list of all command line options |
| [Configuration](docs/configuration.md) | Config file format, fields, feed authentication, and environment variables |
| [Features](docs/features.md) | CPM, version warnings, filtering, health indicators, and more |
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
- Integration with official vulnerability databases
- Graph visualization of internal dependencies
- Support for Azure Key Vault credential storage
