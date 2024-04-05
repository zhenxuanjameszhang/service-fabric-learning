using Microsoft.Extensions.Logging;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stateful1
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class Stateful1 : StatefulService
    {
        private readonly ILogger<Stateful1> _logger;

        public Stateful1(StatefulServiceContext context, ILogger<Stateful1> logger)
            : base(context)
        {
            _logger = logger;
        }

        /// <summary>
        /// Optional override to create listeners (e.g., HTTP, Service Remoting, WCF, etc.) for this service replica to handle client or user requests.
        /// </summary>
        /// <remarks>
        /// For more information on service communication, see https://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return new ServiceReplicaListener[0];
        }

        //protected override async Task OnChangeRoleAsync(ReplicaRole newRole, CancellationToken cancellationToken)
        //{
        //    // TODO: Handle the role change
        //    if (newRole == ReplicaRole.Primary)
        //    {
        //        var testDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary2<string, byte[]>>("testDictionary");

        //        using (var tx = this.StateManager.CreateTransaction())
        //        {

        //            if (await testDictionary.GetCountAsync(tx) != 0)
        //            {

        //                var enumerable = await testDictionary.CreateEnumerableAsync(tx).ConfigureAwait(false);

        //                var enumerator = enumerable.GetAsyncEnumerator();

        //                while (await enumerator.MoveNextAsync(CancellationToken.None).ConfigureAwait(false))
        //                {
        //                    ServiceEventSource.Current.ServiceMessage(this.Context, "Stateful1: Key: {0}, Value: {1}", enumerator.Current.Key, enumerator.Current.Value);
        //                    _logger.LogInformation($"Stateful1 key: {enumerator.Current.Key}, value: {enumerator.Current.Value}");

        //                }
        //            }


        //            await tx.CommitAsync();
        //        }
        //    }

        //    await base.OnChangeRoleAsync(newRole, cancellationToken);
        //}

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {

            // TODO: Replace the following sample code with your own logic 
            //       or remove this RunAsync override if it's not needed in your service.

            // need to trigger checkpoint:
            // - Lower checkpoint threshold to 1
            // - Add programmable amount of transaction afterwards

            // need to read external data:
            // - put information in the data file?

            var stopwatch = new Stopwatch();


            var testDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary2<string, byte[]>>("testDictionary");

            using (var tx = this.StateManager.CreateTransaction())
            {
                if (await testDictionary.GetCountAsync(tx) == 0)
                {
                    await testDictionary.AddAsync(tx, "apple", new byte[4096]);
                    await testDictionary.AddAsync(tx, "Banana", new byte[4096]);
                }

                await tx.CommitAsync();
            }

            stopwatch.Restart();

            for (int i = 0; i < 1000; i++)
            {
                using (var tx = this.StateManager.CreateTransaction())
                {
                    await testDictionary.AddOrUpdateAsync(tx, "apple", new byte[4096], (key, value) => value);
                    await testDictionary.AddOrUpdateAsync(tx, "Banana", new byte[4096], (key, value) => value);
                    await tx.CommitAsync();
                }
            }
            stopwatch.Stop();

            long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;

            ServiceEventSource.Current.ServiceMessage(this.Context, "AddOrUpdate testDictionary time: {0} ms.", elapsedMilliseconds);
            _logger.LogInformation("AddOrUpdate testDictionary time: {0} ms.", elapsedMilliseconds);


            //// Read from the file
            //var aztmDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary2<string, string>>("aztmDictionary");

            //bool needToWrite = false;

            //using (var tx = this.StateManager.CreateTransaction())
            //{
            //    if (await aztmDictionary.GetCountAsync(tx) == 0)
            //    {
            //        needToWrite = true;
            //    }
            //    await tx.CommitAsync();
            //}

            //if (needToWrite)
            //{
            //    stopwatch.Restart();

            //    var filePath = Path.Combine(this.Context.CodePackageActivationContext.GetDataPackageObject("Data").Path, "aztmkeys.txt");
            //    string line;

            //    using (StreamReader reader = new StreamReader(filePath))
            //    {
            //        // Read the file until the end of the stream is reached
            //        while ((line = reader.ReadLine()) != null)
            //        {
            //            using (var tx = this.StateManager.CreateTransaction())
            //            {
            //                await aztmDictionary.AddAsync(tx, line, line);
            //                await tx.CommitAsync();
            //            }

            //            // Do something with the line
            //            //_logger.LogInformation($"{line}");

            //        }
            //    }
            //    stopwatch.Stop();
            //    long aztmDictionaryTime = stopwatch.ElapsedMilliseconds;

            //    ServiceEventSource.Current.ServiceMessage(this.Context, "Add aztmDictionary time: {0} ms.", aztmDictionaryTime);
            //    _logger.LogInformation("Add aztmDictionary time: {0} ms.", aztmDictionaryTime);

            //}




            var myDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, long>>("myDictionary");

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (var tx = this.StateManager.CreateTransaction())
                {
                    var result = await myDictionary.TryGetValueAsync(tx, "Counter");

                    ServiceEventSource.Current.ServiceMessage(this.Context, "Current Counter Value: {0}",
                        result.HasValue ? result.Value.ToString() : "Value does not exist.");

                    await myDictionary.AddOrUpdateAsync(tx, "Counter", 0, (key, value) => ++value);


                    var enumerable = await testDictionary.CreateEnumerableAsync(tx).ConfigureAwait(false);

                    var enumerator = enumerable.GetAsyncEnumerator();

                    while (await enumerator.MoveNextAsync(CancellationToken.None).ConfigureAwait(false))
                    {
                        ServiceEventSource.Current.ServiceMessage(this.Context, "Stateful1: Key: {0}, Value: {1}", enumerator.Current.Key, enumerator.Current.Value);
                        _logger.LogInformation($"Stateful1 key: {enumerator.Current.Key}, value: {enumerator.Current.Value}");

                    }

                    // If an exception is thrown before calling CommitAsync, the transaction aborts, all changes are 
                    // discarded, and nothing is saved to the secondary replicas.
                    await tx.CommitAsync();
                }

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                // Your logic here
            }
        }

    }
}
