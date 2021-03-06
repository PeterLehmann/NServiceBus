﻿namespace NServiceBus.AcceptanceTests.PubSub
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using EndpointTemplates;
    using AcceptanceTesting;
    using Features;
    using NUnit.Framework;
    using ScenarioDescriptors;
    using Unicast.Subscriptions;
    using Unicast.Subscriptions.MessageDrivenSubscriptions;

    public class When_publishing_an_event_with_the_subscriber_scaled_out : NServiceBusAcceptanceTest
    {
        static string Server1 = "Server1";
        static string Server2 = "Server2";

        [Test]//https://github.com/NServiceBus/NServiceBus/issues/1101
        public void Should_only_publish_one_event()
        {
            Scenario.Define<Context>()
                    .WithEndpoint<Publisher>(b =>
                        b.Given((bus, context) => Subscriptions.OnEndpointSubscribed(s =>
                            {
                                if (s.SubscriberReturnAddress.Queue != "MyEndpoint")
                                    return;

                                context.NumberOfSubcriptionsReceived++;
                            }))
                        .When(c => c.NumberOfSubcriptionsReceived >= 2, (bus, c) =>
                            {
                                c.SubcribersOfTheEvent = Configure.Instance.Builder.Build<ISubscriptionStorage>()
                                                                  .GetSubscriberAddressesForMessage(new[] { new MessageType(typeof(MyEvent)) }).Select(a => a.ToString()).ToList();
                            })
                     )
                    .WithEndpoint<Subscriber1>(b => b.Given((bus, context) =>
                        {
                            bus.Subscribe<MyEvent>();

                            if (!Feature.IsEnabled<MessageDrivenSubscriptions>())
                                context.NumberOfSubcriptionsReceived++;
                        }))
                      .WithEndpoint<Subscriber2>(b => b.Given((bus, context) =>
                      {
                          bus.Subscribe<MyEvent>();

                          if (!Feature.IsEnabled<MessageDrivenSubscriptions>())
                              context.NumberOfSubcriptionsReceived++;
                      }))
                    .Done(c => c.SubcribersOfTheEvent != null)
                    .Repeat(r => r.For(Transports.SqlServer)
                        .For<AllSubscriptionStorages>(SubscriptionStorages.Msmq))
                    .Should(c =>
                    {
                        Assert.AreEqual(1, c.SubcribersOfTheEvent.Count(), "There should only be one logical subscriber");
                    })
                    .MaxTestParallelism(1)//we force the endpoint names so we can't run this is parallell
                    .Run();
        }

        public class Context : ScenarioContext
        {
            public int NumberOfSubcriptionsReceived { get; set; }

            public IEnumerable<string> SubcribersOfTheEvent { get; set; }
        }

        public class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultServer>();
            }
        }

        public class Subscriber1 : EndpointConfigurationBuilder
        {
            public Subscriber1()
            {
                EndpointSetup<DefaultServer>(c => Configure.Features.Disable<AutoSubscribe>())
                    .AddMapping<MyEvent>(typeof (Publisher))
                    .CustomMachineName(Server1)
                    .CustomEndpointName("MyEndpoint");
            }

            public class MyEventHandler : IHandleMessages<MyEvent>
            {
                public Context Context { get; set; }

                public void Handle(MyEvent messageThatIsEnlisted)
                {
                }
            }
        }

        public class Subscriber2 : EndpointConfigurationBuilder
        {
            public Subscriber2()
            {
                EndpointSetup<DefaultServer>(c => Configure.Features.Disable<AutoSubscribe>())
                        .AddMapping<MyEvent>(typeof(Publisher))
                        .CustomMachineName(Server2)
                        .CustomEndpointName("MyEndpoint");
            }

            public class MyEventHandler : IHandleMessages<MyEvent>
            {
                public Context Context { get; set; }

                public void Handle(MyEvent messageThatIsEnlisted)
                {
                }
            }
        }

        [Serializable]
        public class MyEvent : IEvent
        {
        }
    }
}