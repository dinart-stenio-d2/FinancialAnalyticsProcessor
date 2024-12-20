using FinancialAnalyticsProcessor.Domain.Interfaces;
using FluentValidation;
using Polly;
using Polly.Retry;

namespace FinancialAnalyticsProcessor.FaultResiliencePolicies
{
    public static class PollyPolicy
    {

        public static AsyncRetryPolicy CreateRetryPolicy(ICsvTransactionLoader csvTransactionLoader, ILogger logger)
        {
            return Policy
                .Handle<ValidationException>() // Handles validation exceptions
                .Or<IOException>() // Handles I/O exceptions
                .Or<Exception>() // Handles other exceptions
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
                    onRetryAsync: async (exception, timeSpan, retryCount, context) =>
                    {
                        // Log the retry attempt and exception message
                        logger.LogWarning($"Retry {retryCount} encountered an error: {exception.Message}. Waiting {timeSpan} before next retry.");

                        // Retrieve parameters from the execution context
                        var csvFilePath = context["csvFilePath"] as string;
                        var newDescription = "New Description added after failure"; // PS: THIS IS JUST A TEST OF BUSINESS RULE REPROCESSING AT THE TIME OF FILE EXECUTION CONSIDER IT AS AN EXAMPLE

                        if (exception is ValidationException validationException)
                        {
                            foreach (var error in validationException.Errors)
                            {
                                // Check if the error message contains "Transaction ID: |"
                                if (error.ErrorMessage.Contains("Transaction ID: |"))
                                {
                                    // Extract the TransactionId from the error message
                                    var startIndex = error.ErrorMessage.IndexOf("|") + 1;
                                    var endIndex = error.ErrorMessage.LastIndexOf("|");
                                    var transactionIdString = error.ErrorMessage.Substring(startIndex, endIndex - startIndex);

                                    if (Guid.TryParse(transactionIdString, out var transactionId))
                                    {
                                        logger.LogWarning($"Validation failed for transaction ID: {transactionId}. Attempting to recreate...");

                                        // Call the method to recreate the transaction
                                        try
                                        {
                                            await csvTransactionLoader.RecreateTransactionAsync(csvFilePath, transactionId, newDescription);
                                            logger.LogInformation($"Transaction with ID {transactionId} was recreated successfully in the CSV file.");

                                            // Continue processing after successful recreation
                                            logger.LogInformation($"Continuing processing for transaction ID: {transactionId} after successful recreation.");
                                            return; // Exit the retry context and continue execution
                                        }
                                        catch (Exception ex)
                                        {
                                            logger.LogError(ex, $"Failed to recreate transaction during retry for ID {transactionId}.");
                                        }
                                    }
                                    else
                                    {
                                        logger.LogWarning("Invalid Transaction ID format in the validation error message.");
                                    }
                                }
                            }
                        }
                    }
                );
        }
    }
}
