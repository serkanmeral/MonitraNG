using MediatR;

namespace MngKeeper.Application.Features.Auth.Commands.GetToken
{
    public class GetTokenCommand : IRequest<GetTokenResponse>
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? DomainName { get; set; }
    }

    public class GetTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public string RefreshToken { get; set; } = string.Empty;
        public int RefreshExpiresIn { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime RefreshExpiresAt { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
