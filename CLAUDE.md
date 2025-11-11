# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

BililiveRecorder (mikufans录播姬) is a live stream recorder for Bilibili and Douyin platforms. It records live streams automatically, fixes broken streams caused by server issues, and provides both desktop (WPF) and command-line interfaces. The project is written primarily in C# and targets both .NET 8.0 and .NET Framework 4.7.2.

Recent addition: Multi-platform support with Douyin integration via a platform abstraction layer (see DOUYIN_INTEGRATION.md).

## Build Commands

### Prerequisites
- .NET SDK 8.0+ (configured in global.json)
- Full git history required for version generation

### Building WPF Desktop Version
```powershell
cd BililiveRecorder/BililiveRecorder.WPF
msbuild -t:restore
msbuild
```

### Building Command Line Version
```sh
# Optional: Build WebUI first
git submodule update --init --recursive
./BililiveRecorder/webui/build.sh      # On Linux/macOS
# ./BililiveRecorder/webui/build.ps1   # On Windows

# Build CLI
cd BililiveRecorder
dotnet build BililiveRecorder.Cli
```

### Building Solution
```sh
cd BililiveRecorder
dotnet build BililiveRecorder.sln
```

### Running Tests
```sh
cd BililiveRecorder
dotnet test
```

### Ad Hoc Testing Tool
For manual testing of room monitoring and recording:
```sh
cd BililiveRecorder
dotnet run --project BililiveRecorder.Test -- <room-url> <output-dir> [duration-seconds]
```

Examples:
```sh
# Test Douyin room for 2 minutes
dotnet run --project BililiveRecorder.Test -- https://live.douyin.com/279323257673 ./test-output 120

# Test Bilibili room for 30 seconds
dotnet run --project BililiveRecorder.Test -- 123456 ./test 30
```

The tool verifies:
- Platform detection (Bilibili/Douyin)
- Room info fetching
- Recording functionality
- File output validation

### Config Code Generation
The project uses TypeScript code generators for configuration classes:
```sh
cd BililiveRecorder/config_gen
npm install
npm run build                    # Generate all sections
npm run build code core          # Generate only core config code
npm run build code cli web schema  # Generate specific sections
```

## Architecture

### Project Structure

```
BililiveRecorder.Flv          → .NET Standard 2.0 (FLV format handling)
BililiveRecorder.Core         → .NET 8.0 + .NET Framework 4.7.2 (Core recording logic)
BililiveRecorder.ToolBox      → .NET Standard 2.0 (Repair utilities)
BililiveRecorder.WPF          → .NET Framework 4.7.2 (Desktop GUI)
BililiveRecorder.Web          → .NET 8.0 (Web interface)
BililiveRecorder.Cli          → .NET 8.0 (Command line + Web server)
```

**Dependency Flow:**
- `Core` and `ToolBox` depend on `Flv`
- `WPF` depends on `Core` and `ToolBox`
- `Cli` depends on `Core`, `ToolBox`, and `Web`
- `Web` depends on `Core`

### Multi-Platform Recording Architecture

The project recently added support for multiple streaming platforms (Bilibili and Douyin) through an abstraction layer:

**Platform Abstraction Layer:**
- `IPlatformApiClient` - Common interface for all platforms
- `PlatformRoomInfo` / `PlatformStreamInfo` - Unified data models
- `StreamingPlatform` enum - Platform identifiers (Bilibili, Douyin)

**Platform Implementations:**
- `BilibiliApiClient` - Wraps existing Bilibili API client
- `DouyinApiClient` - HTML parsing-based client for Douyin (no official API)
- `PlatformApiClientFactory` - Creates appropriate client based on platform detection
- `PlatformDetector` - URL pattern matching for auto-detection

**Integration Points:**
- `Room.cs` - Uses platform factory instead of direct Bilibili API
- `RecordTaskBase.cs` - Platform-aware stream URL fetching
- `RoomConfig` - Added `Platform` and `RoomUrl` properties for multi-platform support

**Quality Mapping:**
Bilibili quality numbers (QN) are used as the common quality scale. Douyin qualities (ORIGIN, UHD, HD, SD, etc.) are mapped to equivalent Bilibili QN values.

### Core Recording Flow

1. **Room Monitoring** (`Room.cs`):
   - Checks room status periodically using platform-specific API client
   - Manages danmaku client connection (Bilibili only)
   - Triggers recording when stream goes live

