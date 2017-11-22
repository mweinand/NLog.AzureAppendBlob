using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using System;

namespace NLog.AzureAppendBlob.Test
{
	internal class Program
	{
		private static void Main()
		{
            var servicesProvider = BuildDi();
            var runner = servicesProvider.GetRequiredService<Runner>();

            runner.DoAction("Action1");

            Console.WriteLine("Press ANY key to exit");
            Console.ReadLine();
        }
        private static IServiceProvider BuildDi()
        {
            var services = new ServiceCollection();

            //Runner is the custom class
            services.AddTransient<Runner>();

            services.AddSingleton<ILoggerFactory, LoggerFactory>();
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            services.AddLogging((builder) => builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace));

            var serviceProvider = services.BuildServiceProvider();

            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

            //configure NLog
            loggerFactory.AddNLog(new NLogProviderOptions { CaptureMessageTemplates = true, CaptureMessageProperties = true });
            loggerFactory.ConfigureNLog("nlog.config");

            return serviceProvider;
        }

        public class Runner
        {
            private readonly ILogger<Runner> logger;

            public Runner(ILogger<Runner> logger)
            {
                this.logger = logger;
            }

            public void DoAction(string name)
            {
                logger.LogTrace("Hello!");
                logger.LogDebug("This is");
                logger.LogInformation("NLog using");
                logger.LogWarning("Append blobs in");
                logger.LogError("Windows Azure");
                logger.LogCritical("Storage.");

                try
                {
                    throw new NotSupportedException();
                }
                catch (Exception ex)
                {
                    logger.LogError("This is an expected exception.", ex);
                }
            }


        }
    }
}
