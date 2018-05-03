using Microsoft.Azure.WebJobs.Host;
using Microsoft.ServiceBus.Messaging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Newtonsoft.Json;
using System.Linq;
using System.Security.Authentication;
using System.Threading.Tasks;

namespace AgdfEventQueueFunctionApp
{
    internal class CosmosDbHelper
    {
        private readonly IMongoDatabase database;
        private readonly TraceWriter log;

        public CosmosDbHelper(string connection, string dbname, TraceWriter log)
        {
            this.database = CosmosDbHelper.GetDatabase(connection, dbname);
            this.log = log;
        }

        public async Task<IMongoCollection<BsonDocument>> GetCollection(string name)
        {
            var collection = database.GetCollection<BsonDocument>(name);
            if (collection == null)
            {
                // todo better do do this with write concern
                this.log.Info($"creating collection: {name}");
                await database.CreateCollectionAsync(name);
                collection = database.GetCollection<BsonDocument>(name);
            }
            return collection;
        }

        public async Task InsertDocumentIfNew(IMongoCollection<BsonDocument> collection, BrokeredMessage message)
        {
            var databody = message.GetBody<string>();
            dynamic messageData = JsonConvert.DeserializeObject(databody);
            var data = new { _id = messageData?.Id, messageData?.Subject, WorkItemDocument = messageData?.Data };
            var json = JsonConvert.SerializeObject(data);

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
                this.log.Info($"data already in the event store: {data?._id}");
            }
        }

        private static IMongoDatabase GetDatabase(string connection, string dbname)
        {
            MongoClientSettings settings = MongoClientSettings.FromUrl(new MongoUrl(connection));
            settings.SslSettings = new SslSettings() { EnabledSslProtocols = SslProtocols.Tls12 };
            var mongoClient = new MongoClient(settings);

            var dbName = "agdf-event-store";
            var database = mongoClient.GetDatabase(dbName);
            return database;
        }
    }
}