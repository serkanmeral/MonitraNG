using Microsoft.Extensions.Logging;
using Moq;

namespace MngDataGateway.Tests.Helpers;

/// <summary>
/// Helper class for creating mock loggers in tests
/// </summary>
public static class LoggerMockHelper
{
    public static ILogger<T> CreateMockLogger<T>()
    {
        var mockLogger = new Mock<ILogger<T>>();

        // Setup the Log method to not throw
        mockLogger
            .Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()))
            .Callback(new InvocationAction(invocation =>
            {
                var logLevel = (LogLevel)invocation.Arguments[0];
                var eventId = (EventId)invocation.Arguments[1];
                var state = invocation.Arguments[2];
                var exception = (Exception)invocation.Arguments[3];
                var formatter = invocation.Arguments[4];

                var method = formatter.GetType().GetMethod("Invoke");
                var logMessage = method?.Invoke(formatter, new[] { state, exception }) as string;

                // For debugging, uncomment to see log output:
                // Console.WriteLine($"[{logLevel}] {logMessage}");
            }));

        return mockLogger.Object;
    }
}
