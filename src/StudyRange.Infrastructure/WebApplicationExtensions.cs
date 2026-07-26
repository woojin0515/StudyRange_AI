using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using StudyRange.Infrastructure.Persistence;

namespace StudyRange.Infrastructure;

public static class WebApplicationExtensions
{
    public static async Task InitializeInfrastructureAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        using var scope = app.Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<IInfrastructureInitializer>();
        await initializer.InitializeAsync(cancellationToken);
    }
}
