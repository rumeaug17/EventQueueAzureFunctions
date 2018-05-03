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
        private readonly string basicPasswd;
        private readonly string basicUser;
        private readonly AsyncLazy<X509Certificate> lazyCertificate;
        private readonly AsyncLazy<HttpClient> lazyClient;
        private Uri baseAddress;
        private string path;

        public DefaultHttpWebHook(Uri baseAddress, string path, string basicUser = null, string basicPasswd = null, bool withMutualTls = false)
        {
            this.baseAddress = baseAddress;
            this.path = path;
            this.basicUser = basicUser;
            this.basicPasswd = basicPasswd;
            lazyCertificate = new AsyncLazy<X509Certificate>(async () => await GetCertificate(withMutualTls));
            lazyClient = new AsyncLazy<HttpClient>(async () => await GetHttpClient());
        }

        private static TimeSpan DefaultTimeout => new TimeSpan(10 * 1000);

        private List<KeyValuePair<string, string>> Headers
        {
            get
            {
                var headers = new List<KeyValuePair<string, string>> {
                    new KeyValuePair<string, string>("Accept", "application/json"),
                };

                if (basicUser != null && basicPasswd != null)
                {
                    headers.Add(new KeyValuePair<string, string>("Authorization", value: $"Basic {AuthToken(basicUser, basicPasswd)}"));
                }

                return headers;
            }
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

        private static async Task<X509Certificate2> GetCertificate(bool withMutualTls)
        {
            if (withMutualTls)
            {
                var clientId = System.Environment.GetEnvironmentVariable("KEYVAULT_clientId", EnvironmentVariableTarget.Process);
                var clientSecret = System.Environment.GetEnvironmentVariable("KEYVAULT_clientSecret", EnvironmentVariableTarget.Process);
                var secretUrl = System.Environment.GetEnvironmentVariable("KEYVAULT_secretUrl", EnvironmentVariableTarget.Process);

                var helper = new KeyVaultHelper(clientId, clientSecret);
                var secret = await helper.getSecret(secretUrl);
                var pfxBytes = Convert.FromBase64String(secret.Value);

                var secretValue = KeyVaultHelper.GetX509Certificate2(pfxBytes);
                return secretValue;
            }

            return null;
        }

        private string AuthToken(string user, string passwd) => Convert.ToBase64String(
                                                            Encoding.Default.GetBytes(s: $"{user}:{passwd}"));
        private async Task<HttpClient> GetHttpClient()
        {
            var handler = new WebRequestHandler();
            var certClient = await lazyCertificate;

            if (certClient != null)
            {
                handler.ClientCertificates.Add(certClient);
                handler.AuthenticationLevel = AuthenticationLevel.MutualAuthRequired;
            }

            var client = new HttpClient(handler) { BaseAddress = baseAddress };

            foreach (var item in Headers ?? Enumerable.Empty<KeyValuePair<string, string>>())
            {
                client.DefaultRequestHeaders.Add(item.Key, item.Value);
            }
            client.Timeout = DefaultTimeout;
            return client;
        }
    }
}