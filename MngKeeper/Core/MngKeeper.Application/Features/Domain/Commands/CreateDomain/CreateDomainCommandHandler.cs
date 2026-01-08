using MediatR;
using Microsoft.Extensions.Logging;
using MngKeeper.Application.Pipelines.DomainCreation;

namespace MngKeeper.Application.Features.Domain.Commands.CreateDomain
{
    public class CreateDomainCommandHandler : IRequestHandler<CreateDomainCommand, CreateDomainResponse>
    {
        private readonly DomainCreationPipeline _pipeline;
        private readonly ILogger<CreateDomainCommandHandler> _logger;

        public CreateDomainCommandHandler(
            DomainCreationPipeline pipeline,
            ILogger<CreateDomainCommandHandler> logger)
        {
            _pipeline = pipeline;
            _logger = logger;
        }

        public async Task<CreateDomainResponse> Handle(CreateDomainCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Creating domain via pipeline: {DomainName}", request.DomainName);
            
            // Create pipeline context from command
            var context = new DomainCreationContext
            {
                DomainName = request.DomainName,
                DisplayName = request.DisplayName,
                AdminEmail = request.AdminEmail,
                AdminPassword = request.AdminPassword,
                Settings = request.Settings,
                RelatedPersonPhone = request.RelatedPersonPhone,
                Logo = request.Logo,
                LogoUrl = request.LogoUrl
            };
            
            // Execute pipeline
            var pipelineResult = await _pipeline.ExecuteAsync(context, cancellationToken);
            
            // Build response
            if (pipelineResult.IsSuccess)
            {
                _logger.LogInformation("Domain created successfully via pipeline: {DomainName}", request.DomainName);
                
                return new CreateDomainResponse
                {
                    DomainId = context.Domain!.Id,
                    DomainName = context.Domain.Name,
                    DatabaseName = context.DatabaseName,
                    AdminUsername = $"{context.DomainName}_admin",
                    AdminEmail = context.AdminEmail,
                    CreatedAt = context.Domain.CreatedAt,
                    IsSuccess = true,
                    Message = $"Domain '{context.DomainName}' created successfully with {pipelineResult.StepResults.Count} steps"
                };
            }
            else
            {
                _logger.LogError(
                    "Domain creation failed at step '{FailedStep}': {ErrorMessage}",
                    pipelineResult.FailedStepName,
                    pipelineResult.ErrorMessage);
                
                return new CreateDomainResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Failed at step '{pipelineResult.FailedStepName}': {pipelineResult.ErrorMessage}",
                    FailedStep = pipelineResult.FailedStepName
                };
            }
        }
    }
}
