using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;
using NServiceBus.AzureServiceBusForwarder.Serializers;

namespace NServiceBus.AzureServiceBusForwarder
{

    public class AzureServiceBusMessageForwarder : IMessageForwarder
    {
        private static readonly Action<BrokeredMessage> DefaultMessageMutator = (message) => { };

        private readonly QueueClient sendClient;
        private readonly Action<BrokeredMessage> outgoingMessageMutator;

        public AzureServiceBusMessageForwarder(QueueClient sendClient, Action<BrokeredMessage> outgoingMessageMutator)
        {
            Guard.IsNotNull(sendClient, nameof(sendClient));

            this.sendClient = sendClient;
            this.outgoingMessageMutator = outgoingMessageMutator ?? DefaultMessageMutator;
        }

        public async Task<IEnumerable<Guid>> ForwardMessages(IEnumerable<BrokeredMessage> messages)
        {
            var lockTokens = new List<Guid>();
            var messagesToForward = new List<BrokeredMessage>();

            foreach (var message in messages)
            {
                var forwardMessage = new BrokeredMessage(message.GetBody<Stream>());
                CopyHeaders(message, forwardMessage);
                outgoingMessageMutator(forwardMessage);

                messagesToForward.Add(forwardMessage);
                lockTokens.Add(message.LockToken);
            }

            if (messagesToForward.Any())
            {
                await sendClient.SendBatchAsync(messagesToForward);
            }
            return lockTokens;
        }

        public void CopyHeaders(BrokeredMessage from, BrokeredMessage to)
        {
            to.MessageId = from.MessageId;
            to.ContentType = from.ContentType;

            foreach (var property in from.Properties)
            {
                to.Properties[property.Key] = property.Value;
            }
        }
    }

    public class NServiceBusMessageForwarder : IMessageForwarder
    {
        private static readonly List<string> IgnoredHeaders = new List<string>
        {
            "NServiceBus.Transport.Encoding" // Don't assume endpoint forwarding into uses the same serialization
        };

        private readonly Func<BrokeredMessage, Type> messageMapper;
        private readonly IEndpointInstance endpoint;
        private readonly string destinationQueue;
        private readonly ISerializer serializer;

        public NServiceBusMessageForwarder(string destinationQueue, IEndpointInstance endpoint, Func<BrokeredMessage, Type> messageMapper, ISerializer serializer)
        {
            Guard.IsNotNull(messageMapper, nameof(messageMapper));
            Guard.IsNotNull(endpoint, nameof(endpoint));
            Guard.IsNotNullOrEmpty(destinationQueue, nameof(destinationQueue));
            Guard.IsNotNull(serializer, nameof(serializer));

            this.messageMapper = messageMapper;
            this.endpoint = endpoint;
            this.destinationQueue = destinationQueue;
            this.serializer = serializer;
        }

        public Task ForwardMessage(BrokeredMessage message)
        {
            var messageType = messageMapper(message);
            var body = GetMessageBody(messageType, message);
            var sendOptions = new SendOptions();
            sendOptions.SetDestination(destinationQueue);

            foreach (var p in message.Properties.Where(x => !IgnoredHeaders.Contains(x.Key)))
            {
                sendOptions.SetHeader(p.Key, p.Value.ToString());
            }

            return endpoint.Send(body, sendOptions);
        }

        public object GetMessageBody(Type type, BrokeredMessage brokeredMessage)
        {
            return serializer.Deserialize(brokeredMessage, type);
        }

        public async Task<IEnumerable<Guid>> ForwardMessages(IEnumerable<BrokeredMessage> messages)
        {
            var lockTokens = new List<Guid>();
            var forwardingTasks = new List<Task>();

            foreach (var message in messages)
            {
                forwardingTasks.Add(ForwardMessage(message));
                lockTokens.Add(message.LockToken);
            }

            await Task.WhenAll(forwardingTasks).ConfigureAwait(false);
            return lockTokens;
        }
    }
}