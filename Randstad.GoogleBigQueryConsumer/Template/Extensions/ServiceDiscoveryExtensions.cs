using Randstad.Logging.Core;

namespace Randstad.GoogleBigQueryConsumer.Template.Extensions
{
    public static class ServiceDiscoveryExtensions
    {
        public static RabbitMQSettings ForLogger(this ServiceDiscovery.Models.RabbitMQSettings sdRabbitMqSettings)
        {
            return new RabbitMQSettings
            {
                Hostname = sdRabbitMqSettings.Hostname,
                ExchangeName = sdRabbitMqSettings.ExchangeName,
                Password = sdRabbitMqSettings.Password,
                Port = sdRabbitMqSettings.Port,
                Username = sdRabbitMqSettings.Username
            };
        }
    }
}
