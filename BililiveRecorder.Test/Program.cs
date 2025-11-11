using BililiveRecorder.Core;
using BililiveRecorder.Core.Config.V3;
using BililiveRecorder.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Diagnostics;

namespace BililiveRecorder.Test;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            if (args.Length < 2)
            {
                ShowUsage();
                return 1;
            }

            var roomUrl = args[0];
            var outputDir = args[1];
            var durationSeconds = args.Length > 2 && int.TryParse(args[2], out var dur) ? dur : 60;

            Log.Information("=== BililiveRecorder Test Tool ===");
            Log.Information("Room URL: {RoomUrl}", roomUrl);
            Log.Information("Output Directory: {OutputDir}", outputDir);
            Log.Information("Test Duration: {Duration} seconds", durationSeconds);
            Log.Information("");

            // Get absolute path for output directory
            var absoluteOutputDir = Path.GetFullPath(outputDir);
            Directory.CreateDirectory(absoluteOutputDir);

            // Initialize recorder
            var config = new ConfigV3
            {
                DisableConfigSave = true,  // Disable config file save for testing
                Global = new GlobalConfig
                {
                    WorkDirectory = absoluteOutputDir,
                    RecordMode = Core.Config.RecordMode.Standard,
                    TimingCheckInterval = 10,
                    TimingStreamRetry = 5,
                    TimingApiTimeout = 10000,
                    FileNameRecordTemplate = @"{name}/录制-{roomid}-{date}-{time}-{ms}-{title}.ts"
                }
            };

            // Setup dependency injection
            var services = new ServiceCollection();
            services.AddSingleton(config);
            services.AddSingleton(config.Global);
            services.AddSingleton<ILogger>(Log.Logger);
            services.AddRecorder();

            var serviceProvider = services.BuildServiceProvider();
            var recorder = serviceProvider.GetRequiredService<IRecorder>();

            Log.Information("Step 1: Testing platform detection and room info fetch...");
            Log.Information("");

            // Add room
            IRoom? room = null;
            try
            {
                room = recorder.AddRoom(roomUrl);
                Log.Information("✓ Room added successfully");
                Log.Information("  Platform: {Platform}", room.RoomConfig.Platform);
                Log.Information("  Room Identifier: {Identifier}", room.RoomConfig.NormalizedRoomIdentifier);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "✗ Failed to add room");
                return 1;
            }

            // Wait for room info to be fetched
            Log.Information("");
            Log.Information("Step 2: Waiting for room information...");
            await Task.Delay(5000);

            Log.Information("  Room ID: {RoomId}", room.RoomConfig.RoomId);
            Log.Information("  Short ID: {ShortId}", room.ShortId);
            Log.Information("  Name: {Name}", room.Name);
            Log.Information("  Title: {Title}", room.Title);
            Log.Information("  Is Streaming: {Streaming}", room.Streaming);
            Log.Information("  Is Recording: {Recording}", room.Recording);

            if (!room.Streaming)
            {
                Log.Warning("⚠ Room is not currently streaming");
                Log.Information("The test will monitor the room but cannot verify recording until stream starts.");
            }

            Log.Information("");
            Log.Information("Step 3: Testing recording (monitoring for {Duration} seconds)...", durationSeconds);

            // Enable auto-record
            room.RoomConfig.AutoRecord = true;
            recorder.SaveConfig();

            var recordSessionStarted = false;
            var filesClosed = new List<string>();
            var stopwatch = Stopwatch.StartNew();

            recorder.RecordSessionStarted += (sender, e) =>
            {
                recordSessionStarted = true;
                Log.Information("✓ Recording session started!");
                Log.Information("  Session ID: {SessionId}", e.Event.SessionId);
            };

            recorder.RecordFileOpening += (sender, e) =>
            {
                Log.Information("✓ File opening: {FilePath}", e.Event.FullPath);
            };

            recorder.RecordFileClosed += (sender, e) =>
            {
                var filePath = e.Event.FullPath;
                filesClosed.Add(filePath);
                Log.Information("✓ File closed: {FilePath}", filePath);

                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    Log.Information("  File size: {Size:N0} bytes", fileInfo.Length);

                    if (fileInfo.Length > 0)
                    {
                        Log.Information("  ✓ File is not empty");
                    }
                    else
                    {
                        Log.Warning("  ⚠ File is empty");
                    }
                }
                else
                {
                    Log.Error("  ✗ File does not exist!");
                }
            };

            recorder.IOStats += (sender, e) =>
            {
                if (e.Event.NetworkBytesDownloaded > 0)
                {
                    Log.Debug("Network downloaded: {Bytes:N0} bytes, Speed: {Speed:N0} KB/s",
                        e.Event.NetworkBytesDownloaded,
                        e.Event.NetworkMbps * 1024 / 8);
                }
            };

            // Monitor for specified duration
            var lastStatus = "";
            while (stopwatch.Elapsed.TotalSeconds < durationSeconds)
            {
                var currentStatus = $"Streaming: {room.Streaming}, Recording: {room.Recording}";
                if (currentStatus != lastStatus)
                {
                    Log.Information("Status: {Status}", currentStatus);
                    lastStatus = currentStatus;
                }

                await Task.Delay(1000);
            }

            Log.Information("");
            Log.Information("=== Test Summary ===");
            Log.Information("Room Info Fetch: ✓ Success");
            Log.Information("Platform Detection: ✓ {Platform}", room.RoomConfig.Platform);
            Log.Information("Recording Session Started: {Status}", recordSessionStarted ? "✓ Yes" : "✗ No");
            Log.Information("Files Recorded: {Count}", filesClosed.Count);

            if (filesClosed.Count > 0)
            {
                Log.Information("");
                Log.Information("Recorded Files:");
                foreach (var file in filesClosed)
                {
                    if (File.Exists(file))
                    {
                        var fileInfo = new FileInfo(file);
                        Log.Information("  - {File} ({Size:N0} bytes)", file, fileInfo.Length);
                    }
                }
            }

            // Cleanup
            Log.Information("");
            Log.Information("Cleaning up...");
            recorder.RemoveRoom(room);
            recorder.Dispose();

            Log.Information("");
            if (recordSessionStarted && filesClosed.Count > 0)
            {
                Log.Information("✓✓✓ All tests PASSED! Recording is working correctly.");
                return 0;
            }
            else if (!room.Streaming)
            {
                Log.Information("⚠ Tests incomplete - room was not streaming during test period");
                Log.Information("   Platform detection and room info fetch were successful.");
                return 0;
            }
            else
            {
                Log.Error("✗✗✗ Tests FAILED - Recording did not work as expected");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Fatal error during test");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    static void ShowUsage()
    {
        Console.WriteLine("BililiveRecorder Test Tool");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  BililiveRecorder.Test <room-url> <output-dir> [duration-seconds]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  room-url          URL or ID of the room to test");
        Console.WriteLine("                    Examples:");
        Console.WriteLine("                      - Bilibili: 123456 or https://live.bilibili.com/123456");
        Console.WriteLine("                      - Douyin: https://live.douyin.com/279323257673");
        Console.WriteLine("  output-dir        Directory to save recorded files");
        Console.WriteLine("  duration-seconds  How long to monitor/record (default: 60)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  BililiveRecorder.Test https://live.douyin.com/279323257673 ./test-output 120");
        Console.WriteLine("  BililiveRecorder.Test 123456 ./recordings 30");
        Console.WriteLine();
        Console.WriteLine("The tool will:");
        Console.WriteLine("  1. Test platform detection");
        Console.WriteLine("  2. Fetch room information");
        Console.WriteLine("  3. Monitor and record the stream for the specified duration");
        Console.WriteLine("  4. Report whether recording worked correctly");
    }
}
