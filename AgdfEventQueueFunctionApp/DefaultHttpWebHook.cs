using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.ServiceBus.Messaging;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace AgdfEventQueueFunctionApp
{
    public class DefaultHttpWebHook
    {
        private Uri baseAddress;
        private string path;

        private readonly AsyncLazy<HttpClient> lazyClient;

        private string AuthToken(string user, string passwd) => Convert.ToBase64String(
            Encoding.Default.GetBytes(s: $"{user}:{passwd}"));

        private KeyValuePair<string, string>[] Headers
        {
            get
            {
                var user = "";
                var passwd = "";
                return new[] {
                    new KeyValuePair<string, string>("Accept", "application/json"),
                    new KeyValuePair<string, string>("Authorization", value:$"Basic {AuthToken(user, passwd)}"),
                };
            }
        }

        private static TimeSpan DefaultTimeout => new TimeSpan(10 * 1000);

        public DefaultHttpWebHook(Uri baseAddress, string path)
        {
            this.baseAddress = baseAddress;
            this.path = path;
            lazyClient = new AsyncLazy<HttpClient>(async () => await GetHttpClient());
        }

        public async Task Post(BrokeredMessage message, TraceWriter log)
        {
            var body = message.GetBody<string>();
            log.Info($"messagebody:{body}");

            var client = await lazyClient;

            // just get message and send to the endpoint
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            await client.PostAsync(path, content);
        }

        private async Task<HttpClient> GetHttpClient()
        {
            var handler = new WebRequestHandler();
            var certClient = await GetCertificate("", "");

            if (certClient != null)
            {
                handler.ClientCertificates.Add(certClient);
            }
            handler.ServerCertificateValidationCallback = ValidateServerCertificate;

            var client = new HttpClient(handler) { BaseAddress = baseAddress };

            foreach (var item in Headers ?? Enumerable.Empty<KeyValuePair<string, string>>())
            {
                client.DefaultRequestHeaders.Add(item.Key, item.Value);
            }
            client.Timeout = DefaultTimeout;
            return client;
        }

        private static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // TODO
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            // Do not allow this client to communicate with unauthenticated servers.
            return false;
        }

        private static async Task<X509Certificate> GetCertificate(string secret, string keyVaultUri)
        {
            var serviceTokenProvider = new AzureServiceTokenProvider();
            var keyVaultClient = new KeyVaultClient(
                new KeyVaultClient.AuthenticationCallback(serviceTokenProvider.KeyVaultTokenCallback)
            );
            var secretUri = $"${keyVaultUri}/Secrets/{secret}";

            var secretValue = await keyVaultClient.GetSecretAsync(secretUri);

            //return secretValue.Value;
            return null;
        }
    }
}
