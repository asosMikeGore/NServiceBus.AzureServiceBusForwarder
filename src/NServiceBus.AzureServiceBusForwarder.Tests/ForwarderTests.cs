﻿using System;
using FakeItEasy;
using Microsoft.ServiceBus;
using NUnit.Framework;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;

namespace NServiceBus.AzureServiceBusForwarder.Tests
{
    [TestFixture]
    public class ForwarderTests
    {
        private IEndpointInstance endpointFake;

        [SetUp]
        public void Setup()
        {
            endpointFake = A.Fake<IEndpointInstance>();
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        public void when_creating_a_forwarder_the_connection_string_is_required(string connectionString)
        {
            Assert.Throws<ArgumentException>(() => new Forwarder(connectionString, "TestTopic", "DestinationQueue", endpointFake, message => typeof(TestMessage)));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        public void when_creating_a_forwarder_the_topic_name_is_required(string topicName)
        {
            Assert.Throws<ArgumentException>(() => new Forwarder("ConnectionString", topicName, "DestinationQueue", endpointFake, message => typeof(TestMessage)));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        public void when_creating_a_forwarder_the_destination_queue_is_required(string destinationQueue)
        {
            Assert.Throws<ArgumentException>(() => new Forwarder("ConnectionString", "TestTopic", destinationQueue, endpointFake, message => typeof(TestMessage)));
        }

        [Test]
        public void when_creating_a_forwarder_an_endpoint_is_required()
        {
            Assert.Throws<ArgumentNullException>(() => new Forwarder("ConnectionString", "TestTopic", "DestinationQueue", null, message => typeof(TestMessage)));
        }

        [Test]
        public void when_creating_a_forwarder_a_message_mapper_is_required()
        {
            Assert.Throws<ArgumentNullException>(() => new Forwarder("ConnectionString", "TestTopic", "DestinationQueue", endpointFake, null));
        }

        // TODO: Improve test to ensure we do not need to delay
        [Test]
        public async Task when_a_forwarder_is_started_messages_are_forwarded_via_the_endpoint()
        {
            const string topicName = "sourceTopic";
            var namespaceConnectionString = Environment.GetEnvironmentVariable("NServiceBus.AzureServiceBusForwarder.ConnectionString", EnvironmentVariableTarget.User);
            var namespaceManager = NamespaceManager.CreateFromConnectionString(namespaceConnectionString);
            
            if (!await namespaceManager.TopicExistsAsync(topicName))
            {
                await namespaceManager.CreateTopicAsync(topicName);
            }

            var forwarder = new Forwarder(namespaceConnectionString, topicName, "destinationQueue", endpointFake, message => typeof(TestMessage));
            forwarder.Start();

            await Task.Delay(TimeSpan.FromSeconds(5));

            var topicClient = TopicClient.CreateFromConnectionString(namespaceConnectionString, topicName);
            var eventMessage = new BrokeredMessage(new TestMessage());
            await topicClient.SendAsync(eventMessage);

            await Task.Delay(TimeSpan.FromSeconds(5));

            A.CallTo(endpointFake).MustHaveHappened(Repeated.Exactly.Once);
        }
    }

    public class TestMessage : IMessage
    {
    }
}
