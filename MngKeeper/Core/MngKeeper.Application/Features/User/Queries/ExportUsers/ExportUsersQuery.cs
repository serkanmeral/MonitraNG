using MediatR;

namespace MngKeeper.Application.Features.User.Queries.ExportUsers
{
    public class ExportUsersQuery : IRequest<ExportUsersResponse>
    {
        public string Format { get; set; } = "csv"; // "csv" | "xlsx" | "json"
        public string? SearchTerm { get; set; }
        public bool? IsActive { get; set; }
        public string? SortBy { get; set; }
        public string? SortOrder { get; set; }
    }

    public class ExportUsersResponse
    {
        public byte[] FileContent { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
