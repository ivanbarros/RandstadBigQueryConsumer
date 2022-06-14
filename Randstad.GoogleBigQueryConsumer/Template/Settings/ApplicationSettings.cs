using Randstad.Environments;

// ReSharper disable once CheckNamespace
namespace Randstad.GoogleBigQueryConsumer.Settings
{
    internal partial class ApplicationSettings
    {
        public DeploymentEnvironment Environment { get; set; }
        public string ServiceName { get; set; }
        public int MaxPollingIntervalInSeconds { get; set; }
        public int PollingIntervalInSeconds { get; set; }
        public int PollingIntervalIncrementInSeconds { get; set; }
        public int MaxKnownErrorsCount { get; set; }
    }
}
