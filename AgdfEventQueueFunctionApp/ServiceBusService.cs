using Microsoft.Azure.WebJobs.Host;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using System;
using System.Threading.Tasks;

namespace AgdfEventQueueFunctionApp
{
    public class ServiceBusService
    {
        private TraceWriter log;
        private NamespaceManager namespaceClient;
        private Uri serviceBusUri;
        private TokenProvider tokenProvider;

        public ServiceBusService(Uri serviceBusUri, TraceWriter log)
        {
            this.serviceBusUri = serviceBusUri;
            var sasKeyName = System.Environment.GetEnvironmentVariable("SERVICEBUS_sasKeyName", EnvironmentVariableTarget.Process);
            var sasKeyValue = System.Environment.GetEnvironmentVariable("SERVICEBUS_sasKeyValue", EnvironmentVariableTarget.Process);

            this.tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(sasKeyName, sasKeyValue);
            this.namespaceClient = new NamespaceManager(serviceBusUri, this.tokenProvider);
            this.log = log;
        }

        public async Task CreateSubscriptionIfNotExist(string topicName, string subscriptionName, string filter, bool withGlobalDeadQueue = true)
        {
            if (!namespaceClient.SubscriptionExists(topicName, subscriptionName))
            {
                var subscription = await namespaceClient.CreateSubscriptionAsync(topicName, subscriptionName, new SqlFilter(filter ?? "1 = 1"));
                if (withGlobalDeadQueue)
                {
                    subscription.ForwardDeadLetteredMessagesTo = "global.dead.letter.queue";
                }

                subscription.EnableDeadLetteringOnMessageExpiration = true;
                subscription.EnableDeadLetteringOnFilterEvaluationExceptions = true;
                subscription.DefaultMessageTimeToLive = new TimeSpan(days: 1, hours: 0, minutes: 0, seconds: 0);

                await namespaceClient.UpdateSubscriptionAsync(subscription);
                log?.Info($"creating subscription {subscriptionName}");
            }
        }

        public async Task CreateTopicIfNotExist(string topicName)
        {
            if (!namespaceClient.TopicExists(topicName))
            {
                log?.Info($"creating topic {topicName}");
                await namespaceClient.CreateTopicAsync(topicName);
            }
        }

        public QueueClient GetQueueClient(string queueName)
        {
            var factory = MessagingFactory.Create(serviceBusUri, tokenProvider);
            return factory.CreateQueueClient(queueName, ReceiveMode.ReceiveAndDelete);
        }

        public long? GetQueueLength(string queueName)
        {
            return namespaceClient.GetQueue(queueName)?.MessageCountDetails.ActiveMessageCount;
        }

        public TopicClient GetTopicClient(string topicName)
        {
            var factory = MessagingFactory.Create(serviceBusUri, tokenProvider);
            return factory.CreateTopicClient(topicName);
        }
    }
}