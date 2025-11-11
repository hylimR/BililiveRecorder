# Build Guide for BililiveRecorder

This document describes how to build BililiveRecorder using the automated build script.

## Quick Start

### Build Everything (Default)
```powershell
.\build-and-push.ps1
```

This will:
- Run tests
- Build WebUI
- Build CLI (framework-dependent)
- Build WPF (Windows only)
- Build Docker image (if Docker is installed)

### Build Specific Target

```powershell
# Build CLI only
.\build-and-push.ps1 -Target CLI

# Build WPF only
.\build-and-push.ps1 -Target WPF

# Build Docker only
.\build-and-push.ps1 -Target Docker
```

## Common Usage Examples

### CLI Builds

```powershell
# Build CLI for Windows x64
.\build-and-push.ps1 -Target CLI -Runtime win-x64

# Build CLI for Linux x64
.\build-and-push.ps1 -Target CLI -Runtime linux-x64

# Build CLI for macOS ARM64 (Apple Silicon)
.\build-and-push.ps1 -Target CLI -Runtime osx-arm64

# Build CLI for all supported platforms
.\build-and-push.ps1 -Target CLI -Runtime all -BuildWebUI

# Framework-dependent build (requires .NET runtime installed on target)
.\build-and-push.ps1 -Target CLI -Runtime any
```

**Supported Runtimes:**
- `win-x64` - Windows x64
- `linux-x64` - Linux x64 (glibc)
- `linux-musl-x64` - Linux x64 (musl, for Alpine)
- `linux-arm64` - Linux ARM64
- `linux-musl-arm64` - Linux ARM64 (musl)
- `linux-arm` - Linux ARM32
- `osx-x64` - macOS x64 (Intel)
- `osx-arm64` - macOS ARM64 (Apple Silicon)
- `any` - Framework-dependent (no runtime included)
- `all` - Build for all platforms

### WPF Builds

```powershell
# Build WPF (Release)
.\build-and-push.ps1 -Target WPF

# Build WPF (Debug)
.\build-and-push.ps1 -Target WPF -Configuration Debug
```

**Requirements:**
- Windows OS
- Visual Studio or MSBuild Tools

### Docker Builds

```powershell
# Build Docker image with 'dev' tag
.\build-and-push.ps1 -Target Docker

# Build and push to registry
.\build-and-push.ps1 -Target Docker -PushDocker -DockerTag latest

# Build and push to custom registry
.\build-and-push.ps1 -Target Docker -PushDocker -DockerRegistry myregistry.io -DockerTag v1.0.0
```

**Requirements:**
- Docker installed and running

## Advanced Options

### Skip Tests

Skip running tests to speed up development builds:

```powershell
.\build-and-push.ps1 -SkipTests
```

### Custom Output Directory

Specify a custom output directory:

```powershell
.\build-and-push.ps1 -Target CLI -Runtime win-x64 -OutputDir "C:\Output"
```

### Debug Builds

Build in Debug configuration:

```powershell
.\build-and-push.ps1 -Configuration Debug
```

### Build WebUI Only

The WebUI can be built separately using:

```powershell
# Windows
.\webui\build.ps1

# Linux/macOS
./webui/build.sh
```

## Build Output Locations

### CLI Builds
- **Framework-dependent:** `BililiveRecorder.Cli/bin/{Configuration}/net8.0/publish/`
- **Self-contained:** `BililiveRecorder.Cli/publish/{runtime}/{Configuration}/`
- **Custom output:** `{OutputDir}/CLI-{runtime}-{Configuration}/`

### WPF Builds
- **Output:** `BililiveRecorder.WPF/bin/{Configuration}/`

### Docker Images
- **Image tag:** `{registry}/{username}/bililiverecorder:{tag}`
- Default: `ghcr.io/{username}/bililiverecorder:dev`

## Prerequisites

### All Platforms
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download) or later
- Git (for version generation)

### WPF (Windows only)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or
- [MSBuild Tools](https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2022)

### Docker
- [Docker Desktop](https://www.docker.com/products/docker-desktop) or
- Docker Engine (Linux)

### WebUI
- Node.js 18+ and npm (for WebUI builds)

## Troubleshooting

### "MSBuild not found" when building WPF
Install Visual Studio 2022 or MSBuild Tools for Visual Studio 2022.

### "Git is not installed"
Install Git from https://git-scm.com/ and ensure it's in your PATH.

### ".NET SDK is not installed"
Install .NET 8.0 SDK or later from https://dotnet.microsoft.com/download

### WebUI build fails
```powershell
cd webui/source
npm install
npm run build
```

### Docker build fails
Ensure Docker is running:
```powershell
docker --version
docker ps
```

### Version generation fails
Ensure you have full git history:
```bash
git fetch --unshallow  # If using shallow clone
git fetch --tags       # Fetch all tags
```

## Manual Build Steps

If you prefer to build manually without the script:

### CLI
```powershell
# Build WebUI first
./webui/build.ps1  # or ./webui/build.sh on Linux

# Build CLI
dotnet publish -c Release -r win-x64 BililiveRecorder.Cli/BililiveRecorder.Cli.csproj
```

### WPF
```powershell
msbuild -t:restore BililiveRecorder.WPF/BililiveRecorder.WPF.csproj
msbuild /p:Configuration=Release BililiveRecorder.WPF/BililiveRecorder.WPF.csproj
```

### Docker
```powershell
./webui/build.sh
dotnet build -c Release -o ./BililiveRecorder.Cli/bin/docker_out BililiveRecorder.Cli/BililiveRecorder.Cli.csproj
docker build -f Dockerfile.GitHubActions -t bililiverecorder:latest .
```

## See Also

- [CLAUDE.md](CLAUDE.md) - Project overview and architecture
- [GitHub Actions Workflows](.github/workflows/) - CI/CD configuration
- [Dockerfile.GitHubActions](Dockerfile.GitHubActions) - Docker build configuration
