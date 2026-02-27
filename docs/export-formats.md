# Export Formats

NuGroom supports multiple export formats for package analysis results, version warnings, and recommendations.

---

## JSON Export

Human-readable JSON with:
- Generation timestamp
- Project and package counts
- Detailed package information per project
- Feed source, versions, and status flags
- Version warnings section (when configured)
- Package update recommendations (when version warnings configured)

```bash
NuGroom --config settings.json --export-packages report.json
```

---

## CSV Export

Spreadsheet-compatible format with columns:

- Repository, ProjectPath
- PackageName, Version, LatestVersion
- Feed, Deprecated, Outdated, Vulnerable flags
- Status, SourceRepository, SourceProject

```bash
NuGroom --config settings.json --export-packages packages.csv --export-format csv
```

When version warnings are configured and the export format is CSV, additional CSV files are automatically generated:

- `packages-warnings.csv` — Contains all version warnings with details
- `packages-recommendations.csv` — Contains package update recommendations with current and target versions

### Recommendations CSV Columns

| Column | Description |
|--------|-------------|
| PackageName | Package identifier |
| Repository | Repository name |
| ProjectPath | Path to the project file |
| CurrentVersion | Currently used version |
| RecommendedVersion | Suggested target version |
| RecommendationType | `latest-available` or `latest-used` |
| Reason | Human-readable explanation |

---

## SBOM Export (SPDX 3.0.0)

Generate a Software Bill of Materials in [SPDX 3.0.0](https://spdx.github.io/spdx-spec/v3.0/) JSON-LD format using `--export-sbom` or the `ExportSbom` config field. The SBOM is always written as JSON-LD regardless of `--export-format`.

The document contains:
- An `SpdxDocument` root element with creation metadata
- A `software_Package` element for every scanned package reference
- Package URL (purl) external identifiers (e.g., `pkg:nuget/Serilog@3.1.1`)
- Security metadata (deprecated / vulnerable flags) when NuGet resolution is enabled

**CLI:**

```bash
NuGroom --config settings.json --export-sbom sbom.spdx.json
```

**Config file:**

```json
{
  "ExportSbom": "sbom.spdx.json"
}
```

---

## Separate Exports

Warnings and recommendations can be exported to dedicated files, independent of the main package report:

```bash
NuGroom --config settings.json \
  --export-warnings warnings.json \
  --export-recommendations recommendations.json
```

The format for all exports is controlled by `--export-format` (default: `Json`). The SBOM export always uses JSON-LD.

---

> **See also:** [Features](features.md) · [Configuration](configuration.md)
