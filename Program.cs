using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using NLog;
using System;

namespace API_Samples
{
    class Program
    {
        static void Main(string[] args)
        {
            Common.InitErrorHandlers();
            if (Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == Environments.Development)
                LogManager.LoadConfiguration("nlog.Development.config");
            else
                LogManager.LoadConfiguration("nlog.config");
            Logger logger = LogManager.GetCurrentClassLogger();
            LogManager.ThrowExceptions = true;
            LogManager.ThrowConfigExceptions = true;
            try
            {
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                logger.Fatal(ex, ex.ToString());
                throw;
            }
            finally
            {
                LogManager.Flush();
                LogManager.Shutdown();
            }
        }
        private static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .UseSystemd()
                .ConfigureLogging(logging => logging.ClearProviders().AddNLog(new NLogProviderOptions
                {
                    CaptureMessageTemplates = true,
                    CaptureMessageProperties = true
                }))
                .ConfigureServices((hostContext, services) =>
                {
                    services
                        .AddSingleton<LeadsHandler>()
                        .AddSingleton<OrdersHandler>()
                        .AddHostedService<DatabaseObserver<LeadsHandler>>()
                        .AddHostedService<DatabaseObserver<OrdersHandler>>();
                });
        }
    }
}
