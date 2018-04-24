using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

using MongoDB.Driver;
using System.Security.Authentication;
using MongoDB.Bson.Serialization;
using MongoDB.Bson;

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
            var connectionString = "mongodb://agdf-event-store:Ss1eBSWgLZVg6n2hKxSIRyVQ60SLmGx1aUO5s5aCClqH6jQfmlZTzq4C1ukM2RvwTo7L9zCfLaCRsXMNIHQeNw==@agdf-event-store.documents.azure.com:10255/?ssl=true&replicaSet=globaldb";
            MongoClientSettings settings = MongoClientSettings.FromUrl(new MongoUrl(connectionString));
            settings.SslSettings =  new SslSettings() { EnabledSslProtocols = SslProtocols.Tls12 };
            var mongoClient = new MongoClient(settings);

            var dbName = "agdf-event-store";
            var collectionName = "WorkItemEvents";

            var database = mongoClient.GetDatabase(dbName);

            var collection = database.GetCollection<BsonDocument>(collectionName);
            if (collection == null) {
                // todo better do do this with write concern
                log.Info($"creating collection: {collectionName}");
                await database.CreateCollectionAsync(collectionName);
                collection = database.GetCollection<BsonDocument>(collectionName);
            }

            var body = message.GetBody<string>();
            dynamic messageData = JsonConvert.DeserializeObject(body);
            var data = new { _id = messageData?.Id, messageData?.Subject, WorkItemDocument = messageData?.Data };
            var json = JsonConvert.SerializeObject(data);

            log.Info($"data to insert in the event store : {json}");

            var document = BsonSerializer.Deserialize<BsonDocument>(json);

            var res = await collection.FindAsync($"{{ _id:\"{data?._id}\" }}");
            var l = res.ToList();
            if (l.Count <= 0)
            {
                // other idea; make a hash of data without id and if we have the same, ignore the insert 
                await collection.InsertOneAsync(document);
            }
            else
            {
                log.Info($"data already in the event store : {l[0]}");
            }
        }
    }
}