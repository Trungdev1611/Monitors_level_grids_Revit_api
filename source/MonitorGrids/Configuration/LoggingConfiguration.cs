using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace MonitorGrids.Configuration;

/// <summary>
///     Application logging configuration.
/// </summary>
/// <example>
/// <code lang="csharp">
/// public class Class(ILogger logger)
/// {
///     private void Execute()
///     {
///         logger.Information("Message");
///     }
/// }
/// </code>
/// </example>
public static class LoggingConfiguration
{
    private const string LogTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}]: {Message:lj}{NewLine}{Exception}";

    extension(IServiceCollection services)
    {
        public void AddSerilog()
        {
            var logger = CreateDefaultLogger();
            services.AddSingleton<ILogger>(logger);

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }
    }

    private static Logger CreateDefaultLogger()
    {
        // 1. Lấy đường dẫn thẳng đến màn hình Desktop của máy tính hiện tại
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        // 2. Tạo file log nằm ngay trên Desktop (Ví dụ đặt tên là: MonitorGrids_Log.txt)
        string logPath = Path.Combine(desktopPath, "MonitorGrids_Log.txt");
       
        return new LoggerConfiguration()
            .WriteTo.Debug(LogEventLevel.Debug, LogTemplate)
            .WriteTo.File(logPath, outputTemplate: LogTemplate)
            .MinimumLevel.Debug()
            .CreateLogger();
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        var exception = (Exception)args.ExceptionObject;
        var logger = Host.GetService<ILogger>();
        logger.Fatal(exception, "Domain unhandled exception");
    }
}