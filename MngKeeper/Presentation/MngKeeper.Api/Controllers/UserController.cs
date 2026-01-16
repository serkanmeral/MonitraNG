using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using MediatR;
using MngKeeper.Application.Features.User.Commands.CreateUser;
using MngKeeper.Application.Features.User.Commands.UpdateUser;
using MngKeeper.Application.Features.User.Commands.DeleteUser;
using MngKeeper.Application.Features.User.Commands.AddUserToGroup;
using MngKeeper.Application.Features.User.Commands.RemoveUserFromGroup;
using MngKeeper.Application.Features.User.Queries.GetUser;
using MngKeeper.Application.Features.User.Queries.GetUsers;
using MngKeeper.Application.Features.User.Queries.ExportUsers;
using MngKeeper.Api.Attributes;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [ManagerAuthorization] // Allows both Admin and Manager users
    public class UserController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IMinioService _minioService;
        private readonly IDomainRepository _domainRepository;
        private readonly IUserRepository _userRepository;
        private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
        private readonly INotifierService _notifierService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserController> _logger;

        public UserController(
            IMediator mediator, 
            IMinioService minioService, 
            IDomainRepository domainRepository,
            IUserRepository userRepository,
            IPasswordResetTokenRepository passwordResetTokenRepository,
            INotifierService notifierService,
            IConfiguration configuration,
            ILogger<UserController> logger)
        {
            _mediator = mediator;
            _minioService = minioService;
            _domainRepository = domainRepository;
            _userRepository = userRepository;
            _passwordResetTokenRepository = passwordResetTokenRepository;
            _notifierService = notifierService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<CreateUserResponse>> CreateUser([FromBody] CreateUserCommand command)
        {
            var response = await _mediator.Send(command);
            
            if (!response.IsSuccess)
                return BadRequest(response);

            // Get domain from token claims
            var claims = HttpContext.Items["TokenClaims"] as TokenClaims;
            return CreatedAtAction(nameof(GetUser), new { userId = response.UserId }, response);
        }

        [HttpGet("{userId}")]
        public async Task<ActionResult<GetUserResponse>> GetUser(string userId)
        {
            var query = new GetUserQuery { UserId = userId };
            var response = await _mediator.Send(query);
            
            if (!response.IsSuccess)
                return NotFound(response);

            return Ok(response);
        }

        [HttpGet]
        public async Task<ActionResult<GetUsersResponse>> GetUsers(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 10,
            [FromQuery] string? searchTerm = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null)
        {
            var query = new GetUsersQuery 
            { 
                Page = page, 
                PageSize = pageSize,
                SearchTerm = searchTerm,
                IsActive = isActive,
                SortBy = sortBy,
                SortOrder = sortOrder
            };
            var response = await _mediator.Send(query);
            
            if (!response.IsSuccess)
                return BadRequest(response);

            return Ok(response);
        }

        [HttpPut("{userId}")]
        public async Task<ActionResult<UpdateUserResponse>> UpdateUser(string userId, [FromBody] UpdateUserCommand command)
        {
            command.UserId = userId;
            
            var response = await _mediator.Send(command);
            
            if (!response.IsSuccess)
                return BadRequest(response);

            return Ok(response);
        }

        [HttpDelete("{userId}")]
        public async Task<ActionResult<DeleteUserResponse>> DeleteUser(string userId)
        {
            var command = new DeleteUserCommand { UserId = userId };
            var response = await _mediator.Send(command);
            
            if (!response.IsSuccess)
                return BadRequest(response);

            return NoContent();
        }

        [HttpPost("{userId}/groups/{groupId}")]
        public async Task<ActionResult<AddUserToGroupResponse>> AddUserToGroup(string userId, string groupId)
        {
            // Get domain from token claims
            var claims = HttpContext.Items["TokenClaims"] as TokenClaims;
            
            if (claims?.DomainId == null)
            {
                return BadRequest(new AddUserToGroupResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Domain information not found in token."
                });
            }
            
            var command = new AddUserToGroupCommand 
            { 
                UserId = userId, 
                GroupId = groupId,
                DomainId = claims.DomainId
            };
            
            var response = await _mediator.Send(command);
            
            if (!response.IsSuccess)
                return BadRequest(response);

            return Ok(response);
        }

        [HttpDelete("{userId}/groups/{groupId}")]
        public async Task<ActionResult<RemoveUserFromGroupResponse>> RemoveUserFromGroup(string userId, string groupId)
        {
            // Get domain from token claims
            var claims = HttpContext.Items["TokenClaims"] as TokenClaims;
            
            if (claims?.DomainId == null)
            {
                return BadRequest(new RemoveUserFromGroupResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Domain information not found in token."
                });
            }
            
            var command = new RemoveUserFromGroupCommand 
            { 
                UserId = userId, 
                GroupId = groupId,
                DomainId = claims.DomainId
            };
            
            var response = await _mediator.Send(command);
            
            if (!response.IsSuccess)
                return BadRequest(response);

            return NoContent();
        }

        [HttpPost("{userId}/photo")]
        public async Task<ActionResult> UploadUserPhoto(string userId, [FromForm] IFormFile file)
        {
            try
            {
                // Get domain from token claims
                var claims = HttpContext.Items["TokenClaims"] as TokenClaims;
                
                if (claims?.DomainId == null || claims?.DomainName == null)
                {
                    return BadRequest(new { error = "Domain information not found in token." });
                }

                // Validate file
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { error = "No file provided." });
                }

                // Max size: 5MB
                const long maxSize = 5 * 1024 * 1024; // 5MB
                if (file.Length > maxSize)
                {
                    return BadRequest(new { error = "File size exceeds 5MB limit." });
                }

                // Allowed formats
                var allowedFormats = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
                if (!allowedFormats.Contains(file.ContentType.ToLower()))
                {
                    return BadRequest(new { error = "Invalid file format. Allowed formats: JPEG, PNG, WebP." });
                }

                // Get file extension
                var extension = System.IO.Path.GetExtension(file.FileName).ToLower();
                if (string.IsNullOrEmpty(extension))
                {
                    // Determine extension from content type
                    extension = file.ContentType.ToLower() switch
                    {
                        "image/jpeg" or "image/jpg" => ".jpg",
                        "image/png" => ".png",
                        "image/webp" => ".webp",
                        _ => ".jpg"
                    };
                }

                // Get domain to retrieve DatabaseName (which is the MinIO bucket name: mng_{domainName})
                var domain = await _domainRepository.GetByIdAsync(claims.DomainId);
                if (domain == null)
                {
                    return BadRequest(new { error = "Domain not found." });
                }

                // Convert DatabaseName to MinIO bucket name (replace underscore with hyphen)
                // DatabaseName format: mng_{domainName} (e.g., "mng_meral")
                // MinIO bucket name format: mng-{domainName} (e.g., "mng-meral")
                // MinIO bucket names cannot contain underscores, only lowercase letters, numbers, dots, and hyphens
                var bucketName = domain.DatabaseName.ToLower().Replace("_", "-"); // e.g., "mng_meral" -> "mng-meral"
                // Object path: data/users/{userId}/photo.{ext}
                var objectName = $"data/users/{userId}/photo{extension}";

                // Upload to MinIO
                using var stream = file.OpenReadStream();
                var success = await _minioService.PutObjectAsync(
                    bucketName,
                    objectName,
                    stream,
                    file.ContentType
                );

                if (!success)
                {
                    _logger.LogError("Failed to upload photo to MinIO for user {UserId}", userId);
                    return StatusCode(500, new { error = "Failed to upload photo." });
                }

                // Build photo URL (proxy URL format: /keeper/api/user/{userId}/photo)
                var photoUrl = $"/keeper/api/user/{userId}/photo";

                _logger.LogInformation("Photo uploaded successfully for user {UserId} to {ObjectName}", userId, objectName);

                return Ok(new { photoUrl, url = photoUrl, fileUrl = photoUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading photo for user {UserId}", userId);
                return StatusCode(500, new { error = "An error occurred while uploading the photo." });
            }
        }

        [HttpGet("{userId}/photo")]
        [HttpGet("{userId}/photo.{ext}")]
        public async Task<ActionResult> GetUserPhoto(string userId, string? ext = null)
        {
            try
            {
                // Get domain from token claims
                var claims = HttpContext.Items["TokenClaims"] as TokenClaims;
                
                if (claims?.DomainId == null || claims?.DomainName == null)
                {
                    return BadRequest(new { error = "Domain information not found in token." });
                }

                // Get domain to retrieve DatabaseName (which is the MinIO bucket name: mng_{domainName})
                var domain = await _domainRepository.GetByIdAsync(claims.DomainId);
                if (domain == null)
                {
                    return BadRequest(new { error = "Domain not found." });
                }

                // Convert DatabaseName to MinIO bucket name (replace underscore with hyphen)
                // DatabaseName format: mng_{domainName} (e.g., "mng_meral")
                // MinIO bucket name format: mng-{domainName} (e.g., "mng-meral")
                // MinIO bucket names cannot contain underscores, only lowercase letters, numbers, dots, and hyphens
                var bucketName = domain.DatabaseName.ToLower().Replace("_", "-"); // e.g., "mng_meral" -> "mng-meral"
                
                // If extension is provided in URL, try that first
                // Otherwise, try different extensions
                var extensions = new List<string>();
                if (!string.IsNullOrEmpty(ext))
                {
                    // Normalize extension (remove leading dot if present, add it if missing)
                    var normalizedExt = ext.StartsWith(".") ? ext : $".{ext}";
                    extensions.Add(normalizedExt);
                }
                
                // Add other common extensions to try
                extensions.AddRange(new[] { ".jpg", ".jpeg", ".png", ".webp" });
                
                Stream? photoStream = null;
                string? contentType = null;
                string? objectName = null;

                foreach (var extension in extensions.Distinct())
                {
                    // Object path: data/users/{userId}/photo.{ext}
                    objectName = $"data/users/{userId}/photo{extension}";
                    photoStream = await _minioService.GetObjectAsync(bucketName, objectName);
                    
                    if (photoStream != null)
                    {
                        contentType = extension switch
                        {
                            ".jpg" or ".jpeg" => "image/jpeg",
                            ".png" => "image/png",
                            ".webp" => "image/webp",
                            _ => "image/jpeg"
                        };
                        break;
                    }
                }

                if (photoStream == null)
                {
                    return NotFound(new { error = "Photo not found." });
                }

                return File(photoStream, contentType ?? "image/jpeg");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving photo for user {UserId}", userId);
                return StatusCode(500, new { error = "An error occurred while retrieving the photo." });
            }
        }

        [HttpDelete("{userId}/photo")]
        public async Task<ActionResult> DeleteUserPhoto(string userId)
        {
            try
            {
                // Get domain from token claims
                var claims = HttpContext.Items["TokenClaims"] as TokenClaims;
                
                if (claims?.DomainId == null || claims?.DomainName == null)
                {
                    return BadRequest(new { error = "Domain information not found in token." });
                }

                // Get domain to retrieve DatabaseName (which is the MinIO bucket name: mng_{domainName})
                var domain = await _domainRepository.GetByIdAsync(claims.DomainId);
                if (domain == null)
                {
                    return BadRequest(new { error = "Domain not found." });
                }

                // Convert DatabaseName to MinIO bucket name (replace underscore with hyphen)
                // DatabaseName format: mng_{domainName} (e.g., "mng_meral")
                // MinIO bucket name format: mng-{domainName} (e.g., "mng-meral")
                var bucketName = domain.DatabaseName.ToLower().Replace("_", "-");
                
                // Try to delete photo from MinIO (try different extensions)
                var extensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                bool deletedFromMinIO = false;
                
                foreach (var ext in extensions)
                {
                    var objectName = $"data/users/{userId}/photo{ext}";
                    var deleted = await _minioService.RemoveObjectAsync(bucketName, objectName);
                    if (deleted)
                    {
                        deletedFromMinIO = true;
                        _logger.LogInformation("Photo deleted from MinIO: {BucketName}/{ObjectName}", bucketName, objectName);
                        break; // Found and deleted, no need to try other extensions
                    }
                }
                
                if (!deletedFromMinIO)
                {
                    _logger.LogWarning("Photo not found in MinIO for user {UserId}, continuing with database update", userId);
                }
                
                // Update user's photoUrl to null in database
                var command = new UpdateUserCommand
                {
                    UserId = userId,
                    PhotoUrl = null
                };
                
                var response = await _mediator.Send(command);
                
                if (!response.IsSuccess)
                {
                    return BadRequest(response);
                }

                _logger.LogInformation("Photo removed successfully for user {UserId}", userId);

                return Ok(new { message = "Photo removed successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing photo for user {UserId}", userId);
                return StatusCode(500, new { error = "An error occurred while removing the photo." });
            }
        }

        /// <summary>
        /// Request password reset for a user (sends email with reset link)
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Success status</returns>
        [HttpPost("{userId}/request-password-reset")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RequestPasswordReset(string userId)
        {
            try
            {
                // Get domain from token claims
                var claims = HttpContext.Items["TokenClaims"] as TokenClaims;
                
                if (claims?.DomainId == null)
                {
                    return BadRequest(new { error = "invalid_request", errorDescription = "Domain information not found in token." });
                }

                _logger.LogInformation("Requesting password reset for user: {UserId} in domain: {DomainId}", userId, claims.DomainId);

                // Get user
                var user = await _userRepository.GetByIdAsync(userId, claims.DomainId);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId} in domain: {DomainId}", userId, claims.DomainId);
                    return NotFound(new { error = "user_not_found", errorDescription = "User not found." });
                }

                // Check if user has email
                if (string.IsNullOrWhiteSpace(user.Email))
                {
                    _logger.LogWarning("User {UserId} does not have an email address", userId);
                    return BadRequest(new { error = "invalid_request", errorDescription = "User does not have an email address." });
                }

                // Generate secure random token
                var tokenBytes = new byte[32];
                using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
                {
                    rng.GetBytes(tokenBytes);
                }
                // Convert to Base64Url (URL-safe base64)
                var base64Token = Convert.ToBase64String(tokenBytes);
                var token = base64Token.TrimEnd('=').Replace('+', '-').Replace('/', '_');

                // Create token entity (1 day expiration)
                var resetToken = new MngKeeper.Domain.Entities.PasswordResetToken
                {
                    Token = token,
                    UserId = user.Id,
                    DomainId = claims.DomainId,
                    ExpiresAt = DateTime.UtcNow.AddDays(1), // 1 day expiration
                    IsUsed = false,
                    CreatedAt = DateTime.UtcNow,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                };

                // Save token
                await _passwordResetTokenRepository.AddAsync(resetToken);

                _logger.LogInformation("Password reset token created for user: {UserId}, Token expires at: {ExpiresAt}", userId, resetToken.ExpiresAt);

                // Get UI base URL from configuration (default to localhost:3000 for development)
                var uiBaseUrl = _configuration["MngKeeperSettings:UiBaseUrl"] ?? "http://localhost:3000";
                var resetLink = $"{uiBaseUrl}/auth/reset-password?token={Uri.EscapeDataString(token)}";

                // Prepare email content
                var subject = "Şifre Sıfırlama Talebi";
                var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4CAF50; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #4CAF50; color: white; text-decoration: none; border-radius: 4px; margin: 20px 0; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Şifre Sıfırlama</h1>
        </div>
        <div class=""content"">
            <p>Merhaba {user.FirstName} {user.LastName},</p>
            <p>Hesabınız için şifre sıfırlama talebi alındı. Şifrenizi sıfırlamak için aşağıdaki bağlantıya tıklayın:</p>
            <p style=""text-align: center;"">
                <a href=""{resetLink}"" class=""button"">Şifremi Sıfırla</a>
            </p>
            <p>Bu bağlantı 24 saat geçerlidir.</p>
            <p>Eğer bu talebi siz yapmadıysanız, bu e-postayı görmezden gelebilirsiniz.</p>
            <p>Bağlantı çalışmıyorsa, aşağıdaki URL'yi tarayıcınıza kopyalayıp yapıştırabilirsiniz:</p>
            <p style=""word-break: break-all; font-size: 12px; color: #666;"">{resetLink}</p>
        </div>
        <div class=""footer"">
            <p>Bu e-posta otomatik olarak gönderilmiştir. Lütfen yanıtlamayın.</p>
        </div>
    </div>
</body>
</html>";

                // Send email via MngNotifier
                await _notifierService.SendEmailAsync(
                    to: new List<string> { user.Email },
                    subject: subject,
                    body: body,
                    isHtml: true,
                    cancellationToken: default);

                _logger.LogInformation("Password reset email sent successfully to user: {UserId}, Email: {Email}", userId, user.Email);

                return Ok(new { 
                    isSuccess = true, 
                    message = "Password reset email sent successfully.",
                    expiresAt = resetToken.ExpiresAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting password reset for user: {UserId}", userId);
                return StatusCode(500, new { 
                    error = "server_error", 
                    errorDescription = "An error occurred while processing your request." 
                });
            }
        }

        /// <summary>
        /// Export users to CSV, XLSX or JSON format
        /// </summary>
        [HttpGet("export")]
        public async Task<IActionResult> ExportUsers(
            [FromQuery] string format = "csv",
            [FromQuery] string? searchTerm = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null)
        {
            var query = new ExportUsersQuery
            {
                Format = format,
                SearchTerm = searchTerm,
                IsActive = isActive,
                SortBy = sortBy,
                SortOrder = sortOrder
            };
            
            var response = await _mediator.Send(query);
            
            if (!response.IsSuccess)
            {
                return BadRequest(new { message = response.ErrorMessage });
            }

            return File(response.FileContent, response.ContentType, response.FileName);
        }
    }
}
