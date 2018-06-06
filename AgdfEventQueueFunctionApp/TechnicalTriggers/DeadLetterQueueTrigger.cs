using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.ServiceBus.Messaging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AgdfEventQueueFunctionApp.TechnicalTriggers
{
    public static class DeadLetterQueueTrigger
    {
        private static EventQueueDispatcher dispatcher = new EventQueueDispatcher(new Uri("sb://agdftestservicebus.servicebus.windows.net"));
        private const int Max_Messages = 1000;

        [FunctionName("DeadLetterQueueTrigger")]
        public static async Task Run([TimerTrigger("0 */17 * * * *")]TimerInfo myTimer, TraceWriter log)
        {
            // get messages from dead letter queue and resent them to dispatcher
            log.Info($"C# Timer trigger function executed at: {DateTime.Now}");

            var serviceBusUri = new Uri("sb://agdftestservicebus.servicebus.windows.net");
            var serviceBus = new ServiceBusService(serviceBusUri, log);
            var deadqueueClient = serviceBus.GetQueueClient("global.dead.letter.queue");

            var len = serviceBus.GetQueueLength("global.dead.letter.queue");
            log.Info($"queue length : {len}");
            if (len > 0)
            {                
                var messages = await deadqueueClient?.ReceiveBatchAsync(Max_Messages);
                var batch = new List<BrokeredMessage>(messages);
                log.Info($"batch length : {batch.Count}");

                foreach (var message in batch)
                {
                    log.Info($"message repost to dispatcher: {message.MessageId}");
                    await dispatcher.Dispatch(message);
                    log.Info($"repost to dispatcher ok: {message.MessageId}");
                }
            }

            log.Info($"finish, it's time to weekend");
            await deadqueueClient?.CloseAsync();
        }
    }
}