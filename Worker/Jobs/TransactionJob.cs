using FinancialAnalyticsProcessor.Domain.Interfaces;
using FinancialAnalyticsProcessor.Domain.Interfaces.ApplicationServices;
using Hangfire;
using Polly;
using Polly.Retry;

namespace FinancialAnalyticsProcessor.Worker.Jobs
{
    public class TransactionJob
    {
        private readonly ICsvTransactionLoader _loader;
        private readonly ITransactionProcessor _processor;
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly ILogger<TransactionJob> _logger;

        public TransactionJob(ICsvTransactionLoader loader, ITransactionProcessor processor, AsyncRetryPolicy retryPolicy, ILogger<TransactionJob> logger)
        {
            _loader = loader;
            _processor = processor;
            _retryPolicy = retryPolicy;
            _logger = logger;
        }

        [DisableConcurrentExecution(10 * 60)]// Maximum blocking time: 10 minutes
        public async Task ExecuteAsync(string csvFilePath, string outputPath)
        {
            _logger.LogInformation("Starting job execution for CSV file: {CsvFilePath}", csvFilePath);

            await _retryPolicy.ExecuteAsync(async context =>
            {
                try
                {
                    _logger.LogInformation("Opening CSV file: {CsvFilePath}", csvFilePath);
                    using var stream = new FileStream(csvFilePath, FileMode.Open, FileAccess.Read);

                    _logger.LogInformation("Loading transactions from CSV file.");
                    var transactions = await _loader.LoadTransactionsAsync(stream);
                    _logger.LogInformation("Loaded {TransactionCount} transactions.", transactions.Count());

                    _logger.LogInformation("Processing transactions.");
                    await _processor.ProcessTransactionsAsync(transactions).ConfigureAwait(false);
                    _logger.LogInformation("Transactions processed successfully.");

                    _logger.LogInformation("Performing analysis on transactions.");
                    var analysis = await _processor.PerformAnalysisAsync();
                    _logger.LogInformation("Analysis completed successfully.");

                    _logger.LogInformation("Saving analysis report to {OutputPath}", outputPath);
                    await _processor.SaveReportAsync(analysis, outputPath);
                    _logger.LogInformation("Report saved successfully to {OutputPath}", outputPath);

                    _logger.LogInformation("Deleting all transactions.");
                    await _processor.PurgeAllTransactionsAsync();
                    _logger.LogInformation("All transactions deleted successfully.");

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during job execution.");
                    throw; // Re-throw for Polly to handle the exception
                }
            },
            new Context
            {
            { "csvFilePath", csvFilePath }
            });

            _logger.LogInformation("Job execution completed for CSV file: {CsvFilePath}", csvFilePath);
        }
    }
}
