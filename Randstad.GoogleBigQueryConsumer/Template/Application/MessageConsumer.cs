using Microsoft.Extensions.Hosting;
using Randstad.GoogleBigQueryConsumer.Settings;
using Randstad.Logging;
using RandstadMessageExchange;
using ServiceDiscovery;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Randstad.ConsumerServiceTests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace Randstad.GoogleBigQueryConsumer.Template.Application
{
    internal class MessageConsumer : BackgroundService
    {
        private const int DelayBeforeKillInSeconds = 10;

        private readonly ILogger _logger;
        private readonly IServiceDiscoveryClient _serviceDiscoveryClient;
        private readonly IConsumerService _consumerService;
        private readonly string _serviceName;
        private readonly int _maxPollingIntervalInSeconds;
        private readonly int _pollingIntervalIncrementInSeconds;
        private readonly int _initialPollingIntervalInSeconds;
        private readonly IErrorHandler _errorHandler;
        private readonly IMessageProcessor _messageProcessor;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private int _delay;
        private bool _removeFromSd;
        private Guid _correlationId;
        private QueueMessage _nextMessage;

        public MessageConsumer(
            ILogger logger,
            IServiceDiscoveryClient serviceDiscoveryClient,
            IConsumerService consumerService,
            ApplicationSettings applicationSettings,
            IErrorHandler errorHandler,
            IHostApplicationLifetime hostApplicationLifetime,
            IMessageProcessor messageProcessor)
        {
            _logger = logger;
            _serviceDiscoveryClient = serviceDiscoveryClient;
            _consumerService = consumerService;
            _serviceName = applicationSettings.ServiceName;
            _maxPollingIntervalInSeconds = applicationSettings.MaxPollingIntervalInSeconds;
            _pollingIntervalIncrementInSeconds = applicationSettings.PollingIntervalIncrementInSeconds;
            _initialPollingIntervalInSeconds = applicationSettings.PollingIntervalInSeconds;
            _delay = _initialPollingIntervalInSeconds;
            _removeFromSd = true;
            _hostApplicationLifetime = hostApplicationLifetime;
            _errorHandler = errorHandler;
            _messageProcessor = messageProcessor;

            hostApplicationLifetime.ApplicationStopped.Register(() =>
            {
                var currentProcess = Process.GetCurrentProcess();
                var sb = new StringBuilder();
                sb.AppendLine($"Total Processor Time: {currentProcess.TotalProcessorTime}");
                sb.AppendLine($"User Processor Time: {currentProcess.UserProcessorTime}");
                sb.AppendLine($"Process started: {currentProcess.StartTime.ToShortDateString()} {currentProcess.StartTime.ToLongTimeString()}");
                _logger.Info($"Application stopped. Killing {currentProcess.ProcessName}.", _correlationId, null, null, null, null);
                _logger.Info($"{currentProcess.ProcessName} has {DelayBeforeKillInSeconds} seconds to live.", _correlationId, null, null, null, null);
                _logger.Info(sb.ToString(), _correlationId, null, null, null, null);
                Thread.Sleep(DelayBeforeKillInSeconds * 1000);
                currentProcess.Kill();
            });

            hostApplicationLifetime.ApplicationStarted.Register(() =>
            {
                _logger.Info("Application started.", _correlationId, null, null, null, null);
            });

            hostApplicationLifetime.ApplicationStopping.Register(() =>
            {
                _logger.Info("Application stopping.", _correlationId, null, null, null, null);
            });
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _correlationId = Guid.NewGuid();
            _logger.Debug($"Entering {nameof(StartAsync)}.", _correlationId, null, null, null, null);

            if (Debugger.IsAttached)
            {
                _logger.Info($"{_serviceName}: debugger attached: NOT registering with service discovery.", _correlationId, null, null, null, null);
            }
            else
            {
                _serviceDiscoveryClient.Register();
                _logger.Info($"{_serviceName}: registered with service discovery.", _correlationId, null, null, null, null);
            }

            _logger.Info($"{_serviceName} has started.", _correlationId, null, null, null, null);
            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => _logger.Info($"{_serviceName} has been cancelled.", _correlationId, null, null, null, null));

            await Task.Run(async () =>
              {
                  _logger.Debug($"Entering {nameof(ExecuteAsync)}.", _correlationId, null, null, null, null);

                  while (!cancellationToken.IsCancellationRequested)
                  {
                      try
                      {
                          await ProcessMessages(cancellationToken);
                          _logger.Debug($"{_serviceName}: waiting {_delay} seconds.", _correlationId, null, null, null,
                              null);
                          await Task.Delay(TimeSpan.FromSeconds(_delay), cancellationToken);
                          SetPollingInterval();
                          _errorHandler.ResetKnownErrorsCount();
                      }
                      catch (Exception ex)
                      {
                          if (_nextMessage != null)
                          {
                              _consumerService.RejectMessage(true);
                          }

                          if (_errorHandler.Handle(ex, _correlationId))
                          {
                              continue;
                          }

                          _removeFromSd = false;
                          _logger.Fatal($"{_serviceName}: {ex.Message}", _correlationId, null, null, null, null, ex);
                          await StopAsync(cancellationToken);
                      }
                  }

                  _hostApplicationLifetime.StopApplication();

              }, cancellationToken);
        }

        /// <summary>
        /// Calls injected IMessageProcessor.Process(_nextMessage) to perform business logic
        /// </summary>
        private async Task ProcessMessages(CancellationToken cancellationToken)
        {
            _logger.Debug($"Entering {nameof(ProcessMessages)}.", _correlationId, null, null, null, null);

            do
            {
                _nextMessage = _consumerService.GetMessage();

                if (_nextMessage == null)
                {
                    _logger.Debug($"{_serviceName}: no message available.", _correlationId, null, null, null, null);
                    continue;
                }

                _correlationId = _nextMessage.CorrelationId;
                _delay = _initialPollingIntervalInSeconds;
                var queueMessageAction = await _messageProcessor.Process(_nextMessage);

                switch (queueMessageAction)
                {
                    case QueueMessageAction.Acknowledge:
                        _consumerService.AcknowledgeMessage();
                        break;

                    case QueueMessageAction.RejectAndRequeue:
                        _consumerService.RejectMessage(true);
                        break;

                    case QueueMessageAction.RejectAndDiscard:
                        _consumerService.RejectMessage(false);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(queueMessageAction), $"Unknown {nameof(QueueMessageAction)} in {nameof(ProcessMessages)}");
                }
            } while (_nextMessage != null && !cancellationToken.IsCancellationRequested);
        }

        private void SetPollingInterval()
        {
            if (_delay < _maxPollingIntervalInSeconds)
            {
                _delay = Math.Min(_delay + _pollingIntervalIncrementInSeconds, _maxPollingIntervalInSeconds);
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            if (_removeFromSd)
            {
                _serviceDiscoveryClient.Remove();
                _logger.Info($"{_serviceName} removed from Service Discovery.", _correlationId, null, null, null, null);
            }

            return base.StopAsync(cancellationToken);
        }
    }
}
