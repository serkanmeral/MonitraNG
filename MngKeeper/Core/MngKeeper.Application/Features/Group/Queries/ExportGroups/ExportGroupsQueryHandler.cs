using MediatR;
using MngKeeper.Application.Interfaces;
using MngKeeper.Application.Common.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Text;

namespace MngKeeper.Application.Features.Group.Queries.ExportGroups
{
    public class ExportGroupsQueryHandler : IRequestHandler<ExportGroupsQuery, ExportGroupsResponse>
    {
        private readonly IGroupRepository _groupRepository;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<ExportGroupsQueryHandler> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ExportGroupsQueryHandler(
            IGroupRepository groupRepository,
            IUserRepository userRepository,
            ILogger<ExportGroupsQueryHandler> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _groupRepository = groupRepository;
            _userRepository = userRepository;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ExportGroupsResponse> Handle(ExportGroupsQuery request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Exporting groups in format: {Format}", request.Format);

                // Get domain from token claims
                var claims = _httpContextAccessor.HttpContext?.Items["TokenClaims"] as TokenClaims;
                
                if (claims?.DomainId == null)
                {
                    return new ExportGroupsResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain information not found in token."
                    };
                }

                // Get all groups (no pagination) with filters
                var groups = await _groupRepository.GetAllByDomainIdAsync(
                    claims.DomainId,
                    request.SearchTerm,
                    request.IsActive);

                // Calculate member count for each group
                var groupDtos = new List<GroupExportDto>();
                foreach (var g in groups)
                {
                    var usersInGroup = await _userRepository.GetByGroupIdAsync(g.Id, claims.DomainId);
                    var memberCount = usersInGroup.Count();
                    
                    groupDtos.Add(new GroupExportDto
                    {
                        GrupAdi = g.Name,
                        Aciklama = g.Description ?? string.Empty,
                        KisiSayisi = memberCount,
                        Durum = g.IsActive ? "Aktif" : "Pasif",
                        OlusturulmaTarihi = g.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                        GuncellenmeTarihi = g.UpdatedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty
                    });
                }

                // Generate file based on format
                byte[] fileContent;
                string contentType;
                string fileName;
                string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HHmmss");

                if (request.Format.ToLower() == "csv")
                {
                    fileContent = GenerateCsv(groupDtos);
                    contentType = "text/csv";
                    fileName = $"gruplar_{timestamp}.csv";
                }
                else
                {
                    return new ExportGroupsResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Unsupported export format: {request.Format}"
                    };
                }

                _logger.LogInformation("Groups exported successfully: {Count} groups in {Format} format", groupDtos.Count, request.Format);

                return new ExportGroupsResponse
                {
                    FileContent = fileContent,
                    ContentType = contentType,
                    FileName = fileName,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting groups");
                return new ExportGroupsResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Export failed: {ex.Message}"
                };
            }
        }

        private byte[] GenerateCsv(List<GroupExportDto> groups)
        {
            using var memoryStream = new MemoryStream();
            using var writer = new StreamWriter(memoryStream, Encoding.UTF8);
            using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";",
                Encoding = Encoding.UTF8
            });

            // Write header
            csv.WriteField("Grup Adı");
            csv.WriteField("Açıklama");
            csv.WriteField("Kişi Sayısı");
            csv.WriteField("Durum");
            csv.WriteField("Oluşturulma Tarihi");
            csv.WriteField("Güncellenme Tarihi");
            csv.NextRecord();

            // Write data
            foreach (var group in groups)
            {
                csv.WriteField(group.GrupAdi);
                csv.WriteField(group.Aciklama);
                csv.WriteField(group.KisiSayisi);
                csv.WriteField(group.Durum);
                csv.WriteField(group.OlusturulmaTarihi);
                csv.WriteField(group.GuncellenmeTarihi);
                csv.NextRecord();
            }

            writer.Flush();
            return memoryStream.ToArray();
        }
    }

    public class GroupExportDto
    {
        public string GrupAdi { get; set; } = string.Empty;
        public string Aciklama { get; set; } = string.Empty;
        public int KisiSayisi { get; set; }
        public string Durum { get; set; } = string.Empty;
        public string OlusturulmaTarihi { get; set; } = string.Empty;
        public string GuncellenmeTarihi { get; set; } = string.Empty;
    }
}

