Financial Analytics Processor

Overview

The Financial Analytics Processor is a robust solution designed to process financial transactions efficiently. This project leverages .NET 8, Entity Framework Core, Hangfire, and other modern tools to ensure scalability, reliability, and maintainability.

Prerequisites

To run the project locally, ensure the following:

Branch:

Checkout the develop branch before running the application.

git checkout develop

SQL Server:

A local instance of SQL Server must be installed and running on your machine.

.NET 8 SDK:

Ensure .NET 8 SDK is installed on your machine.

dotnet --version

Running the Application Locally

Clone the repository:

git clone <repository-url>
cd FinancialAnalyticsProcessor

Checkout the develop branch:

git checkout develop

Update the appsettings.json file in the root of the project to include your SQL Server connection string:

"ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=FinancialAnalyticsDB;Trusted_Connection=True;"
}

Apply migrations:

Add-Migration InitialMigration -StartupProject FinancialAnalyticsProcessor -Project FinancialAnalyticsProcessor.Infrastructure
Update-Database -StartupProject FinancialAnalyticsProcessor -Project FinancialAnalyticsProcessor.Infrastructure

Run the application:

dotnet run --project FinancialAnalyticsProcessor

Access the Hangfire Dashboard:

Ensure the application is running.

Navigate to the Hangfire dashboard at: http://localhost:5000/hangfire.

Useful EF Core Commands

Add a new migration:

Add-Migration MigrationName -StartupProject FinancialAnalyticsProcessor -Project FinancialAnalyticsProcessor.Infrastructure

Apply migrations to the database:

Update-Database -StartupProject FinancialAnalyticsProcessor -Project FinancialAnalyticsProcessor.Infrastructure

Additional Notes

Ensure your local machine meets all prerequisites before running the application.

For troubleshooting or further assistance, refer to the project documentation or contact the development team.

