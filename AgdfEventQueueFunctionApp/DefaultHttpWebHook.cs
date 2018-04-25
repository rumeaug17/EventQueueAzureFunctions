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
        private readonly string basicUser;
        private readonly string basicPasswd;
        private readonly AsyncLazy<HttpClient> lazyClient;
        private readonly AsyncLazy<X509Certificate> lazyCertificate;

        private string AuthToken(string user, string passwd) => Convert.ToBase64String(
            Encoding.Default.GetBytes(s: $"{user}:{passwd}"));

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

        private static TimeSpan DefaultTimeout => new TimeSpan(10 * 1000);

        public DefaultHttpWebHook(Uri baseAddress, string path, string basicUser = null, string basicPasswd = null, bool withMutualTls = false)
        {
            this.baseAddress = baseAddress;
            this.path = path;
            this.basicUser = basicUser;
            this.basicPasswd = basicPasswd;
            lazyCertificate = new AsyncLazy<X509Certificate>(async () => await GetCertificate(withMutualTls));
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
            var certClient = await lazyCertificate;

            if (certClient != null)
            {
                handler.ClientCertificates.Add(certClient);
                handler.AuthenticationLevel = AuthenticationLevel.MutualAuthRequired;
                handler.ServerCertificateValidationCallback = ValidateServerCertificate;
            }

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
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            // Do not allow this client to communicate with unauthenticated servers.
            return false;
        }

        private static async Task<X509Certificate2> GetCertificate(bool withMutualTls)
        {
            if (withMutualTls)
            {
                var serviceTokenProvider = new AzureServiceTokenProvider();
                var keyVaultClient = new KeyVaultClient(
                    new KeyVaultClient.AuthenticationCallback(serviceTokenProvider.KeyVaultTokenCallback)
                );

                var keyVaultUri = System.Environment.GetEnvironmentVariable("KEYVAULT_Url", EnvironmentVariableTarget.Process);

                var secretValue = await keyVaultClient.GetCertificateAsync(keyVaultUri, "default-dummy-certificate");
                return new X509Certificate2(secretValue.Cer);
            }
            return null;

        }
    }
}
