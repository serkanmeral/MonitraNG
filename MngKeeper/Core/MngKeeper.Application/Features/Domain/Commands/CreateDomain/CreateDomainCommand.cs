using MediatR;

namespace MngKeeper.Application.Features.Domain.Commands.CreateDomain
{
    public class CreateDomainCommand : IRequest<CreateDomainResponse>
    {
        public string DomainName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
        public string AdminPassword { get; set; } = string.Empty;
        public DomainSettingsDto Settings { get; set; } = new();
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
