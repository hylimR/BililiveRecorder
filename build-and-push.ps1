#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build and publish BililiveRecorder projects

.DESCRIPTION
    This script builds BililiveRecorder CLI, WPF, or Docker images with various options.
    It can build for multiple platforms, configurations, and optionally push Docker images.

.PARAMETER Target
    Build target: CLI, WPF, Docker, or All (default: All)

.PARAMETER Configuration
    Build configuration: Debug or Release (default: Release)

.PARAMETER Runtime
    Runtime identifier for CLI builds (e.g., win-x64, linux-x64, osx-x64, linux-arm64)
    Use 'all' to build for all supported platforms
    Use 'any' for framework-dependent deployment

.PARAMETER BuildWebUI
    Build WebUI before building CLI (required for CLI)

.PARAMETER SkipTests
    Skip running tests before building

.PARAMETER PushDocker
    Push Docker image to registry after building

.PARAMETER DockerRegistry
    Docker registry to push to (default: ghcr.io)

.PARAMETER DockerTag
    Docker image tag (default: dev)

.PARAMETER OutputDir
    Custom output directory for builds

.EXAMPLE
    .\build-and-push.ps1
    Build all targets in Release configuration

.EXAMPLE
    .\build-and-push.ps1 -Target CLI -Runtime win-x64
    Build CLI for Windows x64

.EXAMPLE
    .\build-and-push.ps1 -Target CLI -Runtime all -BuildWebUI
    Build CLI for all platforms with WebUI

.EXAMPLE
    .\build-and-push.ps1 -Target Docker -PushDocker -DockerTag latest
    Build and push Docker image with 'latest' tag

.EXAMPLE
    .\build-and-push.ps1 -SkipTests -Configuration Debug
    Quick debug build without tests
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('CLI', 'WPF', 'Docker', 'All')]
    [string]$Target = 'All',

    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [Parameter()]
    [ValidateSet('any', 'all', 'linux-arm', 'linux-arm64', 'linux-musl-arm64', 'linux-x64', 'linux-musl-x64', 'osx-x64', 'osx-arm64', 'win-x64')]
    [string]$Runtime = 'any',

    [Parameter()]
    [switch]$BuildWebUI,

    [Parameter()]
    [switch]$SkipTests,

    [Parameter()]
    [switch]$PushDocker,

    [Parameter()]
    [string]$DockerRegistry = 'ghcr.io',

    [Parameter()]
    [string]$DockerTag = 'dev',

    [Parameter()]
    [string]$OutputDir = ''
)

# Error handling
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Color output functions
function Write-Header {
    param([string]$Message)
    Write-Host "`n===================================================" -ForegroundColor Cyan
    Write-Host "  $Message" -ForegroundColor Cyan
    Write-Host "===================================================" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor Green
}

function Write-Error {
    param([string]$Message)
    Write-Host "✗ $Message" -ForegroundColor Red
}

function Write-Info {
    param([string]$Message)
    Write-Host "→ $Message" -ForegroundColor Yellow
}

# Get script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $ScriptDir

