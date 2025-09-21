using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KimiAppNative
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            
            // Configure logging
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddEventLog();
            
            // Add the background service
            builder.Services.AddHostedService<OhGeeBackgroundService>();
            
            // Configure as Windows Service
            builder.Services.AddWindowsService(options =>
            {
                options.ServiceName = "OhGee AI Assistant Service";
            });

            var host = builder.Build();
            host.Run();
        }
    }
}
