using AutoMapper;
using FinancialAnalyticsProcessor.Application.Services;
using FinancialAnalyticsProcessor.Configurations;
using FinancialAnalyticsProcessor.Domain.Interfaces;
using FinancialAnalyticsProcessor.Domain.Interfaces.ApplicationServices;
using FinancialAnalyticsProcessor.Domain.Interfaces.Repositories;
using FinancialAnalyticsProcessor.Domain.Validations;
using FinancialAnalyticsProcessor.FaultResiliencePolicies;
using FinancialAnalyticsProcessor.Infrastructure.Data;
using FinancialAnalyticsProcessor.Infrastructure.Repositories.Generic;
using FinancialAnalyticsProcessor.Infrastructure.Services;
using FinancialAnalyticsProcessor.Mappings;
using FinancialAnalyticsProcessor.Worker.Jobs;
using FluentValidation;
using FluentValidation.AspNetCore;
using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Serilog;
using Serilog.Events;

//TO EXECUTE THE JOB DISCOMENT
var builder = Host.CreateDefaultBuilder(args)
    .UseSerilog((context, services, configuration) =>
    {
        // Configures Serilog to log to the console
        configuration
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console();
    })
    .ConfigureServices((context, services) =>
    {
        // DbContext Configuration
        services.AddDbContext<TransactionDbContext>(options =>
        {
            options.UseSqlServer(context.Configuration.GetConnectionString("DefaultConnection"));
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        // FluentValidation Configuration
        services.AddValidatorsFromAssemblyContaining<TransactionValidator>();
        services.AddValidatorsFromAssemblyContaining<TransactionListValidator>();
        services.AddFluentValidationAutoValidation();
        services.AddFluentValidationClientsideAdapters();

        // AutoMapper Configuration
        var mappings = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile(new TransactionMappingProfile());
        });

        mappings.AssertConfigurationIsValid();
        var mapper = mappings.CreateMapper();

        services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
        services.AddSingleton(mapper);
        services.AddSingleton(mappings);

        // Add configuration for the Cron Job
        var jobConfig = context.Configuration.GetSection("JobSchedule").Get<JobScheduleConfig>();
        services.AddSingleton(jobConfig);

        var hangfireConnectionString = context.Configuration.GetConnectionString("HangfireConnection");

        // Ensure the Hangfire database exists
        EnsureDatabaseExists(hangfireConnectionString);

        // Hangfire Configuration
        services.AddHangfire(config =>
            config.UseSqlServerStorage(hangfireConnectionString));
        services.AddHangfireServer();

        // Service Registration
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<ITransactionProcessor, TransactionProcessor>();
        services.AddScoped<ICsvTransactionLoader, CsvTransactionLoader>();
        services.AddScoped<TransactionJob>();

        services.AddSingleton<AsyncRetryPolicy>(provider =>
        {
            // Create a new scope for scoped services
            using var scope = provider.CreateScope();
            var scopedProvider = scope.ServiceProvider;

            var csvTransactionLoader = scopedProvider.GetRequiredService<ICsvTransactionLoader>();
            var logger = provider.GetRequiredService<ILogger<TransactionJob>>(); // Logger can still be resolved as singleton

            return PollyPolicy.CreateRetryPolicy(csvTransactionLoader, logger);
        });

    })
    .ConfigureWebHostDefaults(webBuilder =>
    {
        webBuilder.Configure(app =>
        {
            // Retrieves Cron Job Schedule
            var jobConfig = app.ApplicationServices.GetService<JobScheduleConfig>();

            // Configures Hangfire Dashboard
            app.UseHangfireDashboard("/hangfire");

            // Fallback to a default cron expression if the interval is invalid
            // Use a expressão cron do arquivo de configuração
            var cronExpression = jobConfig?.CronExpression ?? "*/3 * * * *"; 

            // Schedule the job
            RecurringJob.AddOrUpdate<TransactionJob>(
                "process-transactions",
                job => job.ExecuteAsync(jobConfig.InputFilePath, jobConfig.OutputFilePath),
                cronExpression
            );
        });
    });

