using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VerisFlow.VenusAuto.Core.Extensions;

namespace VerisFlow.VenusAuto.Sample
{
    /// <summary>
    /// Interaction logic for App.xaml, handling application lifecycle and Dependency Injection container setup.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Gets the global host instance managing Dependency Injection, configuration, and hosted services.
        /// </summary>
        public static IHost? AppHost { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class and configures the generic host builder.
        /// </summary>
        public App()
        {
            AppHost = Host.CreateDefaultBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    // Reuse the registration logic prepared for the Agent
                    services.AddVenusAutomation(hostContext.Configuration);

                    // Register the main window and ViewModel
                    services.AddTransient<MainWindow>();
                    services.AddTransient<ViewModels.MainViewModel>();
                })
                .Build();
        }

        /// <summary>
        /// Handles the application startup event by starting the generic host and displaying the main window.
        /// </summary>
        /// <param name="e">A <see cref="StartupEventArgs"/> containing event data.</param>
        protected override async void OnStartup(StartupEventArgs e)
        {
            await AppHost!.StartAsync();

            // Resolve and show the main window from the DI container
            var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        /// <summary>
        /// Handles the application exit event by stopping and disposing the generic host.
        /// </summary>
        /// <param name="e">An <see cref="ExitEventArgs"/> containing event data.</param>
        protected override async void OnExit(ExitEventArgs e)
        {
            await AppHost!.StopAsync();
            AppHost.Dispose();
            base.OnExit(e);
        }
    }
}