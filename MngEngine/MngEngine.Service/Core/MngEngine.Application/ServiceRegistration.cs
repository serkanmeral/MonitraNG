using Microsoft.Extensions.DependencyInjection;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MngEngine.Application
{
    public static class ServiceRegistration
    {
        public static void AddApplicationServices(this IServiceCollection collection)
        {
            //collection.Configure<MngReactorSettings>(_ =>
            //{
            //    _.SeqPath = settings.SeqPath;
            //    _.MongoPath = settings.MongoPath;
            //    _.ClientName = settings.ClientName;
            //    _.TokenService = settings.TokenService;
            //    _.Password = settings.Password;
            //    _.OpenLdapSettings = settings.OpenLdapSettings;
            //});

            collection.AddMemoryCache(options =>
            {
                //options.SizeLimit = 1024; // Cache boyutunu sınırlayın (MB)
                //options.ExpirationScanFrequency = TimeSpan.FromMinutes(5); // Taramanın ne kadar sıklıkla yapılacağını belirleyin
            });

            collection.AddMediatR(config =>
            {
                config.RegisterServicesFromAssemblies(AppDomain.CurrentDomain.GetAssemblies());
                //config.AddBehavior<LoggingBehavior>();
            });
        }
    }
}