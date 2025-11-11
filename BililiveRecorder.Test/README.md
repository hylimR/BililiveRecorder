# BililiveRecorder Test Tool

A console tool for ad hoc testing of room monitoring and recording functionality.

## Usage

```bash
dotnet run --project BililiveRecorder.Test -- <room-url> <output-dir> [duration-seconds]
```

Or after building:

```bash
BililiveRecorder.Test <room-url> <output-dir> [duration-seconds]
```

## Arguments

- `room-url`: URL or ID of the room to test
  - Bilibili: `123456` or `https://live.bilibili.com/123456`
  - Douyin: `https://live.douyin.com/279323257673`
- `output-dir`: Directory to save recorded files
- `duration-seconds`: How long to monitor/record (default: 60 seconds)

## Examples

### Test Douyin room for 2 minutes
```bash
dotnet run --project BililiveRecorder.Test -- https://live.douyin.com/279323257673 ./test-output 120
```

### Test Bilibili room for 30 seconds
```bash
dotnet run --project BililiveRecorder.Test -- 123456 ./recordings 30
```

### Quick test with defaults
```bash
dotnet run --project BililiveRecorder.Test -- https://live.bilibili.com/123456 ./test
```

## What It Tests

The tool performs the following tests:

1. **Platform Detection**: Verifies that the platform (Bilibili/Douyin) is correctly detected from the URL
2. **Room Info Fetch**: Tests fetching room information (title, streamer name, live status, etc.)
3. **Recording**: Monitors the room and attempts to record the stream for the specified duration
4. **File Verification**: Checks that recorded files exist and are not empty

## Output

The tool provides detailed logging of each step:

```
=== BililiveRecorder Test Tool ===
Room URL: https://live.douyin.com/279323257673
Output Directory: ./test-output
Test Duration: 120 seconds

Step 1: Testing platform detection and room info fetch...
✓ Room added successfully
  Platform: Douyin
  Room Identifier: 279323257673

Step 2: Waiting for room information...
  Room ID: 279323257673
  Short ID: 0
  Name: 主播名称
  Title: 直播标题
  Is Streaming: True
  Is Recording: False

Step 3: Testing recording (monitoring for 120 seconds)...
Status: Streaming: True, Recording: False
✓ Recording session started!
  Session ID: 12345678-1234-1234-1234-123456789abc
✓ File opening: ./test-output/主播名称/录制-279323257673-20250111-143022-123-直播标题.flv
Status: Streaming: True, Recording: True
✓ File closed: ./test-output/主播名称/录制-279323257673-20250111-143022-123-直播标题.flv
  File size: 123,456,789 bytes
  ✓ File is not empty

=== Test Summary ===
Room Info Fetch: ✓ Success
Platform Detection: ✓ Douyin
Recording Session Started: ✓ Yes
Files Recorded: 1

Recorded Files:
  - ./test-output/主播名称/录制-279323257673-20250111-143022-123-直播标题.flv (123,456,789 bytes)

✓✓✓ All tests PASSED! Recording is working correctly.
```

## Exit Codes

- `0`: Tests passed or room was not streaming (platform detection still successful)
- `1`: Tests failed or an error occurred

## Use Cases

- Verify that Douyin integration is working correctly
- Test recording before setting up automated recording
- Debug issues with specific rooms or platforms
- Quick validation after code changes
- Reproduce and diagnose recording problems
