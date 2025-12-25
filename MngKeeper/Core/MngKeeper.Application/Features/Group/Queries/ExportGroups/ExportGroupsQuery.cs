using MediatR;

namespace MngKeeper.Application.Features.Group.Queries.ExportGroups
{
    public class ExportGroupsQuery : IRequest<ExportGroupsResponse>
    {
        public string Format { get; set; } = "csv"; // "csv" | "xlsx"
        public string? SearchTerm { get; set; }
        public bool? IsActive { get; set; }
    }

    public class ExportGroupsResponse
    {
        public byte[] FileContent { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }
}

