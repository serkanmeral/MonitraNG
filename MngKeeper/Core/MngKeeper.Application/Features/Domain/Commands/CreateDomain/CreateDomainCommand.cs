using System.Text.Json.Serialization;
using MediatR;

namespace MngKeeper.Application.Features.Domain.Commands.CreateDomain
{
    public class CreateDomainCommand : IRequest<CreateDomainResponse>
    {
        public string DomainName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? DiscoveryRootLabel { get; set; }
        public string AdminEmail { get; set; } = string.Empty;
        public string AdminPassword { get; set; } = string.Empty;
        public DomainSettingsDto Settings { get; set; } = new();
        public string? RelatedPersonPhone { get; set; }
        public string? RelatedPersonEmail { get; set; }
        public string? Logo { get; set; }
        public string? LogoUrl { get; set; }
        
        [JsonPropertyName("initialDataTemplateName")]
        public string? TemplateName { get; set; }  // Optional template name for initial data
    }

    public class DomainSettingsDto
    {
        public int MaxUsers { get; set; } = 100;
        public int MaxAssets { get; set; } = 1000;
        public bool EnableMqtt { get; set; } = true;
        public MqttSettingsDto MqttSettings { get; set; } = new();
        public Dictionary<string, object> CustomSettings { get; set; } = new();
    }

    public class MqttSettingsDto
    {
        public string BrokerHost { get; set; } = "mosquitto";
        public int BrokerPort { get; set; } = 1883;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string TopicPrefix { get; set; } = "MNG";
    }
}
