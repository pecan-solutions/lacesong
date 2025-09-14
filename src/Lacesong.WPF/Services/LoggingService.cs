using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;

namespace Lacesong.WPF.Services;

/// <summary>
/// implementation of logging service for wpf
/// </summary>
public class LoggingService : ILoggingService
{
    private readonly ILogger<LoggingService> _logger;
    private readonly string _logsDirectory;

    public LoggingService(ILogger<LoggingService> logger)
    {
        _logger = logger;
        _logsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lacesong", "Logs");
        
        // ensure logs directory exists
        Directory.CreateDirectory(_logsDirectory);
    }

    public void LogInfo(string message)
    {
        _logger.LogInformation(message);
    }

    public void LogWarning(string message)
    {
        _logger.LogWarning(message);
    }

    public void LogError(string message)
    {
        _logger.LogError(message);
    }

    public void LogError(Exception exception)
    {
        _logger.LogError(exception, "An error occurred");
    }

    public void OpenLogsFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _logsDirectory,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open logs folder");
        }
    }

    public void ClearLogs()
    {
        try
        {
            var logFiles = Directory.GetFiles(_logsDirectory, "*.log");
            foreach (var logFile in logFiles)
            {
                File.Delete(logFile);
            }
            
            _logger.LogInformation("Log files cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear log files");
        }
    }
}