2. **Recording Tasks** (`Recording/`):
   - `StandardRecordTask` - Normal mode with FLV processing and repair
   - `RawDataRecordTask` - Raw mode, saves stream data as-is
   - Both use platform factory to get stream URLs

3. **FLV Processing** (`BililiveRecorder.Flv`):
   - Parses FLV tags from stream
   - Fixes broken streams (timestamp issues, missing headers, etc.)
   - Pure C# implementation, no FFmpeg dependency for recording

4. **File Naming** (`Templating/FileNameGenerator.cs`):
   - Uses Fluid template engine for customizable file names
   - Supports variables like room ID, title, date, quality, etc.

### Configuration System

- **V3 Config** (Current): Uses `HierarchicalPropertyDefault` for inheritance
- Generated code in `Config/V3/Config.gen.cs` from TypeScript generators
- JSON schema files: `configV3.schema.json`
- Config can be modified at runtime and persists changes

### Scripting Support

JavaScript execution via Jint engine for:
- Custom stream URL modification
- DNS resolution control
- Platform-specific workarounds (e.g., Douyin x-bogus signature generation)

Runtime objects exposed to scripts: `JintFetchSync`, `JintDns`, `JintConsole`, `JintURL`, etc.

## Platform-Specific Notes

### Douyin Integration
- Uses HTML parsing (no official API)
- Requires extracting `roomStore` from `window.__INITIAL_STATE__`
- May break if Douyin changes page structure
- Danmaku not supported for Douyin (only video recording)
- Short URLs (`v.douyin.com`) are auto-resolved

### Bilibili Integration
- Uses official live API endpoints
- Supports both FLV and HLS streams
- Danmaku recording via WebSocket/TCP
- Wbi signature generation for authenticated requests

## Development Patterns

### Dependency Injection
Services registered in `DependencyInjectionExtensions.cs`:
- Room-scoped services (per recording room)
- Singleton services (API clients, factories)
- Platform factory is injected into rooms and record tasks

### Logging
- Uses Serilog throughout
- Context enrichment with room ID, session ID
- Log levels: Debug, Information, Warning, Error

### Error Handling
- Polly for retry policies (`PollyPolicy.cs`)
- Platform-specific exceptions handled gracefully
- Recording continues despite transient errors

## Configuration Format

Rooms can be configured using:
- `RoomId` (legacy, Bilibili numeric ID)
- `RoomUrl` (auto-detects platform from URL)
- `Platform` + `RoomUrl` (explicit platform)

Example Douyin room:
```json
{
  "RoomUrl": "https://live.douyin.com/123456789",
  "AutoRecord": true
}
```

Example Bilibili room (backward compatible):
```json
{
  "RoomId": 123456,
  "AutoRecord": true
}
```

## Testing

- xUnit test framework
- Test projects: `BililiveRecorder.Core.UnitTests`, `BililiveRecorder.Flv.Tests`
- Tests cover FLV parsing, config system, platform detection

## Code Generation

When modifying configuration schema:
1. Edit `config_gen/data.ts` to define config properties
2. Run `npm run build code` to regenerate C# code
3. Generated files include: Config classes, CLI models, Web API models, JSON schemas
4. Never manually edit `*.gen.cs` files

## DouyinLiveRecorder (Reference Only)

The `DouyinLiveRecorder/` directory contains a separate Python-based multi-platform live stream recorder project (originally from https://github.com/ihmily/DouyinLiveRecorder). This project supports 60+ streaming platforms including Douyin, TikTok, Kuaishou, Huya, Douyu, Bilibili, and more.

**Important:** DouyinLiveRecorder is included for **reference purposes only**. It is a completely independent project and is not integrated into BililiveRecorder's C# codebase. The JavaScript files in `DouyinLiveRecorder/src/javascript/` (such as `x-bogus.js`, `taobao-sign.js`) may be useful references for understanding platform-specific signature generation or anti-bot mechanisms.

The Douyin integration in BililiveRecorder (see DOUYIN_INTEGRATION.md) was implemented independently using C# and does not directly use code from the DouyinLiveRecorder Python project.

## Important Constraints

- Semantic versioning since 2.0.0
- .NET public APIs are internal implementation (breaking changes allowed)
- Chinese is the primary language for comments and documentation
- UI supports: 简体中文, 繁体中文, 日本語, English
