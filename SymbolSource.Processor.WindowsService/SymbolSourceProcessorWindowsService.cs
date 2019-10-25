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

namespace SymbolSource.Processor.WindowsService
{
   public partial class SymbolSourceProcessorWindowsService : ServiceBase
   {
      CancellationTokenSource cancelSource;

      public SymbolSourceProcessorWindowsService()
      {
         InitializeComponent();
         this.cancelSource = new CancellationTokenSource();
      }

      protected override void OnStart(string[] args)
      {
         Trace.Listeners.Add(new ConsoleTraceListener());

         foreach (var assembly in typeof(PackageProcessor).Assembly.GetReferencedAssemblies())
            Trace.WriteLine(assembly.FullName);

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
            this.cancelSource.Cancel();
      }
   }
}
