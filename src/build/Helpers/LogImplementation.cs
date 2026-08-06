using System;
using Microsoft.Extensions.Logging;
using ILogger = Serilog.ILogger;

namespace Helpers;

public class LogImplementation : Microsoft.Extensions.Logging.ILogger
{
    readonly ILogger Logger;
    readonly LogLevel MiniLogLevel;

    public LogImplementation(ILogger logger, LogLevel miniLogLevel = LogLevel.Debug)
    {
        Logger = logger;
        MiniLogLevel = miniLogLevel;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
        Func<TState, Exception, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        if (logLevel < MiniLogLevel) return;
        switch (logLevel)
        {
            case LogLevel.Trace:
            case LogLevel.Debug:
                Logger.Debug(message);
                break;
            case LogLevel.Information:
                Logger.Information(message);
                break;
            case LogLevel.Warning:
                Logger.Warning(message);
                break;
            case LogLevel.Error:
                Logger.Error(exception, message);
                break;
            case LogLevel.Critical:
                Logger.Fatal(exception, message);
                break;
            case LogLevel.None:
                break;
            default:
                Logger.Information(message);
                break;
        }
    }

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
}