using Microsoft.Extensions.DependencyInjection;
using System;
using TraceLogic.Core.Interfaces;
using TraceLogic.Core.IO;
using TraceLogic.Core.Parsing;
using TraceLogic.Core.Exporting;
using TraceLogic.Core.Options;

namespace TraceLogic.Core
{
    /// <summary>
    /// Extension methods for setting up TraceLogic services in an IServiceCollection.
    /// </summary>
    public static class TraceLogicServiceCollectionExtensions
    {
        /// <summary>
        /// Adds TraceLogic core services to the specified IServiceCollection.
        /// </summary>
        /// <param name="services">The IServiceCollection to add services to.</param>
        /// <param name="configureOptions">An action to configure the TraceLocatorOptions.</param>
        /// <returns>The IServiceCollection so that additional calls can be chained.</returns>
        public static IServiceCollection AddTraceLogic(this IServiceCollection services, Action<TraceLocatorOptions>? configureOptions = null)
        {
            if (configureOptions != null)
            {
                services.Configure(configureOptions);
            }
            else
            {
                services.Configure<TraceLocatorOptions>(options => { });
            }

            services.AddTransient<ITraceLocator, TraceLocator>();
            services.AddTransient<ITraceFileParser, TraceFileParser>();
            services.AddTransient<ITraceDataExporter, CsvDataExporter>();

            return services;
        }
    }
}