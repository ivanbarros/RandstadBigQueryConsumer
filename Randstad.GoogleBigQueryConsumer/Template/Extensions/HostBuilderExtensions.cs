using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;

namespace Randstad.GoogleBigQueryConsumer.Template.Extensions
{
    public static class HostBuilderExtensions
    {
        public static IHostBuilder ConfigureTemplate(this IHostBuilder host)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            host.ConfigureAppConfiguration((hostContext, config) =>
            {
                var env = hostContext.HostingEnvironment.EnvironmentName;
                var basePath = $"Template{Path.DirectorySeparatorChar}";
                config.AddJsonFile($"{basePath}template.json", optional: false, reloadOnChange: false);
                config.AddJsonFile($"{basePath}template.{env}.json", optional: true, reloadOnChange: false);
            });

            return host;
        }
    }
}
