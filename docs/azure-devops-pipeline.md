# Azure DevOps Pipeline Integration

NuGroom is packaged as a [.NET tool](https://learn.microsoft.com/dotnet/core/tools/global-tools) and can be installed and run directly in Azure DevOps pipelines.

---

## Prerequisites

- A pipeline agent with **.NET 10 SDK** installed (or use the `UseDotNet` task)
- An Azure DevOps **Personal Access Token** with the required permissions:
  - **Code (Read)** — for scanning repositories
  - **Code (Read & Write)** and **Pull Request Threads (Read & Write)** — for automated updates and PR creation

---

## Installation

### Install as a Global Tool

```bash
dotnet tool install --global NuGroom
```

### Install as a Local Tool (per-repo)

```bash
dotnet new tool-manifest   # only once, creates .config/dotnet-tools.json
dotnet tool install NuGroom
```

### Install from a Private Feed

If you publish the NuGet package to an Azure Artifacts feed:

```bash
dotnet tool install --global NuGroom \
  --add-source https://pkgs.dev.azure.com/yourorg/_packaging/YourFeed/nuget/v3/index.json
```

---

## Pipeline Examples

### Basic Scan

```yaml
trigger:
  - main

pool:
  vmImage: 'ubuntu-latest'

steps:
  - task: UseDotNet@2
    inputs:
      packageType: 'sdk'
      version: '10.x'

  - script: dotnet tool install --global NuGroom
    displayName: 'Install NuGroom'

  - script: nugroom --config settings.json
    displayName: 'Scan repositories'
    env:
      ADO_PAT: $(System.AccessToken)
```

### Scan with Export

```yaml
steps:
  - task: UseDotNet@2
    inputs:
      packageType: 'sdk'
      version: '10.x'

  - script: dotnet tool install --global NuGroom
    displayName: 'Install NuGroom'

  - script: |
      nugroom --config settings.json \
        --export-packages $(Build.ArtifactStagingDirectory)/packages.json \
        --export-warnings $(Build.ArtifactStagingDirectory)/warnings.json \
        --export-sbom $(Build.ArtifactStagingDirectory)/sbom.spdx.json
    displayName: 'Scan and export'
    env:
      ADO_PAT: $(System.AccessToken)

  - task: PublishBuildArtifacts@1
    inputs:
      pathToPublish: '$(Build.ArtifactStagingDirectory)'
      artifactName: 'nugroom-reports'
```

### Automated Package Updates (Scheduled)

```yaml
trigger: none

schedules:
  - cron: '0 6 * * Mon'
    displayName: 'Weekly package update check'
    branches:
      include:
        - main

pool:
  vmImage: 'ubuntu-latest'

steps:
  - task: UseDotNet@2
    inputs:
      packageType: 'sdk'
      version: '10.x'

  - script: dotnet tool install --global NuGroom
    displayName: 'Install NuGroom'

  - script: |
      nugroom --config settings.json \
        --update-references \
        --update-scope Patch
    displayName: 'Update packages (Patch scope)'
    env:
      ADO_PAT: $(System.AccessToken)
```

### Package Sync

```yaml
steps:
  - task: UseDotNet@2
    inputs:
      packageType: 'sdk'
      version: '10.x'

  - script: dotnet tool install --global NuGroom
    displayName: 'Install NuGroom'

  - script: |
      nugroom --config settings.json \
        --sync Newtonsoft.Json 13.0.3
    displayName: 'Sync Newtonsoft.Json to 13.0.3'
    env:
      ADO_PAT: $(System.AccessToken)
```

### CPM Migration (Dry-Run)

```yaml
steps:
  - task: UseDotNet@2
    inputs:
      packageType: 'sdk'
      version: '10.x'

  - script: dotnet tool install --global NuGroom
    displayName: 'Install NuGroom'

  - script: |
      nugroom --config settings.json \
        --migrate-to-cpm --dry-run
    displayName: 'Preview CPM migration'
    env:
      ADO_PAT: $(System.AccessToken)
```

---

## Configuration with Environment Variables

Use environment variable references in your config file to avoid storing secrets in source control. Map pipeline variables in the `env` block:

**settings.json:**

```json
{
  "Organization": "https://dev.azure.com/yourorg",
  "Token": "$env:ADO_PAT",
  "Feeds": [
    {
      "Name": "InternalFeed",
      "Url": "https://pkgs.dev.azure.com/yourorg/_packaging/Feed/nuget/v3/index.json"
    }
  ],
  "FeedAuth": [
    {
      "FeedName": "InternalFeed",
      "Username": "",
      "Pat": "${FEED_PAT}"
    }
  ]
}
```

**Pipeline step:**

```yaml
- script: nugroom --config settings.json
  displayName: 'Run NuGroom'
  env:
    ADO_PAT: $(System.AccessToken)
    FEED_PAT: $(System.AccessToken)
```

The `$(System.AccessToken)` is automatically available in Azure DevOps pipelines and requires no manual PAT creation when the build service has the necessary permissions.

---

## Permissions for `$(System.AccessToken)`

The default pipeline token has limited permissions. To use automated updates or PR creation, grant additional permissions to the **Build Service** identity:

1. Go to **Project Settings** → **Repositories** → **Security**
2. Find your project's build service identity (e.g., `ProjectName Build Service (OrgName)`)
3. Grant:
   - **Contribute** — Allow
   - **Create branch** — Allow
   - **Contribute to pull requests** — Allow

---

## Building the Package

To build the `.nupkg` yourself:

```bash
dotnet pack NuGroom/NuGroom.csproj -c Release -o nupkg
```

This creates `nupkg/NuGroom.0.1.0.nupkg`. Push it to your Azure Artifacts feed:

```bash
dotnet nuget push nupkg/NuGroom.0.1.0.nupkg \
  --source https://pkgs.dev.azure.com/yourorg/_packaging/YourFeed/nuget/v3/index.json \
  --api-key az
```

---

> **See also:** [Getting Started](getting-started.md) · [CLI Reference](cli-reference.md) · [Configuration](configuration.md)
