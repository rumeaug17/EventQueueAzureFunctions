using Microsoft.Azure.WebJobs.Host;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace AgdfEventQueueFunctionApp
{
    internal class EventQueueDispatcher
    {
        private readonly Uri serviceBusUri;

        public EventQueueDispatcher(Uri serviceBusUri)
        {
            this.serviceBusUri = serviceBusUri;
        }

        public async Task Dispatch(BrokeredMessage message)
        {
            var body = message.GetBody<string>();
            dynamic data = JsonConvert.DeserializeObject(body);

            string topicName = data?.EventType;
            string subject = data?.Subject;

            var serviceBus = new ServiceBusService(this.serviceBusUri, null);
            var topicClient = serviceBus.GetTopicClient(topicName);

            var newMessage = new BrokeredMessage(data.ToString());
            newMessage.Properties.Add("subject", subject);

            await topicClient.SendAsync(newMessage);
        }

        public async Task Dispatch(dynamic data, TraceWriter log)
        {
            string topicName = data?.EventType;
            string id = data?.Id;
            string subject = data?.Subject;

            log.Info($"*** id:{id} *** topic:{topicName} *** subject:{subject} ***");

            var serviceBus = new ServiceBusService(this.serviceBusUri, log);
            await serviceBus.CreateTopicIfNotExist(topicName);

            var topicClient = serviceBus.GetTopicClient(topicName);

            var message = new BrokeredMessage(data.ToString());
            message.Properties.Add("subject", subject);
            await topicClient.SendAsync(message);
            log.Info($"posting message {message} to topic");
        }
    }
}