using MediatR;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MngKeeper.Application.Features.Domain.Commands.CreateDomain
{
    public class CreateDomainCommandHandler : IRequestHandler<CreateDomainCommand, CreateDomainResponse>
    {
        private readonly IDomainRepository _domainRepository;
        private readonly IKeycloakService _keycloakService;
        private readonly ILogger<CreateDomainCommandHandler> _logger;

        public CreateDomainCommandHandler(
            IDomainRepository domainRepository,
            IKeycloakService keycloakService,
            ILogger<CreateDomainCommandHandler> logger)
        {
            _domainRepository = domainRepository;
            _keycloakService = keycloakService;
            _logger = logger;
        }

        public async Task<CreateDomainResponse> Handle(CreateDomainCommand request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Creating domain: {DomainName}", request.DomainName);

                // Check if domain already exists
                if (await _domainRepository.ExistsByNameAsync(request.DomainName))
                {
                    return new CreateDomainResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Domain with name '{request.DomainName}' already exists."
                    };
                }

                // Create database name
                var databaseName = $"mng_{request.DomainName.ToLower().Replace(" ", "_")}";
                var realmName = request.DomainName.ToLower().Replace(" ", "_");

                // Create domain entity
                var domain = new MngKeeper.Domain.Entities.Domain
                {
                    Name = request.DomainName,
                    DisplayName = request.DisplayName,
                    DatabaseName = databaseName,
                    RealmName = realmName,
                    Status = MngKeeper.Domain.Entities.DomainStatus.Pending,
                    Settings = new MngKeeper.Domain.Entities.DomainSettings
                    {
                        MaxUsers = request.Settings.MaxUsers,
                        MaxAssets = request.Settings.MaxAssets,
                        EnableMqtt = request.Settings.EnableMqtt,
                        MqttSettings = new MngKeeper.Domain.Entities.MqttSettings
                        {
                            BrokerHost = request.Settings.MqttSettings.BrokerHost,
                            BrokerPort = request.Settings.MqttSettings.BrokerPort,
                            Username = request.Settings.MqttSettings.Username,
                            Password = request.Settings.MqttSettings.Password,
                            TopicPrefix = request.Settings.MqttSettings.TopicPrefix
                        },
                        CustomSettings = request.Settings.CustomSettings
                    },
                    CreatedBy = "system", // TODO: Get from current user context
                    CreatedAt = DateTime.UtcNow
                };

                // Save domain to database
                var savedDomain = await _domainRepository.AddAsync(domain);

                // Create database
                await _domainRepository.CreateDatabaseAsync(databaseName);

                // Create Keycloak realm
                var realmInfo = await _keycloakService.CreateRealmAsync(realmName, request.Settings);

                // Create default groups
                var adminsGroup = await _keycloakService.CreateGroupAsync(realmName, new CreateGroupRequest
                {
                    Name = "admins",
                    Description = "Administrators group"
                });

                var managersGroup = await _keycloakService.CreateGroupAsync(realmName, new CreateGroupRequest
                {
                    Name = "managers",
                    Description = "Managers group"
                });

                var guestsGroup = await _keycloakService.CreateGroupAsync(realmName, new CreateGroupRequest
                {
                    Name = "guests",
                    Description = "Guests group"
                });

                // Create admin user
                var adminUserRequest = new CreateUserRequest
                {
                    Username = $"{request.DomainName}_admin",
                    Email = request.AdminEmail,
                    Password = request.AdminPassword,
                    FirstName = "Admin",
                    LastName = request.DomainName,
                    Groups = new List<string> { "admins" }
                };
                var userInfo = await _keycloakService.CreateUserAsync(realmName, adminUserRequest);

                // Add admin user to admins group
                await _keycloakService.AddUserToGroupAsync(realmName, userInfo.Id, "admins");

                // Update domain status to active
                savedDomain.Status = MngKeeper.Domain.Entities.DomainStatus.Active;
                savedDomain.UpdatedAt = DateTime.UtcNow;
                savedDomain.UpdatedBy = "system";

                await _domainRepository.UpdateAsync(savedDomain);

                _logger.LogInformation("Domain created successfully: {DomainName}", request.DomainName);

                return new CreateDomainResponse
                {
                    DomainId = savedDomain.Id,
                    DomainName = savedDomain.Name,
                    DatabaseName = savedDomain.DatabaseName,
                    AdminUsername = $"admin@{request.DomainName}",
                    AdminEmail = request.AdminEmail,
                    CreatedAt = savedDomain.CreatedAt,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating domain: {DomainName}", request.DomainName);
                return new CreateDomainResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Failed to create domain: {ex.Message}"
                };
            }
        }
    }
}
