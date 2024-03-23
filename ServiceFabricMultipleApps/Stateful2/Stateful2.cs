using Microsoft.Extensions.Logging;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stateful2
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class Stateful2 : StatefulService
    {
        private readonly ILogger<Stateful2> _logger;

        public Stateful2(StatefulServiceContext context, ILogger<Stateful2> logger)
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

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following sample code with your own logic 
            //       or remove this RunAsync override if it's not needed in your service.

            var testDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary2<string, long>>("testDictionary");

            using (var tx = this.StateManager.CreateTransaction())
            {
                if (await testDictionary.GetCountAsync(tx) == 0)
                {
                    await testDictionary.AddAsync(tx, "apple", 1);
                    await testDictionary.AddAsync(tx, "Banana", 2);
                }



                var enumerable = await testDictionary.CreateEnumerableAsync(tx).ConfigureAwait(false);

                var enumerator = enumerable.GetAsyncEnumerator();

                while (await enumerator.MoveNextAsync(CancellationToken.None).ConfigureAwait(false))
                {
                    ServiceEventSource.Current.ServiceMessage(this.Context, "Stateful2: Key: {0}, Value: {1}", enumerator.Current.Key, enumerator.Current.Value);
                    _logger.LogInformation("Stateful2: Key: {0}, Value: {1}", enumerator.Current.Key, enumerator.Current.Value);

                }

                await tx.CommitAsync();
            }

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

                    // If an exception is thrown before calling CommitAsync, the transaction aborts, all changes are 
                    // discarded, and nothing is saved to the secondary replicas.
                    await tx.CommitAsync();
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }
}
