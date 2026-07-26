using Microsoft.Extensions.DependencyInjection;
using StudyRange.Application.UseCases;

namespace StudyRange.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IStudyCoachService, StudyCoachService>();
        return services;
    }
}
