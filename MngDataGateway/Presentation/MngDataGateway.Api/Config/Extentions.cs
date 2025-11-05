using Microsoft.AspNetCore.Diagnostics;
using MngDataGateway.Application.Configuration;
using Scalar.AspNetCore;
using Serilog;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;

namespace MngDataGateway.Api.Config
{
    public static class Extentions
    {
        public static Serilog.Core.Logger InitSerilog(this WebApplicationBuilder builder, MngDataGatewaySettings mngDataGatewaySettings)
        {

            //        Log.Logger = new LoggerConfiguration()
            //.ReadFrom.Configuration(builder.Configuration)
            //.CreateLogger();

            using var log = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration) // önemli kısım!
                .CreateLogger();

            builder.Services.AddSingleton<Serilog.ILogger>(log);

            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(log);
            builder.Host.UseSerilog(log);

            log.Information($"DataGateway Starting. Version {Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion.Split('+')[0]} ");

            return log;
        }

        public static void InitOpenApi(this WebApplicationBuilder builder)
        {
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = "MngDataGateway API",
                    Version = "v1.0.0",
                    Description = "Dynamic Data Gateway for MongoDB with schema management",
                    Contact = new Microsoft.OpenApi.Models.OpenApiContact
                    {
                        Name = "iSIM Platform",
                        Email = "serkan.meral@isimplatform.io"
                    }
                });

                var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    c.IncludeXmlComments(xmlPath);
                }
            });
        }

    public static void InitWebAPP(this WebApplicationBuilder builder, X509Certificate2 certificate)
    {
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;

            options.ListenAnyIP(5010, _opt =>
            {
                _opt.UseHttps(httpsOptions =>
                {
                    httpsOptions.ServerCertificate = certificate;
                });
            });
        });

            builder.Services.AddControllers().AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.ReferenceHandler
                    = ReferenceHandler.IgnoreCycles;
                o.JsonSerializerOptions.MaxDepth = 64;
            });
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

            builder.Services.AddCors(l =>
            {
                l.AddPolicy("CorsPolicy", b =>
                    b.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                     .WithExposedHeaders("Content-Disposition")
                    );
            });


            // Add services to the container
            builder.Services.AddControllers();
        }

        public static void InitAuthentication(this WebApplicationBuilder builder, MngDataGatewaySettings settings)
        {
            builder.Services.AddAuthentication("Bearer")
                .AddJwtBearer("Bearer", options =>
                {
                    options.Authority = settings.Actors.MngKeeper;

                    options.RequireHttpsMetadata = false;

                    options.BackchannelHttpHandler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = delegate { return true; }
                    };


                    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                    {
                        ValidateAudience = false,
                        ValidateIssuer = false,
                        ValidateIssuerSigningKey = false,
                        SignatureValidator = delegate (string token, Microsoft.IdentityModel.Tokens.TokenValidationParameters parameters)
                        {
                            //var jwt = new JwtSecurityToken(token);

                            var jwt = new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(token);

                            return jwt;
                        }
                    };

                });
        }

        public static void UseApplicationSettings(this WebApplication app, MngDataGatewaySettings mngDataGatewaySettings)
        {
            // 1. Global hata yakalama en yukarıda olmalı
            app.UseExceptionHandler(options =>
            {
                options.Run(async context =>
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    context.Response.ContentType = "text/html";
                    var exceptionObject = context.Features.Get<IExceptionHandlerFeature>();
                    if (null != exceptionObject)
                    {
                        var errorMessage = $"{exceptionObject.Error.Message}";
                        await context.Response.WriteAsync(errorMessage).ConfigureAwait(false);
                    }
                });
            });

            // 2. HTTPS yönlendirmesi erkenden
            app.UseHttpsRedirection();

            // 3. Serilog ile request loglama
            app.UseSerilogRequestLogging(options =>
            {
                // options.RequestProjection = r => new { r.IsHttps, QueryString = r.QueryString.Value };
            });

            //if (app.Environment.IsDevelopment())
            {
                // Swagger with custom route
                app.UseSwagger(c =>
                {
                    c.RouteTemplate = "api-docs/{documentName}/swagger.json";
                });
                
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/api-docs/v1/swagger.json", "MngDataGateway API v1");
                    c.RoutePrefix = "swagger";
                    c.DocumentTitle = "MngDataGateway API Documentation";
                    c.DisplayRequestDuration();
                });
            }

            // 4. Routing middleware
            app.UseRouting();

            // 5. CORS
            app.UseCors("CorsPolicy");

            // 6. Authentication (kimlik doğrulama)
            app.UseAuthentication();

            // 7. Authorization (yetkilendirme)
            app.UseAuthorization();

            // 8. OpenAPI ve Scalar dökümantasyon endpointleri
            app.MapOpenApi();

            app.MapScalarApiReference(options =>
            {
                options
                    .WithTitle("MngDataGateway API")
                    .WithTheme(ScalarTheme.Purple)
                    .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
                    .WithOpenApiRoutePattern("/api-docs/{documentName}/swagger.json");

                options.AddServer(new ScalarServer(mngDataGatewaySettings.OpenApiServerPath, "MngDataGateway Server"));
            });

            // 9. Controller ve endpoint tanımlamaları (Map'ler en sonda)
            app.MapControllers().WithOpenApi();
        }
    }
}
