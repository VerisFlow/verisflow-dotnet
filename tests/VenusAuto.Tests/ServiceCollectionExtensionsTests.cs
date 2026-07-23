using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VerisFlow.VenusAuto.Core.Contracts;
using VerisFlow.VenusAuto.Core.Extensions;
using VerisFlow.VenusAuto.Core.Models;
using Xunit;

namespace VerisFlow.VenusAuto.Core.Tests;

/// <summary>
/// Contains unit tests for verifying service collection extensions and dependency injection configuration.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    /// <summary>
    /// Verifies that <see cref="ServiceCollectionExtensions.AddVenusAutomation"/> correctly binds configuration 
    /// options from the provider and registers all expected core services in the service container.
    /// </summary>
    [Fact]
    public void AddVenusAutomation_RegistersExpectedServices()
    {
        // Arrange: Create a service collection and construct in-memory configuration settings
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { $"{VenusAutoOptions.SectionName}:RunControlProcessName", "TestProcess" }
            })
            .Build();

        // Act: Register required logging infrastructure and Venus automation services into DI container
        services.AddLogging();
        services.AddVenusAutomation(configuration);

        var provider = services.BuildServiceProvider();

        // Assert: Validate options pattern binding for VenusAutoOptions
        var options = provider.GetRequiredService<IOptions<VenusAutoOptions>>().Value;
        Assert.Equal("TestProcess", options.RunControlProcessName);

        // Assert: Ensure IVenusRunControlService implementation is properly registered and resolvable
        var runControlService = provider.GetRequiredService<IVenusRunControlService>();
        Assert.NotNull(runControlService);
    }
}