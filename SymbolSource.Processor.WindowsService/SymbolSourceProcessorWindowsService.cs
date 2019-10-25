using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using Autofac;
using Microsoft.Azure.WebJobs;
using Microsoft.ApplicationInsights.Extensibility;
using SymbolSource.Contract;
using SymbolSource.Contract.Container;
using SymbolSource.Contract.Scheduler;
using SymbolSource.Support;
using log4net;
using System.Threading.Tasks;

namespace SymbolSource.Processor.WindowsService
{
   public partial class SymbolSourceProcessorWindowsService : ServiceBase
   {
      private readonly ILog log = LogManager.GetLogger("SymbolSource.Processor.WindowsService");
      CancellationTokenSource cancelSource;

      public SymbolSourceProcessorWindowsService()
      {
         log.Info("Initializing the SymbolSource.Processor Windows service.");
         InitializeComponent();
         this.cancelSource = new CancellationTokenSource();
      }

      protected override void OnStart(string[] args)
      {
         Task.Run(() => ProcessSymbolSourceQueue());
      }

      private void ProcessSymbolSourceQueue()
      {
         log.Info("Starting the SymbolSource.Processor Windows service.");

         foreach (var assembly in typeof(PackageProcessor).Assembly.GetReferencedAssemblies())
         {
            log.Debug(assembly.FullName);
         }

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

      protected override void OnStop()
      {
         log.Info("Stopping the SymbolSource.Processor Windows service.");
         this.cancelSource.Cancel();
      }
   }
}
