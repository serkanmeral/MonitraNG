using Microsoft.OpenApi.Models;
using System.Reflection;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace MngKeeper.Api.Configuration
{
    public static class SwaggerConfiguration
    {
        public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services)
        {
            services.AddSwaggerGen(c =>
            {
                // Disable schema generation for problematic types
                c.IgnoreObsoleteActions();
                c.IgnoreObsoleteProperties();
                
                // Use a more robust schema ID strategy to avoid conflicts
                c.CustomSchemaIds(type => 
                {
                    var fullName = type.FullName ?? type.Name;
                    
                    // For DTOs, include the namespace to make them unique
                    if (type.Name.EndsWith("Dto"))
                    {
                        var namespaceParts = fullName.Split('.');
                        var relevantNamespace = string.Join("_", namespaceParts.Skip(Math.Max(0, namespaceParts.Length - 4)));
                        return $"{type.Name}_{relevantNamespace}";
                    }
                    
                    // For Response types, include the query/command name
                    if (type.Name.EndsWith("Response"))
                    {
                        var namespaceParts = fullName.Split('.');
                        var queryType = namespaceParts.FirstOrDefault(p => p.Contains("Query") || p.Contains("Command")) ?? "Response";
                        return $"{type.Name}_{queryType}";
                    }
                    
                    // For other types, use a hash
                    var hash = Math.Abs(fullName.GetHashCode());
                    return $"{type.Name}_{hash}";
                });
                
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "MngKeeper API",
                    Version = "v1",
                    Description = "MngKeeper - Multi-tenant Management System API",
                    Contact = new OpenApiContact
                    {
                        Name = "MngKeeper Team",
                        Email = "support@mngkeeper.com",
                        Url = new Uri("https://mngkeeper.com")
                    },
                    License = new OpenApiLicense
                    {
                        Name = "MIT License",
                        Url = new Uri("https://opensource.org/licenses/MIT")
                    }
                });

                // Add JWT Authentication
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });

                // Include XML comments
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (System.IO.File.Exists(xmlPath))
                {
                    c.IncludeXmlComments(xmlPath);
                }

                // Add operation filters
                c.OperationFilter<SwaggerDefaultValues>();
                c.OperationFilter<SwaggerAuthorizationFilter>();

                // Customize operation IDs
                c.CustomOperationIds(apiDesc =>
                {
                    return apiDesc.TryGetMethodInfo(out var methodInfo) ? methodInfo.Name : null;
                });

                // Add tags
                c.TagActionsBy(api =>
                {
                    if (api.GroupName != null)
                    {
                        return new[] { api.GroupName };
                    }

                    var controllerActionDescriptor = api.ActionDescriptor as Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor;
                    if (controllerActionDescriptor != null)
                    {
                        return new[] { controllerActionDescriptor.ControllerName };
                    }

                    throw new InvalidOperationException("Unable to determine tag for endpoint.");
                });

                c.DocInclusionPredicate((name, api) => true);
            });

            // services.AddSwaggerGenNewtonsoftSupport(); // Removed as not needed

            return services;
        }

        public static IApplicationBuilder UseSwaggerConfiguration(this IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseSwagger(c =>
            {
                c.RouteTemplate = "api-docs/{documentName}/swagger.json";
            });

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/api-docs/v1/swagger.json", "MngKeeper API V1");
                c.RoutePrefix = "api-docs";
                c.DocumentTitle = "MngKeeper API Documentation";
                c.DefaultModelsExpandDepth(2);
                c.DefaultModelExpandDepth(2);
                c.DisplayRequestDuration();
                c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
                c.EnableDeepLinking();
                c.EnableFilter();
                c.ShowExtensions();
                c.ShowCommonExtensions();
                c.InjectStylesheet("/swagger-ui/custom.css");
                c.InjectJavascript("/swagger-ui/custom.js");
            });

            return app;
        }
    }

    public class SwaggerDefaultValues : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var apiDescription = context.ApiDescription;

            // operation.Deprecated |= apiDescription.IsDeprecated(); // Removed as method doesn't exist

            // REF: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/1752#issue-663991077
            foreach (var responseType in context.ApiDescription.SupportedResponseTypes)
            {
                var responseKey = responseType.IsDefaultResponse ? "default" : responseType.StatusCode.ToString();
                var response = operation.Responses[responseKey];

                foreach (var contentType in response.Content.Keys)
                {
                    if (!responseType.ApiResponseFormats.Any(x => x.MediaType == contentType))
                    {
                        response.Content.Remove(contentType);
                    }
                }
            }
        }
    }

    public class SwaggerAuthorizationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var hasAuthorize = context.MethodInfo.DeclaringType?.GetCustomAttributes(true)
                .Union(context.MethodInfo.GetCustomAttributes(true))
                .OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
                .Any();

            if (hasAuthorize == true)
            {
                operation.Security = new List<OpenApiSecurityRequirement>
                {
                    new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                }
                            },
                            Array.Empty<string>()
                        }
                    }
                };
            }
        }
    }
}
