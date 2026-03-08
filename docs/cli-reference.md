# CLI Reference

Complete list of all command line options for NuGroom.

---

## Modes of Operation

NuGroom supports two modes:

- **Azure DevOps mode** (default) — connects to an Azure DevOps organization to discover and scan repositories. Requires `--organization` and `--token`.
- **Local mode** — scans files and folders on disk. No Azure DevOps credentials or connectivity needed. Use `--paths` to specify one or more paths.

---

## Local Mode

| Option | Description | Example |
|--------|-------------|---------|
| `--paths <path>` | Scan a local file or directory (repeatable, no Azure DevOps required) | `--paths ./src --paths MyApp.csproj` |

When `--paths` is specified, `--organization` and `--token` are **not** required.
All other options (feeds, exclusions, exports, NuGet resolution) work the same as in Azure DevOps mode.

`--update-references`, `--dry-run`, `--sync`, and `--migrate-to-cpm` are supported in local mode.
Instead of creating branches and PRs, changes are written directly to files on disk. In dry-run mode,
planned changes are displayed without modifying files.

---

## Required Options (Azure DevOps mode)

| Option | Short | Description |
|--------|-------|-------------|
| `--organization` | `-o` | Azure DevOps organization URL |
| `--token` | `-t` | Personal Access Token for authentication |

---

## Configuration

| Option | Short | Description |
|--------|-------|-------------|
| `--config` | | Load settings from JSON config file |
| `--project` | `-p` | Specific project name to search (searches all if not specified) |
| `--max-repos` | `-m` | Maximum repositories to process (default: 100) |
| `--include-archived` | `-a` | Include archived repositories (default: false) |
| `--include-packages-config` | | Also scan legacy `packages.config` files (default: false) |

---

## Resolution Options

| Option | Description | Example |
|--------|-------------|---------|
| `--resolve-nuget` | Resolve package info from feeds (default: true) | `--resolve-nuget` |
| `--skip-nuget` | Skip resolution for faster processing | `--skip-nuget` |
| `--feed <url>` | Add a NuGet feed (repeatable) | `--feed https://pkgs.dev.azure.com/.../index.json` |
| `--feed-auth <auth>` | Feed authentication: `feedUrl\|username\|pat` (repeatable) | See [Configuration](configuration.md) |

---

## Package Exclusion Options

| Option | Description | Example |
|--------|-------------|---------|
| `--exclude-prefix` | Exclude packages starting with prefix | `--exclude-prefix "Microsoft."` |
| `--exclude-package` | Exclude specific package by exact name | `--exclude-package "Newtonsoft.Json"` |
| `--exclude-pattern` | Exclude packages matching regex | `--exclude-pattern ".*\.Test.*"` |
| `--no-default-exclusions` | Include Microsoft.* and System.* | |
| `--case-sensitive` | Case-sensitive matching | |

---

## Project File Filtering Options

| Option | Description | Example |
|--------|-------------|---------|
| `--exclude-project <pattern>` | Exclude project files matching regex pattern (repeatable) | `--exclude-project ".*\.Test[s]?\.csproj$"` |
| `--include-project <pattern>` | Include only project files matching regex pattern (repeatable). When specified, only matching projects are scanned. | `--include-project ".*\.Web\.csproj$"` |
| `--case-sensitive-project` | Use case-sensitive project file matching | |

When both `--include-project` and `--exclude-project` are specified, exclusion takes precedence: a project must match at least one include pattern **and** must not match any exclude pattern.

---

## Repository Exclusion Options

| Option | Description | Example |
|--------|-------------|---------|
| `--exclude-repo <pattern>` | Exclude repositories matching regex pattern (repeatable, case-insensitive) | `--exclude-repo "Legacy-.*"` |
| `--include-repo <pattern>` | Include only repositories matching pattern (repeatable). Replaces config file repositories when specified. | `--include-repo "MyRepo-.*"` |

---

## Export Options

| Option | Description | Example |
|--------|-------------|---------|
| `--export-packages <path>` | Export package results to file (format via `--export-format`) | `--export-packages report.json` |
| `--export-warnings <path>` | Export version warnings to a separate file | `--export-warnings warnings.json` |
| `--export-recommendations <path>` | Export update recommendations to a separate file | `--export-recommendations recs.json` |
| `--export-format <format>` | Format for all exports: `Json` or `Csv` (default: `Json`) | `--export-format csv` |
| `--export-sbom <path>` | Export SPDX 3.0.0 SBOM as JSON-LD (independent of `--export-format`) | `--export-sbom sbom.spdx.json` |

---

## Display Options

| Option | Short | Description |
|--------|-------|-------------|
| `--detailed` | `-d` | Show detailed package info |
| `--debug` | | Enable debug logging (automatically enabled when debugger attached) |

---

## Update Options

| Option | Description | Default |
|--------|-------------|---------|
| `--update-references` | Enable auto-update mode (creates branches and PRs in repo mode, writes files directly in local mode) | |
| `--dry-run` | Show planned updates without making any changes | |
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

---

## Version Increment Options

| Option | Description | Default |
|--------|-------------|---------|
| `--increment-project-version [scope]` | Increment `<Version>` in updated project files | scope: `Patch` |
| `--increment-project-assemblyversion [scope]` | Increment `<AssemblyVersion>` in updated project files | scope: `Patch` |
| `--increment-project-fileversion [scope]` | Increment `<FileVersion>` in updated project files | scope: `Patch` |
| `--increment-project-version-all [scope]` | Increment all three version properties | scope: `Patch` |

The optional `[scope]` parameter accepts `Patch`, `Minor`, or `Major` (default: `Patch`).
These options only take effect when combined with `--update-references` or `--dry-run`.
Version properties that do not exist in a project file are left unchanged.

---

## Sync Options

| Option | Description | Default |
|--------|-------------|---------|
| `--sync <package> [version]` | Sync a specific package to a version across all repositories (creates PRs) or local files (writes directly) | |
| | If version is omitted, the latest available version from feeds is used | |

---

## CPM Migration Options

| Option | Description | Default |
|--------|-------------|---------|
| `--migrate-to-cpm` | Migrate projects to use Central Package Management (`Directory.Packages.props`) | |
| `--per-project` | Create a `Directory.Packages.props` per project instead of per repository (only valid with `--migrate-to-cpm`) | |

---

## Vulnerability Scanning Options

| Option | Description | Default |
|--------|-------------|---------|
| `--skip-vuln` | Disable OSV.dev vulnerability database queries (NuGet feed advisories still shown) | OSV enabled |
| `--no-vuln-cache` | Disable local vulnerability result caching | Cache enabled |
| `--vuln-cache-path <path>` | Path for the vulnerability cache file | `.nugroom/vuln-cache.json` |
| `--export-vulnerabilities <path>` | Export vulnerability report to a separate file (format via `--export-format`) | |

Vulnerability scanning is **enabled by default** when NuGet resolution is active. Use `--skip-vuln` to disable external database queries. NuGet feed-embedded advisories are always shown regardless of this flag.

See [Vulnerability Scanning](vulnerability.md) for configuration file options, caching details, and data sources.

---

## Help

| Option | Short | Description |
|--------|-------|-------------|
| `--help` | `-h` | Show help |

---

## CLI Precedence

CLI arguments always override config file values. When both are provided, the CLI value wins. This applies to all settings including update options, boolean flags, and branch patterns.

---

> **See also:** [Getting Started](getting-started.md) · [Configuration](configuration.md)
