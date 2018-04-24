using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.ServiceBus.Messaging;

using System.Collections.Generic;
using System;

namespace AgdfEventQueueFunctionApp.TechnicalTriggers
{
    public static class EventQueueHttpEntryPoint
    {
        private static EventQueueDispatcher dispatcher = new EventQueueDispatcher(new Uri("sb://agdftestservicebus.servicebus.windows.net"));

        [FunctionName("EventQueueHttpEntryPoint")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestMessage req,            
            TraceWriter log)
        {
            // check private access token
            var auth = string.Empty;
            IEnumerable<string> headerValues;
            if (req.Headers.TryGetValues("custom-id", out headerValues))
            {
                auth = headerValues.FirstOrDefault();
            }

            if (string.IsNullOrEmpty(auth))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "custom-id not found in request headers");
            }
            if (auth != "xkTCFRCS9bQlwudgNDmsqblLMqlBjWKe7pPSiAwzR/s=")
            {
                return req.CreateResponse(HttpStatusCode.Forbidden, "connection refused");
            }

            dynamic data = await req.Content.ReadAsAsync<object>();
            await dispatcher.Dispatch(data, log);

            return req.CreateResponse(HttpStatusCode.OK, "Event was received");
        }
    }
}