await builder.Build().RunAsync();

void EnsureDatabaseExists(string connectionString)
{
    try
    {
        // Parse the connection string to extract the database name
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;

        // Set the InitialCatalog to "master" to connect to the server itself
        builder.InitialCatalog = "master";

        using var connection = new SqlConnection(builder.ConnectionString);
        connection.Open();

        // Check if the database exists
        var commandText = $"IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'{databaseName}') CREATE DATABASE [{databaseName}]";
        using var command = new SqlCommand(commandText, connection);
        command.ExecuteNonQuery();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred while ensuring the Hangfire database exists: {ex.Message}");
        throw;
    }
}

//TO TEST LOCALLY DISCOMENT
//var builder = Host.CreateDefaultBuilder(args)
//    .UseSerilog((context, services, configuration) =>
//    {
//        // Configures Serilog to log to the console
//        configuration
//            .MinimumLevel.Debug()
//            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
//            .Enrich.FromLogContext()
//            .WriteTo.Console();
//    })
//    .ConfigureServices((context, services) =>
//    {

//        services.AddDbContext<TransactionDbContext>(options =>
//        {
//            options.UseSqlServer(context.Configuration.GetConnectionString("DefaultConnection")); 
//            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking); 
//        });
       

//        // FluentValidation Configuration
//        services.AddValidatorsFromAssemblyContaining<TransactionValidator>();
//        services.AddValidatorsFromAssemblyContaining<TransactionListValidator>();
//        services.AddFluentValidationAutoValidation();
//        services.AddFluentValidationClientsideAdapters();

//        // AutoMapper Configuration
//        var mappings = new MapperConfiguration(cfg =>
//        {
//            cfg.AddProfile(new TransactionMappingProfile());
//        });

//        mappings.AssertConfigurationIsValid();
//        var mapper = mappings.CreateMapper();

//        services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
//        services.AddSingleton(mapper);
//        services.AddSingleton(mappings);

//        // Add configuration for the Job
//        var jobConfig = context.Configuration.GetSection("JobSchedule").Get<JobScheduleConfig>();
//        services.AddSingleton(jobConfig);

//        // Service Registration
//        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
//        services.AddScoped<ITransactionProcessor, TransactionProcessor>();
//        services.AddScoped<ICsvTransactionLoader, CsvTransactionLoader>();
//        services.AddScoped<TransactionJob>();

//        // Add Polly retry policy
//        services.AddSingleton<AsyncRetryPolicy>(provider =>
//        {
//            using var scope = provider.CreateScope();
//            var scopedProvider = scope.ServiceProvider;

//            var csvTransactionLoader = scopedProvider.GetRequiredService<ICsvTransactionLoader>();
//            var logger = provider.GetRequiredService<ILogger<TransactionJob>>(); // Logger can still be resolved as singleton

//            return PollyPolicy.CreateRetryPolicy(csvTransactionLoader, logger);
//        });
//    });

//var host = builder.Build();

//using (var scope = host.Services.CreateScope())
//{
//    var serviceProvider = scope.ServiceProvider;

//    // Retrieve job configuration
//    var jobConfig = serviceProvider.GetRequiredService<JobScheduleConfig>();
//    var job = serviceProvider.GetRequiredService<TransactionJob>();

//    // Log and execute the job
//    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
//    logger.LogInformation("Executing the job for testing...");

//    try
//    {
//        await job.ExecuteAsync(jobConfig.InputFilePath, jobConfig.OutputFilePath);
//        logger.LogInformation("Job executed successfully.");
//    }
//    catch (Exception ex)
//    {
//        logger.LogError(ex, "An error occurred while executing the job.");
//    }
//}

//await host.RunAsync();
