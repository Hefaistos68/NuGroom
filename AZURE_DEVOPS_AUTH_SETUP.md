# Azure DevOps NuGet Feed Authentication Setup

## Problem
When using `USE_CURRENT_USER` authentication for Azure DevOps NuGet feeds, you may encounter:
```
401 Unauthorized - Unable to load the service index for source https://[org].pkgs.visualstudio.com/...
```

## Root Cause
The NuGet Protocol SDK requires the **Azure Artifacts Credential Provider** to handle interactive authentication with Azure DevOps. Setting empty credentials prevents this plugin system from working.

## Solution: Install Azure Artifacts Credential Provider

### Option 1: Using PowerShell (Windows - Recommended)
```powershell
# Run in PowerShell as Administrator
iex "& { $(irm https://aka.ms/install-artifacts-credprovider.ps1) } -AddNetfx"
```

### Option 2: Manual Installation (Windows)
1. Download the latest credential provider from:
   https://github.com/microsoft/artifacts-credprovider/releases

2. Extract to one of these locations:
   - `%USERPROFILE%\.nuget\plugins\netfx\CredentialProvider.Microsoft\`
   - `%USERPROFILE%\.nuget\plugins\netcore\CredentialProvider.Microsoft\`

### Option 3: Using .NET CLI (Cross-platform)
```bash
# Linux/macOS
wget -qO- https://aka.ms/install-artifacts-credprovider.sh | bash

# Or with curl
curl -sSL https://aka.ms/install-artifacts-credprovider.sh | bash
```

### Option 4: Use Personal Access Token (PAT)
If you cannot install the credential provider, use a PAT instead:

1. Generate a PAT in Azure DevOps:
   - Go to User Settings ? Personal Access Tokens
   - Create token with **Packaging (Read)** scope

2. Update your configuration to use the PAT:
   ```csharp
   var feedAuth = new List<FeedAuth>
   {
       new FeedAuth("CIR-Feed", "VssSessionToken", "YOUR_PAT_HERE")
   };
   ```

## Verification
After installation, verify the credential provider is installed:

### Windows PowerShell
```powershell
Test-Path "$env:USERPROFILE\.nuget\plugins\netcore\CredentialProvider.Microsoft"
```

### Command Line
```cmd
dir "%USERPROFILE%\.nuget\plugins\netcore\CredentialProvider.Microsoft"
```

### Linux/macOS
```bash
ls ~/.nuget/plugins/netcore/CredentialProvider.Microsoft
```

## How It Works
1. When `USE_CURRENT_USER` is specified, the code **does not** set credentials
2. NuGet SDK detects missing credentials for Azure DevOps feed
3. NuGet SDK invokes the credential provider plugin
4. Plugin handles interactive authentication (browser-based or cached tokens)
5. Credentials are cached for future requests

## Troubleshooting

### Still getting 401 errors?
1. **Clear NuGet cache:**
   ```bash
   dotnet nuget locals all --clear
   ```

2. **Check Visual Studio authentication:**
   - Open Visual Studio
   - Go to Tools ? Options ? Azure Service Authentication
   - Ensure you're signed in with correct account

3. **Use environment variable for non-interactive scenarios:**
   ```bash
   # Set PAT as environment variable
   set VSS_NUGET_EXTERNAL_FEED_ENDPOINTS={"endpointCredentials":[{"endpoint":"https://pkgs.dev.azure.com/[org]/_packaging/[feed]/nuget/v3/index.json","username":"VssSessionToken","password":"YOUR_PAT"}]}
   ```

4. **Run application with inherited credentials:**
   - Ensure your application runs under a user context that has access to Azure DevOps
   - Check Azure DevOps feed permissions for your user account

### Debugging Authentication Issues
Enable detailed NuGet logging:
```bash
set NUGET_SHOW_STACK=true
set NUGET_HTTP_CACHE_PATH=%TEMP%\NuGetScratch
```

## References
- [Azure Artifacts Credential Provider](https://github.com/microsoft/artifacts-credprovider)
- [NuGet Authentication for Azure Artifacts](https://learn.microsoft.com/en-us/azure/devops/artifacts/nuget/nuget-exe)
