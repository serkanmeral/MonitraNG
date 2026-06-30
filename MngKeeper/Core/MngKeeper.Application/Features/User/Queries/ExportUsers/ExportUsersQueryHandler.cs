using MediatR;
using MngKeeper.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MngKeeper.Application.Features.User.Queries.ExportUsers
{
    public class ExportUsersQueryHandler : IRequestHandler<ExportUsersQuery, ExportUsersResponse>
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<ExportUsersQueryHandler> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ExportUsersQueryHandler(
            IUserRepository userRepository,
            ILogger<ExportUsersQueryHandler> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _userRepository = userRepository;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ExportUsersResponse> Handle(ExportUsersQuery request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Exporting users in format: {Format}", request.Format);

                // Get domain from token claims
                var claims = _httpContextAccessor.HttpContext?.Items["TokenClaims"] as TokenClaims;
                
                if (claims?.DomainId == null)
                {
                    return new ExportUsersResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain information not found in token."
                    };
                }

                // Get all users (no pagination) with filters and sorting
                var users = await _userRepository.GetAllByDomainIdAsync(
                    claims.DomainId,
                    request.SearchTerm,
                    request.IsActive,
                    includeInApplication: null,
                    request.SortBy,
                    request.SortOrder);

                // Convert to export DTOs
                var userDtos = users.Select(u => new UserExportDto
                {
                    KullaniciAdi = u.Username ?? string.Empty,
                    Email = u.Email ?? string.Empty,
                    Ad = u.FirstName ?? string.Empty,
                    Soyad = u.LastName ?? string.Empty,
                    Unvan = u.Title ?? string.Empty,
                    Departman = u.Department ?? string.Empty,
                    Telefon = u.PhoneNumber ?? string.Empty,
                    Durum = u.IsActive ? "Aktif" : "Pasif",
                    Gruplar = u.Groups != null && u.Groups.Any() ? string.Join("; ", u.Groups) : string.Empty,
                    OlusturulmaTarihi = u.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    GuncellenmeTarihi = u.UpdatedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty
                }).ToList();

                // Generate file based on format
                byte[] fileContent;
                string contentType;
                string fileName;
                string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HHmmss");

                switch (request.Format.ToLower())
                {
                    case "csv":
                        fileContent = GenerateCsv(userDtos);
                        contentType = "text/csv;charset=utf-8";
                        fileName = $"kullanicilar_{timestamp}.csv";
                        break;
                    case "xlsx":
                        return new ExportUsersResponse
                        {
                            IsSuccess = false,
                            ErrorMessage = "XLSX export is not yet implemented. Please use CSV or JSON format."
                        };
                    case "json":
                        fileContent = GenerateJson(userDtos);
                        contentType = "application/json;charset=utf-8";
                        fileName = $"kullanicilar_{timestamp}.json";
                        break;
                    default:
                        return new ExportUsersResponse
                        {
                            IsSuccess = false,
                            ErrorMessage = $"Unsupported export format: {request.Format}. Supported formats: csv, json"
                        };
                }

                _logger.LogInformation("Users exported successfully: {Count} users in {Format} format", userDtos.Count, request.Format);

                return new ExportUsersResponse
                {
                    FileContent = fileContent,
                    ContentType = contentType,
                    FileName = fileName,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting users");
                return new ExportUsersResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Export failed: {ex.Message}"
                };
            }
        }

        private byte[] GenerateCsv(List<UserExportDto> users)
        {
            using var memoryStream = new MemoryStream();
            using var writer = new StreamWriter(memoryStream, new UTF8Encoding(false));
            
            // Write UTF-8 BOM for Excel compatibility
            var bom = new byte[] { 0xEF, 0xBB, 0xBF };
            memoryStream.Write(bom, 0, bom.Length);
            
            using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ",",
                Encoding = Encoding.UTF8
            });

            // Write header
            csv.WriteField("Kullanıcı Adı");
            csv.WriteField("Email");
            csv.WriteField("Ad");
            csv.WriteField("Soyad");
            csv.WriteField("Unvan");
            csv.WriteField("Departman");
            csv.WriteField("Telefon");
            csv.WriteField("Durum");
            csv.WriteField("Gruplar");
            csv.WriteField("Oluşturulma Tarihi");
            csv.WriteField("Güncellenme Tarihi");
            csv.NextRecord();

            // Write data
            foreach (var user in users)
            {
                csv.WriteField(user.KullaniciAdi);
                csv.WriteField(user.Email);
                csv.WriteField(user.Ad);
                csv.WriteField(user.Soyad);
                csv.WriteField(user.Unvan);
                csv.WriteField(user.Departman);
                csv.WriteField(user.Telefon);
                csv.WriteField(user.Durum);
                csv.WriteField(user.Gruplar);
                csv.WriteField(user.OlusturulmaTarihi);
                csv.WriteField(user.GuncellenmeTarihi);
                csv.NextRecord();
            }

            writer.Flush();
            return memoryStream.ToArray();
        }

        private byte[] GenerateJson(List<UserExportDto> users)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            
            var json = JsonSerializer.Serialize(users, options);
            return Encoding.UTF8.GetBytes(json);
        }
    }

    public class UserExportDto
    {
        public string KullaniciAdi { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public string Soyad { get; set; } = string.Empty;
        public string Unvan { get; set; } = string.Empty;
        public string Departman { get; set; } = string.Empty;
        public string Telefon { get; set; } = string.Empty;
        public string Durum { get; set; } = string.Empty;
        public string Gruplar { get; set; } = string.Empty;
        public string OlusturulmaTarihi { get; set; } = string.Empty;
        public string GuncellenmeTarihi { get; set; } = string.Empty;
    }
}
