using Microsoft.Extensions.Logging;

namespace WalleeCharging.ManualTests;

public class ConsoleLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        throw new NotImplementedException();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        throw new NotImplementedException();
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Console.WriteLine(formatter(state,exception));
        if (exception != null)
        {
            Console.WriteLine(exception.Message);
            Console.WriteLine(exception.StackTrace);
        }
    }
}
