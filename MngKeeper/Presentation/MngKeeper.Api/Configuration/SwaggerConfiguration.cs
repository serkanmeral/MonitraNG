using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace MngKeeper.Api.Configuration
{
    public static class SwaggerConfiguration
    {
        public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services, string? openApiServerPath = null)
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

                // Scalar/Swagger "Try it out": önce mevcut host (5001 doğrudan erişim)
                c.AddServer(new OpenApiServer
                {
                    Url = "/",
                    Description = "Current host (direct — use on :5001 Scalar)"
                });

                if (!string.IsNullOrEmpty(openApiServerPath))
                {
                    var baseUrl = openApiServerPath.TrimEnd('/');
                    c.AddServer(new OpenApiServer
                    {
                        Url = baseUrl,
                        Description = "Configured API base"
                    });

                    // Odak: OPENAPI_SERVER_PATH genelde gateway kökü (:5040); Keeper yolları /keeper altında
                    if (baseUrl.Contains(":5040", StringComparison.OrdinalIgnoreCase)
                        && !baseUrl.EndsWith("/keeper", StringComparison.OrdinalIgnoreCase))
                    {
                        c.AddServer(new OpenApiServer
                        {
                            Url = $"{baseUrl}/keeper",
                            Description = "API Gateway — MngKeeper (/keeper)"
                        });
                    }
                }

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
                c.OperationFilter<SwaggerFileUploadOperationFilter>();

                // Customize operation IDs
                c.CustomOperationIds(apiDesc =>
                {
                    return apiDesc.TryGetMethodInfo(out var methodInfo) ? methodInfo.Name : null;
                });

                // Yalnızca MVC controller endpoint'leri (Scalar/redirect/GraphQL minimal API hariç)
                c.DocInclusionPredicate((_, api) =>
                    api.ActionDescriptor is ControllerActionDescriptor);

                c.TagActionsBy(api =>
                {
                    if (api.GroupName != null)
                        return new[] { api.GroupName };

                    if (api.ActionDescriptor is ControllerActionDescriptor cad)
                        return new[] { cad.ControllerName.Replace("Controller", "", StringComparison.Ordinal) };

                    return new[] { "Other" };
                });
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

    /// <summary>
    /// multipart/form-data + IFormFile — Swashbuckle doc üretim hatasını önler (Scalar/Swagger UI).
    /// </summary>
    public class SwaggerFileUploadOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var formParams = context.ApiDescription.ParameterDescriptions
                .Where(p =>
                    p.ModelMetadata?.ModelType == typeof(IFormFile)
                    || p.ModelMetadata?.ModelType == typeof(IFormFile[])
                    || (p.Source.Id == "Form" && p.ModelMetadata?.ModelType != null
                        && p.ModelMetadata.ModelType.GetProperties()
                            .Any(prop => prop.PropertyType == typeof(IFormFile) || prop.PropertyType == typeof(IFormFile[]))))
                .ToList();

            if (formParams.Count == 0)
                return;

            var properties = new Dictionary<string, OpenApiSchema>();
            var required = new HashSet<string>();

            foreach (var param in formParams)
            {
                if (param.ModelMetadata?.ModelType == typeof(IFormFile)
                    || param.ModelMetadata?.ModelType == typeof(IFormFile[]))
                {
                    properties[param.Name] = new OpenApiSchema { Type = "string", Format = "binary" };
                    required.Add(param.Name);
                    continue;
                }

                foreach (var prop in param.ModelMetadata!.ModelType.GetProperties())
                {
                    if (prop.PropertyType == typeof(IFormFile) || prop.PropertyType == typeof(IFormFile[]))
                    {
                        properties[prop.Name] = new OpenApiSchema { Type = "string", Format = "binary" };
                        required.Add(prop.Name);
                    }
                    else
                    {
                        properties[prop.Name] = context.SchemaGenerator.GenerateSchema(
                            prop.PropertyType, context.SchemaRepository);
                    }
                }
            }

            operation.RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = properties,
                            Required = required
                        }
                    }
                }
            };

            var formParamNames = formParams.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            operation.Parameters = operation.Parameters
                .Where(p => !formParamNames.Contains(p.Name))
                .ToList();
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
