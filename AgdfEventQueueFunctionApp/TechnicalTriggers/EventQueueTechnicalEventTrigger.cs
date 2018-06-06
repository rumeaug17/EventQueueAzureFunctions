using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace AgdfEventQueueFunctionApp
{    
    public static class EventQueueTechnicalEventTrigger
    {
        [FunctionName("EventQueueTechnicalEventTrigger")]
        public static async Task Run(
            [ServiceBusTrigger("servicebuscommandevent", "ServiceBusCommandEvent.Subscription", AccessRights.Manage, Connection = "agdftestservicebus_RootManageSharedAccessKey_SERVICEBUS")] BrokeredMessage mySbMsg,
            TraceWriter log)
        {
            var body = mySbMsg.GetBody<string>();
            dynamic data = JsonConvert.DeserializeObject(body);

            string id = data?.Id;
            string subject = data?.Subject;
            log.Info($"*** id:{id} *** subject:{subject} ***");

            string subscriptionName = data?.Data?.SubscriptionName;
            string eventType = data?.Data?.EventType;
            int order = data?.Data?.Order;

            // TODO check data
            log.Info($"*** subscriptionName:{subscriptionName} *** eventType:{eventType} *** order:{order} ***");

            var serviceBusUri = new Uri("sb://agdftestservicebus.servicebus.windows.net");
            var serviceBus = new ServiceBusService(serviceBusUri, log);

            await serviceBus.CreateTopicIfNotExist(eventType);
            await serviceBus.CreateSubscriptionIfNotExist(eventType, subscriptionName, null);
        }
    }
}