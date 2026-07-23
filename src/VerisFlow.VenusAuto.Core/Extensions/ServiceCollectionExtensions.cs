// Copyright (c) VerisFlow. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VerisFlow.VenusAuto.Core.Contracts;
using VerisFlow.VenusAuto.Core.Internal;
using VerisFlow.VenusAuto.Core.Services;
using VerisFlow.VenusAuto.Core.Models;

namespace VerisFlow.VenusAuto.Core.Extensions;

/// <summary>
/// Provides Dependency Injection extension methods for registering Venus Automation core services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Venus Auto services and configures the required coordinate options.
    /// </summary>
    /// <param name="services">The service collection container to register services into.</param>
    /// <param name="configuration">The root configuration provider containing options sections.</param>
    /// <returns>The updated <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    public static IServiceCollection AddVenusAutomation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<VenusAutoOptions>()
            .Bind(configuration.GetSection(VenusAutoOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddTransient<IWindowOrchestrator, WindowOrchestrator>();
        services.AddTransient<ISilentSimulator, SilentSimulator>();
        services.AddScoped<IVenusRunControlService, VenusRunControlService>();
        services.AddTransient<IDialogGuard, DialogGuard>();

        return services;
    }
}