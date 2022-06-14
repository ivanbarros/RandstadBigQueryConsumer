using Randstad.Environments;
using Randstad.GoogleBigQueryConsumer.Settings;
using Randstad.GoogleBigQueryConsumer.Template.Settings;
using Randstad.Logging;
using RandstadMessageExchange;
using ServiceDiscovery;
using System;
using System.Collections.Generic;
using System.Text;

namespace Randstad.GoogleBigQueryConsumer.Template.Application
{
    internal class ErrorHandler : IErrorHandler
    {
        private readonly ILogger _logger;
        private readonly IServiceDiscoveryClient _serviceDiscoveryClient;
        private readonly IProducerService _producerService;
        private readonly DeploymentEnvironment _environment;
        private readonly string _hostServer;
        private readonly string _serviceName;
        private readonly string _monitoringRoutingKey;
        private readonly int _maxKnownErrorsCount;

        public int KnownErrorsCount { get; private set; }


        public ErrorHandler(
            ILogger logger,
            IServiceDiscoveryClient serviceDiscoveryClient,
            IProducerService producerService,
            ApplicationSettings applicationSettings,
            ServiceDiscoverySettings serviceDiscoverySettings,
            MonitoringSettings monitoringSettings)
        {
            _logger = logger;
            _serviceDiscoveryClient = serviceDiscoveryClient;
            _producerService = producerService;
            _maxKnownErrorsCount = applicationSettings.MaxKnownErrorsCount;
            _monitoringRoutingKey = monitoringSettings.RoutingKey;
            _environment = applicationSettings.Environment;
            _serviceName = applicationSettings.ServiceName;
            _hostServer = serviceDiscoverySettings.ServiceDetails.HostServer;
            KnownErrorsCount = 0;
        }

        public void ResetKnownErrorsCount()
        {
            KnownErrorsCount = 0;
        }

        public bool Handle(Exception ex, Guid correlationId)
        {
            if (_serviceDiscoveryClient.ConfigurationGroup_IsKnownError(ex.Message))
            {
                ++KnownErrorsCount;
                _logger.Error($"Known Error: {ex.Message} (Total known errors: {KnownErrorsCount})", correlationId, null, null, null, null, ex);

                if (KnownErrorsCount < _maxKnownErrorsCount)
                {
                    return true;
                }

                var body = BuildIssueNotificationMessage(ex);
                var headers = new Dictionary<string, object>
                {
                    {"CorrelationId", correlationId.ToString("D")}
                };

                try
                {
                    _producerService.Publish(headers, correlationId, _monitoringRoutingKey, body);

                    if (KnownErrorsCount >= _maxKnownErrorsCount)
                    {
                        return false;
                    }

                    ResetKnownErrorsCount();
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            _logger.Error(ex.Message, correlationId, null, null, null, null, ex);
            return false;
        }

        private string BuildIssueNotificationMessage(Exception exception)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Issue Notification");
            sb.AppendLine($"Service Name: {_serviceName}");
            sb.AppendLine($"Host Server: {_hostServer}");
            sb.AppendLine($"Deployment Environment: {_environment}");
            sb.AppendLine($"Max Known Errors Count: {_maxKnownErrorsCount}");
            sb.AppendLine($"Known Errors Count: {KnownErrorsCount}");
            sb.AppendLine(exception.ToString());
            return sb.ToString();
        }
    }
}
