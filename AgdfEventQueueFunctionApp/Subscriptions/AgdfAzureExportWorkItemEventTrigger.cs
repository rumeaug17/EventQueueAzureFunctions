using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.ServiceBus.Messaging;
using System;
using System.Threading.Tasks;

namespace AgdfEventQueueFunctionApp
{
    internal class SubscriptionLoader
    {
        public static readonly string SubscriptionName = "AgdfAzureExport.WorkItemEventBase.Subs";
        public static readonly string TopicName = "workitemeventbase";

        public SubscriptionLoader()
        {
            var serviceBusUri = new Uri("sb://agdftestservicebus.servicebus.windows.net");
            var serviceBus = new ServiceBusService(serviceBusUri, null);
            serviceBus.CreateSubscriptionIfNotExist(TopicName, SubscriptionName, null).ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }

    public static class AgdfAzureExportWorkItemEventTrigger
    {
        // Todo create subscription by code at startup with a static object loader ?
        private static SubscriptionLoader loader = new SubscriptionLoader();

        [FunctionName("AgdfAzureExportWorkItemEventTrigger")]
        public static async Task Run(
            [ServiceBusTrigger("workitemeventbase", "AgdfAzureExport.WorkItemEventBase.Subs", AccessRights.Manage, Connection = "agdftestservicebus_RootManageSharedAccessKey_SERVICEBUS")]BrokeredMessage message,
            TraceWriter log)
        {
            var connectionString = System.Environment.GetEnvironmentVariable("MONGODB_CONNEXION", EnvironmentVariableTarget.Process);
            var dbName = System.Environment.GetEnvironmentVariable("MONGODB_DBName", EnvironmentVariableTarget.Process);
            var helper = new CosmosDbHelper(connectionString, dbName, log);

            var collectionName = "WorkItemEvents";
            var collection = await helper.GetCollection(collectionName);
            await helper.InsertDocumentIfNew(collection, message);
        
        }
    }
}