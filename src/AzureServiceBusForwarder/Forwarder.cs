using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace AzureServiceBusForwarder
{
    public class Forwarder
    {
        private readonly int concurrency;
        private readonly ForwarderSourceConfiguration sourceConfiguration;
        private readonly ForwarderDestinationConfiguration destinationConfiguration;
        private readonly ILogger logger;
        private readonly List<IBatchMessageReceiver> messageReceivers = new List<IBatchMessageReceiver>();

        public Forwarder(ForwarderConfiguration configuration)
        {
            Guard.IsNotNull(configuration, nameof(configuration));

            this.sourceConfiguration = configuration.Source;
            this.destinationConfiguration = configuration.Destination;
            this.logger = configuration.Logger;
            this.concurrency = configuration.Concurrency;
        }

        public void Start()
        {
            CreateQueueClients();
            Poll();
        }

        private void Poll()
        {
            foreach (var messageReceiver in messageReceivers)
            {
                PollMessageReceiever(messageReceiver);
            }
        }

        private async Task PollMessageReceiever(IBatchMessageReceiver receiver) // TODO: Support cancellation
        {
            var stopwatch = new Stopwatch();
            var messageForwarder = this.destinationConfiguration.MessageForwarderFactory();

            while (true)
            {
                try
                {
                    stopwatch.Restart();
                    var messages = (await receiver.ReceieveMessages(sourceConfiguration.ReceiveBatchSize).ConfigureAwait(false)).ToArray();
                    logger.Info($"Received {messages.Length} messages from the source. Took {stopwatch.Elapsed}");
                    stopwatch.Restart();
                    var sentMessageTokens = (await messageForwarder.ForwardMessages(messages).ConfigureAwait(false)).ToArray();
                    logger.Info($"Forwarded {sentMessageTokens.Length} messages to the destination. Took {stopwatch.Elapsed}");
                    stopwatch.Restart();
                    await receiver.CompleteMessages(sentMessageTokens).ConfigureAwait(false);
                    logger.Info($"Completed {sentMessageTokens.Length} messages at the source. Took {stopwatch.Elapsed}");
                }
                catch (Exception e)
                {
                    logger.Error(e.Message, e);
                }
            }
        }

        private void CreateQueueClients()
        {
            for (int i = 0; i < concurrency; i++)
            {
                messageReceivers.Add(sourceConfiguration.MessageReceiverFactory());
            }
        }
    }
}