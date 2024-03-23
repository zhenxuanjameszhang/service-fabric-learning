using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceFabric.Services.Runtime;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Serilog;

namespace Stateful1
{
    internal static class Program
    {
        /// <summary>
        /// This is the entry point of the service host process.
        /// </summary>
        private static void Main()
        {
            Log.Logger = new LoggerConfiguration()
                            .WriteTo.File("C:\\Logs\\stateful1.log") // Specify the log file path
                            .CreateLogger();

            var serviceProvider = new ServiceCollection()
                .AddLogging(builder =>
                {
                    builder.AddSerilog(); // Use Serilog for logging
                })
                .BuildServiceProvider();

            var logger = serviceProvider.GetService<ILogger<Stateful1>>();
            logger.LogInformation("Service Fabric stateful1 service started.");

            try
            {
                // The ServiceManifest.XML file defines one or more service type names.
                // Registering a service maps a service type name to a .NET type.
                // When Service Fabric creates an instance of this service type,
                // an instance of the class is created in this host process.

                ServiceRuntime.RegisterServiceAsync("Stateful1Type",
                    context => new Stateful1(context, logger)).GetAwaiter().GetResult();

                ServiceEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof(Stateful1).Name);

                // Prevents this host process from terminating so services keep running.
                Thread.Sleep(Timeout.Infinite);
            }
            catch (Exception e)
            {
                ServiceEventSource.Current.ServiceHostInitializationFailed(e.ToString());
                throw;
            }
        }
    }
}
