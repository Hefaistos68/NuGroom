# Automated Updates

NuGroom can automatically create feature branches and pull requests to update outdated package references across your Azure DevOps repositories.

---

## How It Works

1. **Scan** — repositories are scanned and NuGet metadata is resolved (as usual)
2. **Plan** — the tool compares used versions against latest available and builds an update plan within the configured scope
3. **Preview** (dry-run) — the plan is displayed showing what branches and PRs would be created
4. **Apply** — feature branches are created from the source branch, updated project (`.csproj`, `.vbproj`, `.fsproj`) files are pushed, and PRs are opened against the target branch

Projects within each repository are processed in order of dependency count (fewest dependencies first).

---

## Update Scope

The scope controls the maximum version change that will be applied. Scopes are cumulative:

| Scope | Allowed Updates | Example |
|-------|----------------|---------|
| `Patch` | Patch only | `1.2.3` → `1.2.5` |
| `Minor` | Minor and patch | `1.2.3` → `1.4.0` |
| `Major` | Any version | `1.2.3` → `2.0.0` |

---

## Branch Resolution

- **Source branch** — the branch to read current file content from and create the feature branch from. Resolved via pattern matching with semver ordering (e.g., `develop/*` picks the latest `develop/1.2.3` branch). If not specified, the repository's **default branch** is used.
- **Target branch** — the PR destination. Also resolved via pattern matching with semver ordering.
- **Feature branch** — created with a timestamp suffix, e.g., `feature/update-nuget-references-20250224-155601`.

---

## Dry-Run Mode

Dry-run is enabled by default in configuration (`"DryRun": true`). Use `--dry-run` on the CLI or set `"DryRun": false` / `--update-references` to apply changes.

In dry-run mode the tool shows:
- All planned package version updates per repository and project
- The feature branch name that would be created
- The source branch it would branch from
- The PR title and target branch
- A total count of branches and PRs that would be created

---

## Pinned Packages

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

---

## Project Version Increment

When updating package references, the tool can automatically increment version properties (`<Version>`, `<AssemblyVersion>`, `<FileVersion>`) in each project file that receives updates. This ensures the project version reflects that its dependencies have changed.

**CLI flags:**
- `--increment-project-version [scope]` — increment `<Version>` only
- `--increment-project-assemblyversion [scope]` — increment `<AssemblyVersion>` only
- `--increment-project-fileversion [scope]` — increment `<FileVersion>` only
- `--increment-project-version-all [scope]` — increment all three properties

The optional `[scope]` parameter controls which component is bumped:

| Scope | Example |
|-------|---------|
| `Patch` (default) | `1.2.3` → `1.2.4` |
| `Minor` | `1.2.3` → `1.3.0` |
| `Major` | `1.2.3` → `2.0.0` |

Both 3-part (`Major.Minor.Patch`) and 4-part (`Major.Minor.Build.Revision`) version formats are supported. Lower components are reset to zero when a higher component is incremented.

**Config file:**

```json
{
  "Update": {
    "VersionIncrement": {
      "IncrementVersion": true,
      "IncrementAssemblyVersion": true,
      "IncrementFileVersion": true,
      "Scope": "Patch"
    }
  }
}
```

Version properties that do not exist in a project file are silently skipped. The increment is only applied to `.csproj` / `.vbproj` / `.fsproj` project files — `Directory.Packages.props` and `packages.config` files are not affected.

---

## Source Packages Only

Use `--source-packages-only` or `"SourcePackagesOnly": true` to restrict updates to packages that have identified source projects in the scanned repositories.

---

## PR Reviewers

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

---

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
2. All repositories are scanned for project files (`.csproj`, `.vbproj`, `.fsproj`) referencing the specified package
3. Projects already at the target version are skipped
4. For each affected repository, a feature branch is created, changes are pushed, and a PR is opened

### Behavior

- **Upgrades and downgrades** — unlike `--update-references`, `--sync` has no scope restriction. It always sets the exact target version.
- **Dry-run** — uses the `DryRun` setting from `UpdateConfig`. Pass `--dry-run` or set `"DryRun": true` to preview changes.
- **Branch patterns** — uses `SourceBranchPattern` and `TargetBranchPattern` from `UpdateConfig`.
- **Reviewers** — uses `RequiredReviewers` / `OptionalReviewers` from `UpdateConfig`, with Renovate `reviewers` override per repository.
- **Renovate** — respects `ignoreDeps` and disabled `packageRules`. If the package is excluded by Renovate in a repository, that repository is skipped.

---

> **See also:** [CLI Reference](cli-reference.md) · [Configuration](configuration.md) · [Renovate Compatibility](renovate-compatibility.md)
