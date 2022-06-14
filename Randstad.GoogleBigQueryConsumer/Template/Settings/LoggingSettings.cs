using Randstad.Logging;
using Randstad.OperatingCompanies;

namespace Randstad.GoogleBigQueryConsumer.Template.Settings
{
    internal class LoggingSettings
    {
        public LogLevel LogLevel { get; set; }
        public int? RetentionPeriodInDaysDebug { get; set; }
        public int? RetentionPeriodInDaysError { get; set; }
        public int? RetentionPeriodInDaysFatal { get; set; }
        public int? RetentionPeriodInDaysInfo { get; set; }
        public int? RetentionPeriodInDaysSuccess { get; set; }
        public int? RetentionPeriodInDaysWarn { get; set; }
        public OperatingCompany OperatingCompany { get; set; }
    }
}
