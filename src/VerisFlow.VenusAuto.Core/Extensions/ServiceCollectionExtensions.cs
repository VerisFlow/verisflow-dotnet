// Copyright (c) VerisFlow. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VerisFlow.VenusAuto.Core.Contracts;
using VerisFlow.VenusAuto.Core.Internal;
using VerisFlow.VenusAuto.Core.Models;
using VerisFlow.VenusAuto.Core.Services;

namespace VerisFlow.VenusAuto.Core.Extensions;

/// <summary>
/// Provides Dependency Injection extension methods for registering Venus Automation core services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Venus Auto services and binds <see cref="VenusAutoOptions"/> from the "VenusAutomation" section.
    /// </summary>
    public static IServiceCollection AddVenusAutomation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<VenusAutoOptions>()
            .Bind(configuration.GetSection(VenusAutoOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddTransient<IWindowMessenger, WindowMessenger>();
        services.AddTransient<IWindowOrchestrator, WindowOrchestrator>();
        services.AddTransient<ISilentSimulator, SilentSimulator>();
        services.AddTransient<IUiaDialogDriver, UiaDialogDriver>();
        services.AddTransient<IDialogGuard, DialogGuard>();
        services.AddScoped<IVenusRunControlService, VenusRunControlService>();

        return services;
    }
}
