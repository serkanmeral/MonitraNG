using Serilog.Events;
using Serilog;
using Serilog.Core;
using System.Reflection;
using MngEngine.Api.Logging;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using System.Net;
using MngEngine.Api.Middlewares;

namespace MngEngine.Api.Extentions
{
    public static class WebBuilderExtentions
    {
        public static Serilog.Core.Logger InitSerilog(this WebApplicationBuilder builder)
        {
            var inMemorySink = new InMemoryLogSink(capacity: 1000);
            builder.Services.AddSingleton(inMemorySink);

            using var log = new LoggerConfiguration()
                            .Enrich.FromLogContext()
                            .WriteTo.Console()
                            .WriteTo.Sink(inMemorySink)
                            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                            .Enrich.WithProperty("AppName", "MngEngine.Api")
                            .CreateLogger(); //initialise the logger

            builder.Services.AddSingleton<Serilog.ILogger>(log);

            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(log);
            builder.Host.UseSerilog(log);

            return log;
        }

        public static void InitSwagger(this WebApplicationBuilder builder)
        {
            builder.Services.AddEndpointsApiExplorer();

            builder.Services.AddSwaggerGen(s =>
            {
                s.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "MngEngine API",
                    Version = "v1",
                    Description = "MngEngine API",
                    //TermsOfService = new Uri("https://example.com/terms"),
                    Contact = new OpenApiContact
                    {
                        Name = "Serkan MERAL",
                        Email = "serkan.meral@outlook.com",
                    }
                });
            });
        }

        public static void InitWebAPP(this WebApplicationBuilder builder)
        {
            var httpPort = builder.Configuration.GetValue("MngEngine:Server:Port", 5037);

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.AddServerHeader = false;
                options.ListenAnyIP(httpPort);
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
        }

        public static void UseApplicationSettings(this WebApplication app)
        {
            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseSwagger();
            app.UseSwaggerUI(s =>
            {
                s.SwaggerEndpoint("/swagger/v1/swagger.json", "MngEngine API");
            });

            app.UseCors("CorsPolicy");

            app.UseMiddleware<ErrorHandlingMiddleware>();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseExceptionHandler(
                options =>
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
                }
            );

            app.MapControllers();

            app.MapFallbackToFile("index.html");
        }
    }
}