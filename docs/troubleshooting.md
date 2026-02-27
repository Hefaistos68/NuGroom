# Troubleshooting & Operations

---

## Troubleshooting

### Enable Debug Logging

For detailed diagnostic information, add the `--debug` flag:

```bash
NuGroom --config settings.json --debug
```

Debug logging is also automatically enabled when a debugger is attached (e.g., running from Visual Studio).

### Private Feed Authentication Fails

- Verify feed URL exactly matches between `Feeds` and `FeedAuth`
- Ensure PAT has **Packaging (Read)** permissions
- Check PAT hasn't expired
- For Azure DevOps, try username `"VssSessionToken"` or empty string
- Enable debug logging to see authentication details

### Package Not Found on Private Feed

- Confirm package exists in the feed
- Verify authentication is working (check feed URL in output with `--debug`)
- Ensure feed URL uses v3 API format: `.../nuget/v3/index.json`

### JSON Configuration File Fails to Load

- Verify JSON syntax is valid
- Check enum values are strings (e.g., `"Minor"` not `Minor`)
- Ensure feed names in `FeedAuth` match names in `Feeds` array exactly
- Enable debug logging to see detailed error messages

### Version Warnings Not Appearing

- Verify `VersionWarnings.DefaultLevel` is not `"None"`
- Check that `ResolveNuGet` is `true` (default)
- Ensure packages are being resolved successfully (check feed access)
- Review package-specific rules for conflicting configurations

---

## Performance & Concurrency

Resolution is parallelized (default 5 concurrent requests) and results are cached per run. Feeds are queried in order until a package is found, then searching stops for improved performance.

---

## Security Best Practices

1. **Store PATs securely**: Use config files with appropriate file permissions
2. **Use least-privilege tokens**: Only grant package read permissions
3. **Rotate tokens regularly**: Update config files when tokens expire
4. **Don't commit tokens**: Add config files with credentials to `.gitignore`
5. **Use environment variables**: Consider loading tokens from environment for CI/CD
6. **Review debug output**: Debug logs may contain sensitive information; use carefully in production

---

## Notes

- Vulnerability detection is heuristic; integrate with security advisories for production use
- Feed authentication uses NuGet's built-in credential system
- Credentials are stored in memory only and never logged (even in debug mode)
- Version warnings compare semantic versions; invalid version strings are ignored
- Feed resolution stops at first successful match for improved performance
- Update workflow requires Azure DevOps PAT with **Code (Read & Write)** and **Pull Request Threads (Read & Write)** permissions
- Dry-run mode is enabled by default to prevent accidental changes
- Pinned packages are never updated, even if a newer version is within scope
- Renovate `ignoreDeps` and disabled `packageRules` are applied per-repository during scanning
- Renovate `reviewers` override `RequiredReviewers` per-repository; `OptionalReviewers` are unaffected

---

## Roadmap

- ✅ Version warning system with configurable levels
- ✅ Automated package update recommendations
- ✅ Automated package updates with branch creation and PR workflow
- ✅ Renovate compatibility (ignoreDeps, packageRules, reviewers)
- Integration with official vulnerability databases
- Graph visualization of internal dependencies
- Support for Azure Key Vault credential storage

---

> **See also:** [Getting Started](getting-started.md) · [Configuration](configuration.md)
