using System;
using Microsoft.Extensions.Logging;
using Moq;

namespace MngReactor.Tests.Helpers;

/// <summary>
/// Helper for creating mock loggers in tests.
/// </summary>
public static class LoggerMockHelper
{
    public static ILogger<T> CreateMockLogger<T>()
    {
        var mockLogger = new Mock<ILogger<T>>();
        mockLogger
            .Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(_ => { }));
        return mockLogger.Object;
    }
}
