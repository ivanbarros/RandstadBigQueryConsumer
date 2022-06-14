using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Randstad.GoogleBigQueryConsumer.Settings;
using Randstad.GoogleBigQueryConsumer.Template.Application;
using Randstad.GoogleBigQueryConsumer.Template.Extensions;
using Randstad.GoogleBigQueryConsumer.Template.Settings;
using Randstad.Logging;
using RandstadMessageExchange;
using ServiceDiscovery;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Randstad.GoogleBigQueryConsumer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureTemplate()
                .UseWindowsService()
                .ConfigureServices((hostContext, services) =>
                {
                    // extending default shutdown timeout on cancellation token from 5 to 10 seconds
                    services.Configure<HostOptions>(option => { option.ShutdownTimeout = TimeSpan.FromSeconds(10); });

                    // get configurations
                    services.Configure<ApplicationSettings>(hostContext.Configuration.GetSection(Constants.ApplicationConfigHeader));
                    services.Configure<LoggingSettings>(hostContext.Configuration.GetSection(Constants.LoggingConfigHeader));
                    services.Configure<ServiceDiscoverySettings>(hostContext.Configuration.GetSection(Constants.ServiceDiscoveryConfigHeader));
                    services.Configure<ConsumerServiceSettings>(hostContext.Configuration.GetSection(Constants.ConsumerServiceConfigHeader));
                    services.Configure<MonitoringSettings>(hostContext.Configuration.GetSection(Constants.MonitoringConfigHeader));

                    // configuration IoC
                    services.AddSingleton(_ => _.GetRequiredService<IOptions<ApplicationSettings>>().Value);
                    services.AddSingleton(_ => _.GetRequiredService<IOptions<ServiceDiscoverySettings>>().Value);
                    services.AddSingleton(_ => _.GetRequiredService<IOptions<ConsumerServiceSettings>>().Value);
                    services.AddSingleton(_ => _.GetRequiredService<IOptions<LoggingSettings>>().Value);
                    services.AddSingleton(_ => _.GetRequiredService<IOptions<MonitoringSettings>>().Value);

                    // services IoC
                    var applicationSettings = new ApplicationSettings();
                    var customSettings = new CustomSettings();
                    var loggingSettings = new LoggingSettings();
                    var serviceDiscoverySettings = new ServiceDiscoverySettings();
                    var consumerServiceSettings = new ConsumerServiceSettings();
                    var monitoringSettings = new MonitoringSettings();
                    hostContext.Configuration.GetSection(Constants.ApplicationConfigHeader).Bind(applicationSettings);
                    hostContext.Configuration.GetSection(Constants.CustomConfigHeader).Bind(customSettings);
                    hostContext.Configuration.GetSection(Constants.ServiceDiscoveryConfigHeader).Bind(serviceDiscoverySettings);
                    hostContext.Configuration.GetSection(Constants.ConsumerServiceConfigHeader).Bind(consumerServiceSettings);
                    hostContext.Configuration.GetSection(Constants.LoggingConfigHeader).Bind(loggingSettings);
                    hostContext.Configuration.GetSection(Constants.MonitoringConfigHeader).Bind(monitoringSettings);
                    var serviceDetailsSettings = serviceDiscoverySettings.ServiceDetails;
                    var sdClient = new ServiceDiscoveryClient(new List<string> { serviceDiscoverySettings.BaseUrl },
                        serviceDiscoverySettings.ServiceDetails.Name,
                        serviceDiscoverySettings.ServiceDetails.HostServer,
                        applicationSettings.Environment,
                        RequiredConfigurationGroupNames: null,
                        serviceDetailsSettings.IsApi,
                        serviceDetailsSettings.BaseUrl,
                        serviceDetailsSettings.AllowMonitorRestart);
                    var rabbitSettingsForLogger = sdClient.GetConfigurationGroup_RabbitMQSettings().ForLogger();

                    var retentionPolicy = new RetentionPolicy
                    {
                        RetentionPeriodInDaysDebug = loggingSettings.RetentionPeriodInDaysDebug,
                        RetentionPeriodInDaysError = loggingSettings.RetentionPeriodInDaysError,
                        RetentionPeriodInDaysFatal = loggingSettings.RetentionPeriodInDaysFatal,
                        RetentionPeriodInDaysInfo = loggingSettings.RetentionPeriodInDaysInfo,
                        RetentionPeriodInDaysSuccess = loggingSettings.RetentionPeriodInDaysSuccess,
                        RetentionPeriodInDaysWarn = loggingSettings.RetentionPeriodInDaysWarn
                    };
                    var assemblyName = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName();
                    var systemVersion = $"{assemblyName.Version}";
                    var logger = Logger.BuildLogger(
                        loggingSettings.LogLevel,
                        applicationSettings.Environment,
                        applicationSettings.ServiceName,
                        systemVersion,
                        loggingSettings.OperatingCompany,
                        rabbitSettingsForLogger,
                        retentionPolicy);

                    // services IoC
                    services.AddHostedService<MessageConsumer>();
                    services.AddSingleton<ILogger>(_ => logger);
                    services.AddSingleton<IServiceDiscoveryClient>(_ => sdClient);
                    var consumerRabbitSettings = sdClient.GetConfigurationGroup_RabbitMQ();
                    var consumerService = new RandstadMessageExchange.ConsumerService(
                        consumerRabbitSettings,
                        consumerServiceSettings.QueueName,
                        consumerServiceSettings.Bindings);
                    consumerService.CreateQueueAndBindings();
                    services.AddSingleton<IConsumerService>(_ => consumerService);

                    using (var monitorConsumerService = new RandstadMessageExchange.ConsumerService(
                        consumerRabbitSettings,
                        monitoringSettings.QueueName,
                        new List<string> { monitoringSettings.RoutingKey }))
                    {
                        monitorConsumerService.CreateQueueAndBindings();
                    }

                    var producerSettings = new Dictionary<string, string>
                    {
                        { "Host", consumerRabbitSettings["Host"] },
                        { "Username", consumerRabbitSettings["Username"] },
                        { "Password", consumerRabbitSettings["Password"] },
                        { "ExchangeName", consumerRabbitSettings["ExchangeName"] },
                        { "Port", consumerRabbitSettings["Port"] }
                    };
                    services.AddSingleton<IProducerService>(_ => new ProducerService(producerSettings));
                    services.AddSingleton<IErrorHandler, ErrorHandler>();
                    services.AddSingleton<IMessageProcessor, MessageProcessor>();
                    logger.Info("ConfigureServices done.", Guid.NewGuid(), null, null, null, null);
                });
    }
}
