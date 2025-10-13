using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lacesong.Core.Services;
using Lacesong.Core.Models;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Comprehensive Game Detection Test ===");
        Console.WriteLine($"Platform: {PlatformDetector.CurrentPlatform}");
        Console.WriteLine($"OS: Windows={PlatformDetector.IsWindows}, macOS={PlatformDetector.IsMacOS}, Linux={PlatformDetector.IsLinux}");
        Console.WriteLine();

        var detector = new GameDetector();

        // test 1: automatic detection
        Console.WriteLine("Test 1: Automatic Detection");
        var detectedGame = await detector.DetectGameInstall();
        
        if (detectedGame != null)
        {
            Console.WriteLine($"  ✓ Game detected!");
            Console.WriteLine($"    Name: {detectedGame.Name}");
            Console.WriteLine($"    Path: {detectedGame.InstallPath}");
            Console.WriteLine($"    Executable: {detectedGame.Executable}");
            Console.WriteLine($"    Detected By: {detectedGame.DetectedBy}");
            Console.WriteLine($"    Is Valid: {detectedGame.IsValid}");
        }
        else
        {
            Console.WriteLine($"  ✗ No game detected");
        }
        Console.WriteLine();

        // test 2: manual path detection (if game was found)
        if (detectedGame != null)
        {
            Console.WriteLine("Test 2: Manual Path Detection");
            var manualDetection = await detector.DetectGameInstall(detectedGame.InstallPath);
            
            if (manualDetection != null)
            {
                Console.WriteLine($"  ✓ Game detected from manual path!");
                Console.WriteLine($"    Name: {manualDetection.Name}");
                Console.WriteLine($"    Detected By: {manualDetection.DetectedBy}");
            }
            else
            {
                Console.WriteLine($"  ✗ Manual path detection failed");
            }
            Console.WriteLine();
        }

        // test 3: validation
        if (detectedGame != null)
        {
            Console.WriteLine("Test 3: Game Installation Validation");
            var isValid = detector.ValidateGameInstall(detectedGame);
            Console.WriteLine($"  Validation result: {(isValid ? "✓ Valid" : "✗ Invalid")}");
            Console.WriteLine();
        }

        // test 4: check executable paths
        if (detectedGame != null)
        {
            Console.WriteLine("Test 4: Executable Path Verification");
            var executablePath = Path.Combine(detectedGame.InstallPath, detectedGame.Executable);
            Console.WriteLine($"  Basic path: {executablePath}");
            Console.WriteLine($"    Exists: {File.Exists(executablePath)}");
            
            if (PlatformDetector.IsMacOS)
            {
                var appBundlePath = Path.Combine(detectedGame.InstallPath, $"{Path.GetFileNameWithoutExtension(detectedGame.Executable)}.app");
                Console.WriteLine($"  App bundle: {appBundlePath}");
                Console.WriteLine($"    Exists: {Directory.Exists(appBundlePath)}");
                
                if (Directory.Exists(appBundlePath))
                {
                    var appExePath = Path.Combine(appBundlePath, "Contents", "MacOS", Path.GetFileNameWithoutExtension(detectedGame.Executable));
                    Console.WriteLine($"  Executable in bundle: {appExePath}");
                    Console.WriteLine($"    Exists: {File.Exists(appExePath)}");
                    Console.WriteLine($"    Is Valid Executable: {PlatformDetector.IsValidExecutable(appExePath)}");
                }
            }
            Console.WriteLine();
        }

        // test 5: supported games
        Console.WriteLine("Test 5: Supported Games");
        var supportedGames = await detector.GetSupportedGames();
        Console.WriteLine($"  Supported games count: {supportedGames.Count}");
        foreach (var game in supportedGames)
        {
            Console.WriteLine($"    - {game.Name} (ID: {game.Id})");
            Console.WriteLine($"      Steam: {game.SteamAppId}, Epic: {game.EpicAppId}");
            Console.WriteLine($"      Executable: {game.Executable}");
        }
        Console.WriteLine();

        Console.WriteLine("=== All Tests Complete ===");
    }
}
