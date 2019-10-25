using System.Diagnostics;
using System.Threading;
using Autofac;
using log4net;
using Microsoft.Azure.WebJobs;
using Microsoft.ApplicationInsights.Extensibility;
using SymbolSource.Contract;
using SymbolSource.Contract.Container;
using SymbolSource.Contract.Scheduler;
using SymbolSource.Support;

namespace SymbolSource.Processor.Console
{
    public class Program
    {
        private static readonly ILog log = LogManager.GetLogger("SymbolSource.Processor.Console");

        static void Main(string[] args)
        {
            log.Info("Starting up the SymbolSource.Processor.Console");

            log.Debug("Loading referenced Assemblies");
            foreach (var assembly in typeof(PackageProcessor).Assembly.GetReferencedAssemblies())
            {
               log.Debug(assembly.FullName);
            }

            var cancelSource = new CancellationTokenSource();

            System.Console.CancelKeyPress += (o, e) =>
            {
                cancelSource.Cancel();
                e.Cancel = true;
            };

            var shutdownWatcher = new WebJobsShutdownWatcher();
            var shutdownSource = CancellationTokenSource.CreateLinkedTokenSource(new[]
            {
                shutdownWatcher.Token,
                cancelSource.Token
            });

            var configuration = new DefaultConfigurationService();
            var builder = new ContainerBuilder();

            DefaultContainerBuilder.Register(builder, configuration);
            SupportContainerBuilder.Register(builder, SupportEnvironment.WebJob);
            PackageProcessorContainerBuilder.Register(builder);

            var container = builder.Build();

            var support = container.Resolve<ISupportConfiguration>();

            if (!string.IsNullOrWhiteSpace(support.InsightsInstrumentationKey))
                TelemetryConfiguration.Active.InstrumentationKey = support.InsightsInstrumentationKey;

            var scheduler = container.Resolve<ISchedulerService>();
            scheduler.ListenAndProcess(shutdownSource.Token);
        }
    }
}
