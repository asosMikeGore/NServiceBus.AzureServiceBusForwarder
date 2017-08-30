﻿using System;
using FakeItEasy;
using NServiceBus.Logging;
using NUnit.Framework;

namespace NServiceBus.AzureServiceBusForwarder.Tests
{
    [TestFixture]
    public class ForwarderTests
    {
        private const int ReceiveBatchSize = 1;
        private const int PrefetchCount = 1;
        private IMessageForwarder messageForwarderFake;
        private ILog loggerFake;

        [SetUp]
        public void Setup()
        {
            messageForwarderFake = A.Fake<IMessageForwarder>();
            loggerFake = A.Fake<ILog>();
        }

        [Test]
        public void when_creating_a_forwarder_the_source_configuration_is_required()
        {
            Assert.Throws<ArgumentNullException>(() => new Forwarder(
                null,
                new ForwarderDestinationConfiguration("DestinationQueue", () => messageForwarderFake),
                message => typeof(TestMessage),
                loggerFake));
        }

        [Test]
        public void when_creating_a_forwarder_the_destination_configuration_is_required()
        {
            Assert.Throws<ArgumentNullException>(() => new Forwarder(
                new ForwarderSourceConfiguration("ConnectionString", "TestTopic", ReceiveBatchSize, PrefetchCount),
                null,
                message => typeof(TestMessage),
                loggerFake));
        }

        [Test]
        public void when_creating_a_forwarder_a_message_mapper_is_required()
        {
            Assert.Throws<ArgumentNullException>(() => new Forwarder(
                new ForwarderSourceConfiguration("ConnectionString", "TestTopic", ReceiveBatchSize, PrefetchCount),
                new ForwarderDestinationConfiguration("DestinationQueue", () => messageForwarderFake),
                null,
                loggerFake));
        }

        [Test]
        public void when_creating_a_forwarder_a_logger_is_required()
        {
            Assert.Throws<ArgumentNullException>(() => new Forwarder(
                new ForwarderSourceConfiguration("ConnectionString", "TestTopic", ReceiveBatchSize, PrefetchCount),
                new ForwarderDestinationConfiguration("DestinationQueue", () => messageForwarderFake),
                message => typeof(TestMessage),
                null));
        }
    }
}
