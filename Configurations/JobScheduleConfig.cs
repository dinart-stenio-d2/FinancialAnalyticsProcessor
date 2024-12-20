namespace FinancialAnalyticsProcessor.Configurations
{
    public class JobScheduleConfig
    {
        public string CronExpression { get; set; }
        public string InputFilePath { get; set; }
        public string OutputFilePath { get; set; }
    }
}
