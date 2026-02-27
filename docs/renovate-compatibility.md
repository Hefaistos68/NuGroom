# Renovate Compatibility

NuGroom automatically detects and respects [Renovate](https://docs.renovatebot.com/) configuration files in scanned repositories. This allows teams already using Renovate to maintain a single source of truth for dependency management rules.

---

## Detection

The following file paths are checked in priority order:

1. `/renovate.json`
2. `/.renovaterc`
3. `/.renovaterc.json`
4. `/.github/renovate.json`

The first file found is used. If no Renovate config exists, the repository is processed normally.

To disable Renovate detection entirely, use `--ignore-renovate` on the CLI or `"IgnoreRenovate": true` in the config file.

---

## Supported Fields

| Renovate Field | Effect |
|----------------|--------|
| `ignoreDeps` | Listed packages are excluded from scanning and update plans for this repository |
| `packageRules[].matchPackageNames` + `enabled: false` | Matched packages are excluded from scanning and update plans |
| `reviewers` | Overrides the tool's `RequiredReviewers` for PRs created against this repository |
| `packageRules[].matchPackageNames` + `reviewers` | Stored per-package for future use |

---

## Example `renovate.json`

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

---

## Precedence

- **ignoreDeps / packageRules** — applied during scanning, before NuGet resolution. These packages won't appear in reports or update plans.
- **reviewers** — applied during PR creation. Renovate reviewers **replace** (not merge with) the tool's `RequiredReviewers` for that repository. `OptionalReviewers` from the tool config are still added.
- Repositories without a Renovate config use the tool's configured reviewers as usual.

---

## Console Output

When a Renovate config is detected, the scan log shows:

```
[1/50] Scanning repository: MyRepository
  Renovate config found (2 ignored, 2 disabled)
```

---

> **See also:** [Automated Updates](automated-updates.md) · [Configuration](configuration.md)
