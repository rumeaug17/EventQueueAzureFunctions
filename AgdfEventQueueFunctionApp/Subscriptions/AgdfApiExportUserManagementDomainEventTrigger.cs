using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.ServiceBus.Messaging;
using System;
using System.Threading.Tasks;

namespace AgdfEventQueueFunctionApp.Subscriptions
{
    public static class AgdfApiExportUserManagementDomainEventTrigger
    {
        private static DefaultHttpWebHook webHook = new DefaultHttpWebHook(new Uri("https://localhost/Agdf.Api.Export"), "/event/userManagmentDomain");

        [FunctionName("AgdfApiExportUserManagementDomainEventTrigger")]
        public static async Task Run(
            [ServiceBusTrigger("usermanagmentdomainevent", "AgdfApiExport.UserManagmentDomainEvent.Subs", AccessRights.Manage, Connection = "agdftestservicebus_RootManageSharedAccessKey_SERVICEBUS")]BrokeredMessage message,
            TraceWriter log)
        {
            await webHook.Post(message, log);
        }
    }
}