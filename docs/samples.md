# Output Examples

Example console output from various NuGroom operations.

---

## Scan & Report Output

```
NuGet feeds:
  - NuGet.org: https://api.nuget.org/v3/index.json
  - InternalFeed: https://pkgs.dev.azure.com/yourorg/_packaging/YourFeed/nuget/v3/index.json

Version warning level: Minor
Package-specific rules: 2 configured

Repository: MyRepository
--------------------------------------------------
  Project: /src/MyProject/MyProject.csproj
    - Serilog (Version: 2.12.0) [Feed: NuGet.org] ✓ [https://www.nuget.org/packages/Serilog]
    - AutoMapper (Version: 11.0.1) [Feed: NuGet.org] ✓ [OUTDATED]
    - MyCompany.Core (Version: 1.2.0) [Feed: InternalFeed] ✓
    - InternalLib (Version: 1.0.0) [Not found on feeds]
      🔍 Source: InternalLib in CoreLibraries (95% match)

PACKAGE SUMMARY
----------------
Serilog: 3 reference(s) across 1 version ✓ [Feed: NuGet.org]
AutoMapper: 5 reference(s) across 2 versions ✓ [OUTDATED] [Feed: NuGet.org]
  ├─ v11.0.1: 3 reference(s)
  ├─ v10.0.0: 2 reference(s)
  └─ Multiple versions detected for AutoMapper
MyCompany.Core: 4 reference(s) across 1 version ✓ [Feed: InternalFeed]
InternalLib: 2 reference(s) across 1 version [INTERNAL]

Total unique packages: 4
Available on NuGet.org: 2
Available on other feeds: 1
Internal packages (with source): 1
Outdated packages: 1
```

---

## Version Warnings Output

```
VERSION WARNINGS
================================================================================

AutoMapper:
  ⚠ MyRepository/Project1.csproj
    Package version 10.0.0 differs from latest used version 11.0.1 (minor version difference)
  ⚠ MyRepository/Project2.csproj
    Package version 10.0.0 differs from latest available version 12.0.0 (major version difference)

--------------------------------------------------------------------------------
Total warnings: 2
Packages with warnings: 1
Major version differences: 1
Minor version differences: 1
```

---

## Recommendations Output

```
PACKAGE UPDATE RECOMMENDATIONS
================================================================================
The following projects should update their package versions:

Newtonsoft.Json:
  Recommended version: 13.0.3

  • MyRepository/MyProject.csproj
    Current: 12.0.3 → Upgrade to: 13.0.3
    Upgrade to latest available version (currently major version behind)

  • MyRepository/AnotherProject.csproj
    Current: 11.0.1 → Upgrade to: 13.0.3
    Upgrade to latest available version (currently major version behind)

Serilog:
  Recommended version: 2.12.0

  • DifferentRepository/Logger.csproj
    Current: 2.10.0 → Upgrade to: 2.12.0
    Align with latest version used in solution (currently minor version behind)

--------------------------------------------------------------------------------
Total update recommendations: 3
Packages needing update: 2
Projects affected: 3
--------------------------------------------------------------------------------
```

---

## Automated Update Output (Dry-Run)

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
    Azure.Storage.Blobs: 12.26.0 → 12.27.0
  /src/Api/Api.csproj (12 total dependencies)
    Azure.Storage.Blobs: 12.26.0 → 12.27.0
    CloudConvert.API: 1.4.0 → 1.4.1
  [Would create] Branch: feature/update-nuget-references-{timestamp} from develop/*
  [Would create] PR: "chore: update 3 NuGet package reference(s) (Patch scope)" → develop/*

Total: 3 update(s) across 1 repository(ies)
Would create 1 feature branch(es) and 1 pull request(s).
Run with --update-references (without --dry-run) to apply changes.
```

---

## Package Sync Output (Dry-Run)

```
================================================================================
SYNC: Newtonsoft.Json → 13.0.1
================================================================================
Scanning 50 repository(ies) for Newtonsoft.Json...

Repository: MyRepository
--------------------------------------------------
  /src/Core/Core.csproj: 12.0.3 → 13.0.1
  /src/Api/Api.csproj: 11.0.1 → 13.0.1
  [Would create] Branch + PR to sync Newtonsoft.Json to 13.0.1

--------------------------------------------------------------------------------
DRY RUN: Would sync 1 repository(ies). 49 already at 13.0.1.
Run without --dry-run (or set "DryRun": false) to apply changes.
```

---

> **See also:** [Features](features.md) · [Automated Updates](automated-updates.md)
