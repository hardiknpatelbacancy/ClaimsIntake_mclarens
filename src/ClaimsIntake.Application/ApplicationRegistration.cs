using ClaimsIntake.Application.Common.Behaviours;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsIntake.Application;

public static class ApplicationRegistration
{
    /// <summary>
    /// Registers all Application services by assembly scan: FluentValidation validators,
    /// MediatR handlers, and the <see cref="ValidationBehaviour{TRequest,TResponse}"/> pipeline.
    /// </summary>
    public static IServiceCollection RegisterApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationRegistration).Assembly, includeInternalTypes: true);

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(typeof(ApplicationRegistration).Assembly);
            configuration.AddOpenBehavior(typeof(ValidationBehaviour<,>));
        });

        return services;
    }
}