try {
    Write-Header "BililiveRecorder Build Script"
    Write-Info "Target: $Target"
    Write-Info "Configuration: $Configuration"
    Write-Info "Script Directory: $ScriptDir"

    # Check prerequisites
    Write-Header "Checking Prerequisites"

    # Check Git
    $gitVersion = git --version 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Git is not installed or not in PATH"
    }
    Write-Success "Git: $gitVersion"

    # Check .NET SDK
    $dotnetVersion = dotnet --version 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw ".NET SDK is not installed or not in PATH"
    }
    Write-Success ".NET SDK: $dotnetVersion"

    # Check if this is a git repository
    $isGitRepo = Test-Path ".git"
    if (-not $isGitRepo) {
        Write-Error "Not a git repository - version generation may fail"
    }

    # Run tests (unless skipped)
    if (-not $SkipTests) {
        Write-Header "Running Tests"

        Write-Info "Running tests in $Configuration mode..."
        dotnet test -v m -c $Configuration
        if ($LASTEXITCODE -ne 0) {
            throw "Tests failed"
        }
        Write-Success "All tests passed"
    } else {
        Write-Info "Skipping tests (--SkipTests specified)"
    }

    # Build WebUI if requested or if building CLI/All
    if ($BuildWebUI -or $Target -in @('CLI', 'Docker', 'All')) {
        Write-Header "Building WebUI"

        if (Test-Path "webui/build.ps1") {
            Write-Info "Building WebUI using build.ps1..."
            & "$ScriptDir/webui/build.ps1"
            if ($LASTEXITCODE -ne 0) {
                throw "WebUI build failed"
            }
            Write-Success "WebUI built successfully"
        } else {
            Write-Error "WebUI build script not found at webui/build.ps1"
        }
    }

    # Build CLI
    if ($Target -in @('CLI', 'All')) {
        Write-Header "Building CLI"

        $cliProject = "BililiveRecorder.Cli/BililiveRecorder.Cli.csproj"

        if ($Runtime -eq 'all') {
            # Build for all supported runtimes
            $runtimes = @('linux-arm', 'linux-arm64', 'linux-musl-arm64', 'linux-x64', 'linux-musl-x64', 'osx-x64', 'osx-arm64', 'win-x64')

            foreach ($rid in $runtimes) {
                Write-Info "Building CLI for $rid..."

                $publishArgs = @(
                    'publish',
                    '-c', $Configuration,
                    '-r', $rid,
                    $cliProject
                )

                if ($OutputDir) {
                    $publishArgs += '-o', "$OutputDir/CLI-$rid-$Configuration"
                }

                dotnet @publishArgs
                if ($LASTEXITCODE -ne 0) {
                    throw "CLI build failed for $rid"
                }
                Write-Success "CLI built for $rid"
            }
        } elseif ($Runtime -eq 'any') {
            # Framework-dependent deployment
            Write-Info "Building CLI (framework-dependent)..."

            $publishArgs = @(
                'publish',
                '-c', $Configuration,
                $cliProject
            )

            if ($OutputDir) {
                $publishArgs += '-o', "$OutputDir/CLI-any-$Configuration"
            }

            dotnet @publishArgs
            if ($LASTEXITCODE -ne 0) {
                throw "CLI build failed"
            }
            Write-Success "CLI built (framework-dependent)"
        } else {
            # Build for specific runtime
            Write-Info "Building CLI for $Runtime..."

            $publishArgs = @(
                'publish',
                '-c', $Configuration,
                '-r', $Runtime,
                $cliProject
            )

            if ($OutputDir) {
                $publishArgs += '-o', "$OutputDir/CLI-$Runtime-$Configuration"
            }

            dotnet @publishArgs
            if ($LASTEXITCODE -ne 0) {
                throw "CLI build failed for $Runtime"
            }
            Write-Success "CLI built for $Runtime"
        }
    }

    # Build WPF
    if ($Target -in @('WPF', 'All')) {
        Write-Header "Building WPF"

        # Check if MSBuild is available
        $msbuildPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
            -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe `
            -prerelease 2>$null | Select-Object -First 1

        if (-not $msbuildPath) {
            # Try to find msbuild in PATH
            $msbuildPath = Get-Command msbuild -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
        }

        if (-not $msbuildPath) {
            Write-Error "MSBuild not found. WPF build requires Visual Studio or MSBuild Tools."
            Write-Info "Skipping WPF build..."
        } else {
            Write-Success "MSBuild found: $msbuildPath"

            $wpfProject = "BililiveRecorder.WPF/BililiveRecorder.WPF.csproj"

            Write-Info "Restoring NuGet packages..."
            & $msbuildPath -t:restore /v:m $wpfProject
            if ($LASTEXITCODE -ne 0) {
                throw "WPF restore failed"
            }

            Write-Info "Building WPF..."
            & $msbuildPath /nologo /v:m /p:Configuration=$Configuration $wpfProject
            if ($LASTEXITCODE -ne 0) {
                throw "WPF build failed"
            }

            Write-Success "WPF built successfully"
            Write-Info "Output: BililiveRecorder.WPF/bin/$Configuration"
        }
    }

    # Build Docker
    if ($Target -in @('Docker', 'All')) {
        Write-Header "Building Docker Image"

        # Check if Docker is available
        $dockerVersion = docker --version 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Docker is not installed or not in PATH"
            Write-Info "Skipping Docker build..."
        } else {
            Write-Success "Docker: $dockerVersion"

            # Determine image name
            $gitRemote = git remote get-url origin 2>$null
            $imageName = "bililiverecorder"

            if ($gitRemote -match 'github.com[:/](.+?)/.+?(?:\.git)?$') {
                $username = $Matches[1]
                $imageName = "$DockerRegistry/$username/bililiverecorder"
            }

            $fullImageTag = "${imageName}:${DockerTag}"

            Write-Info "Building Docker image: $fullImageTag"

            # Build CLI for Docker (if not already built)
            if (-not (Test-Path "BililiveRecorder.Cli/bin/docker_out")) {
                Write-Info "Building CLI for Docker..."
                dotnet build -c Release -o ./BililiveRecorder.Cli/bin/docker_out BililiveRecorder.Cli/BililiveRecorder.Cli.csproj
                if ($LASTEXITCODE -ne 0) {
                    throw "CLI build for Docker failed"
                }
            }

            # Build Docker image
            $dockerFile = "Dockerfile.GitHubActions"
            if (-not (Test-Path $dockerFile)) {
                $dockerFile = "Dockerfile"
            }

            docker build -f $dockerFile -t $fullImageTag .
            if ($LASTEXITCODE -ne 0) {
                throw "Docker build failed"
            }

            Write-Success "Docker image built: $fullImageTag"

            # Push to registry if requested
            if ($PushDocker) {
                Write-Info "Pushing Docker image to registry..."
                docker push $fullImageTag
                if ($LASTEXITCODE -ne 0) {
                    throw "Docker push failed"
                }
                Write-Success "Docker image pushed: $fullImageTag"
            } else {
                Write-Info "Docker image not pushed (use -PushDocker to push)"
            }
        }
    }

    Write-Header "Build Complete"
    Write-Success "All builds completed successfully!"

    # Display output locations
    Write-Host "`nOutput Locations:" -ForegroundColor Cyan
    if ($Target -in @('CLI', 'All')) {
        if ($OutputDir) {
            Write-Host "  CLI: $OutputDir/CLI-*" -ForegroundColor White
        } else {
            Write-Host "  CLI: BililiveRecorder.Cli/publish/" -ForegroundColor White
        }
    }
    if ($Target -in @('WPF', 'All')) {
        Write-Host "  WPF: BililiveRecorder.WPF/bin/$Configuration" -ForegroundColor White
    }
    if ($Target -in @('Docker', 'All')) {
        Write-Host "  Docker: $fullImageTag" -ForegroundColor White
    }

} catch {
    Write-Error "Build failed: $_"
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    exit 1
} finally {
    Pop-Location
}
