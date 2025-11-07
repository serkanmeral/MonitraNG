using System;
using System.Collections.Generic;

namespace MngDataGateway.Application.DTOs.Common
{
    /// <summary>
    /// Generic response wrapper for data operations
    /// </summary>
    public class DataResponseDto<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public ResponseMetaDto Meta { get; set; } = new();
        public List<WarningDto>? Warnings { get; set; }
    }

    /// <summary>
    /// Response metadata
    /// </summary>
    public class ResponseMetaDto
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? Path { get; set; }
    }

    /// <summary>
    /// Warning information (e.g., notification failures)
    /// </summary>
    public class WarningDto
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Error response
    /// </summary>
    public class ErrorResponseDto
    {
        public bool Success { get; set; } = false;
        public ErrorDetailDto Error { get; set; } = new();
        public ResponseMetaDto Meta { get; set; } = new();
    }

    /// <summary>
    /// Error details
    /// </summary>
    public class ErrorDetailDto
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public object? Details { get; set; }
    }
}

