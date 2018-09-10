using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SymbolSource.Contract.Processor;
using SymbolSource.Contract.Storage;
using Polly;
using Polly.Retry;

namespace SymbolSource.Contract.Scheduler
{
    public class FileSchedulerService : ISchedulerService
    {
        private readonly ILocalStorageConfiguration configuration;
        private readonly Lazy<IPackageProcessor> processor;

        public FileSchedulerService(ILocalStorageConfiguration configuration, Lazy<IPackageProcessor> processor)
        {
            this.configuration = configuration;
            this.processor = processor;
        }

        public async Task Signal(PackageMessage message)
        {
            Trace.TraceInformation("Signaling message {0}", message);
            var file = Path.Combine(configuration.RootPath, Guid.NewGuid() + ".tmp");
            File.WriteAllText(file, JsonConvert.SerializeObject(message));
            File.Move(file, Path.ChangeExtension(file, ".run"));
        }

        public void ListenAndProcess(CancellationToken cancellationToken)
        {
            foreach (var file in Directory.GetFiles(configuration.RootPath, "*.run"))
                Process(file);

            using (var watcher = new FileSystemWatcher(configuration.RootPath, "*.run"))
            {
                watcher.Renamed += (o, e) => Process(e.FullPath);
                Trace.TraceInformation("Starting watcher");
                watcher.EnableRaisingEvents = true;
                cancellationToken.WaitHandle.WaitOne();
                Trace.TraceInformation("Stopping watcher");
                watcher.EnableRaisingEvents = false;
            }
        }

        private async void Process(string file)
        {
            await Task.Delay(1000);
            Policy policy = RetryPolicy.Handle<Exception>().WaitAndRetry(3, (i) => TimeSpan.FromSeconds(1) );

            try
            {
               await policy.Execute(async () =>
               {
                   var message = JsonConvert.DeserializeObject<PackageMessage>(File.ReadAllText(file));
                   Trace.TraceInformation("Processing message {0}", message);

                   await processor.Value.Process(message);
                   Trace.TraceInformation("Removing signal for message {0}", message);
                   File.Delete(file);
               });
            }
            catch ( Exception exception )
            {
                Trace.TraceWarning($"Exception found.\n{exception.GetType()}; {exception.Message}\n {exception.StackTrace}");
            }
        }
    }

   
}