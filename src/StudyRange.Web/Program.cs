using StudyRange.Web.Components;
using MudBlazor.Services;
using StudyRange.Application;
using StudyRange.Infrastructure;
using Microsoft.Extensions.Options;
using System.Text.Json;
using StudyRange.Infrastructure.Integrations;
using StudyRange.Infrastructure.Persistence;
using StudyRange.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();
await app.InitializeInfrastructureAsync();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapGet("/health", async (IServiceProvider serviceProvider) =>
{
    var llmOptions = serviceProvider.GetRequiredService<IOptions<LlmOptions>>().Value;
    var storageOptions = serviceProvider.GetRequiredService<IOptions<StorageOptions>>().Value;
    var persistenceOptions = serviceProvider.GetRequiredService<IOptions<PersistenceOptions>>().Value;

    bool storageWritable;
    string? storageError = null;
    try
    {
        Directory.CreateDirectory(storageOptions.RootDirectory);
        var probePath = Path.Combine(storageOptions.RootDirectory, $".healthcheck-{Guid.NewGuid():N}.tmp");
        await File.WriteAllTextAsync(probePath, "ok");
        File.Delete(probePath);
        storageWritable = true;
    }
    catch (Exception ex)
    {
        storageWritable = false;
        storageError = ex.Message;
    }

    bool databaseOk = true;
    string databaseMessage;
    if (string.Equals(persistenceOptions.Provider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
    {
        var connectionFactory = serviceProvider.GetService<PostgreSqlConnectionFactory>();
        if (connectionFactory is null)
        {
            databaseOk = false;
            databaseMessage = "PostgreSQL connection factory is not registered.";
        }
        else
        {
            try
            {
                await using var connection = connectionFactory.Create();
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                _ = await command.ExecuteScalarAsync();
                databaseMessage = "PostgreSQL connectivity is healthy.";
            }
            catch (Exception ex)
            {
                databaseOk = false;
                databaseMessage = ex.Message;
            }
        }
    }
    else
    {
        databaseMessage = "InMemory persistence is enabled.";
    }

    var llmConfigured = !string.IsNullOrWhiteSpace(llmOptions.ApiKey) && !string.IsNullOrWhiteSpace(llmOptions.Model);
    var ok = storageWritable && databaseOk && llmConfigured;
    var response = new
    {
        status = ok ? "Healthy" : "Degraded",
        entries = new
        {
            configuration = new
            {
                llmProvider = llmOptions.Provider,
                llmModel = llmOptions.Model,
                llmConfigured
            },
            storage = new
            {
                rootDirectory = storageOptions.RootDirectory,
                writable = storageWritable,
                error = storageError
            },
            database = new
            {
                provider = persistenceOptions.Provider,
                ok = databaseOk,
                message = databaseMessage
            }
        }
    };

    return Results.Json(response, statusCode: ok ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();

app.Run();
