using System;
using System.Collections.Generic;
using System.IO;
using Lacesong.Core.Services;
using Lacesong.Core.Models;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Game Detection Test ===");
        Console.WriteLine($"Platform: {PlatformDetector.CurrentPlatform}");
        Console.WriteLine($"Is macOS: {PlatformDetector.IsMacOS}");
        Console.WriteLine();

        // test steam paths
        Console.WriteLine("=== Steam Paths ===");
        var steamPaths = PlatformDetector.GetSteamPaths();
        foreach (var path in steamPaths)
        {
            Console.WriteLine($"  {path} (Exists: {Directory.Exists(path)})");
        }
        Console.WriteLine();

        // check steamapps/common locations
        Console.WriteLine("=== Checking steamapps/common ===");
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var testPaths = new List<string>
        {
            Path.Combine(homeDir, "Library", "Application Support", "Steam", "steamapps", "common"),
            Path.Combine(homeDir, "Library", "Application Support", "Steam", "steamapps", "libraryfolders.vdf")
        };

        foreach (var testPath in testPaths)
        {
            Console.WriteLine($"  {testPath}");
            Console.WriteLine($"    Exists: {(Directory.Exists(testPath) || File.Exists(testPath))}");
            
            if (Directory.Exists(testPath))
            {
                try
                {
                    var subdirs = Directory.GetDirectories(testPath);
                    Console.WriteLine($"    Subdirectories: {subdirs.Length}");
                    foreach (var subdir in subdirs.Take(10))
                    {
                        Console.WriteLine($"      - {Path.GetFileName(subdir)}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    Error listing: {ex.Message}");
                }
            }
            else if (File.Exists(testPath))
            {
                try
                {
                    var content = File.ReadAllText(testPath);
                    Console.WriteLine($"    File size: {content.Length} bytes");
                    Console.WriteLine($"    First 500 chars: {content.Substring(0, Math.Min(500, content.Length))}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    Error reading: {ex.Message}");
                }
            }
        }
        Console.WriteLine();

        // check the specific game location
        Console.WriteLine("=== Checking Specific Game Location ===");
        var gamePath = Path.Combine(homeDir, "Library", "Application Support", "Steam", "steamapps", "common", "Hollow Knight Silksong");
        Console.WriteLine($"  Path: {gamePath}");
        Console.WriteLine($"  Exists: {Directory.Exists(gamePath)}");
        
        if (Directory.Exists(gamePath))
        {
            var files = Directory.GetFileSystemEntries(gamePath);
            Console.WriteLine($"  Contents:");
            foreach (var file in files)
            {
                Console.WriteLine($"    - {Path.GetFileName(file)}");
            }
            Console.WriteLine();

            // check for .app bundle
            var appBundle = Path.Combine(gamePath, "Hollow Knight Silksong.app");
            Console.WriteLine($"  App Bundle: {appBundle}");
            Console.WriteLine($"    Exists: {Directory.Exists(appBundle)}");
            
            if (Directory.Exists(appBundle))
            {
                var exePath = Path.Combine(appBundle, "Contents", "MacOS", "Hollow Knight Silksong");
                Console.WriteLine($"  Executable: {exePath}");
                Console.WriteLine($"    Exists: {File.Exists(exePath)}");
                Console.WriteLine($"    Is Valid Executable: {PlatformDetector.IsValidExecutable(exePath)}");
            }
        }
        Console.WriteLine();

        // run actual detection
        Console.WriteLine("=== Running GameDetector ===");
        var detector = new GameDetector();
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
    }
}

