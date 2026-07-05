using DocuMind.Infrastructure.Configuration;
using DocuMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Configuration
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
        .AddEnvironmentVariables();

    builder.Services.AddSingleton<IValidateOptions<PostgresOptions>, PostgresOptionsValidator>();
    builder.Services
        .AddOptions<PostgresOptions>()
        .Bind(builder.Configuration.GetSection(PostgresOptions.SectionName))
        .ValidateOnStart();
    builder.Services.AddDocuMindPersistence();

    using var host = builder.Build();
    var logger = host.Services.GetRequiredService<ILoggerFactory>()
        .CreateLogger("DocuMind.DbMigrator");

    try
    {
        await using var scope = host.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DocuMindDbContext>();

        logger.LogInformation("Applying EF Core migrations.");
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("EF Core migrations applied successfully.");

        return 0;
    }
    catch (Exception exception)
    {
        logger.LogCritical(exception, "Failed to apply EF Core migrations.");
        return 1;
    }
}
