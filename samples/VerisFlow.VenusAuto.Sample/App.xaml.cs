using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VerisFlow.VenusAuto.Core.Extensions;

namespace VerisFlow.VenusAuto.Sample
{
    /// <summary>
    /// Application lifecycle and service container setup.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// The host managing configuration and services.
        /// </summary>
        public static IHost? AppHost { get; private set; }

        public App()
        {
            AppHost = Host.CreateDefaultBuilder()
                // appsettings.json next to the executable; it is reloaded on change, so IDs can be edited while running.
                .UseContentRoot(AppContext.BaseDirectory)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddVenusAutomation(hostContext.Configuration);

                    services.AddTransient<MainWindow>();
                    services.AddTransient<ViewModels.MainViewModel>();
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            await AppHost!.StartAsync();

            var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            await AppHost!.StopAsync();
            AppHost.Dispose();
            base.OnExit(e);
        }
    }
}