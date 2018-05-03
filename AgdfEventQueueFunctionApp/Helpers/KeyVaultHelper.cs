using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace AgdfEventQueueFunctionApp
{
    public class KeyVaultHelper
    {
        private string clientId;

        public string clientSecret;

        public KeyVaultHelper(string clientId, string clientSecret)
        {
            this.clientId = clientId;
            this.clientSecret = clientSecret;
        }

        //the method that will be provided to the KeyVaultClient
        public async Task<string> GetToken(string authority, string resource, string scope)
        {
            var authContext = new AuthenticationContext(authority);
            ClientCredential clientCred = new ClientCredential(this.clientId, this.clientSecret);
            AuthenticationResult result = await authContext.AcquireTokenAsync(resource, clientCred);

            if (result == null)
                throw new InvalidOperationException("Failed to obtain the JWT token");

            return result.AccessToken;
        }

        public async Task<CertificateBundle> getCertificate(string certificateUri)
        {
            KeyVaultClient kv = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(GetToken));
            return await kv.GetCertificateAsync(certificateUri);
        }

        public async Task<KeyBundle> getKey(string secretUri)
        {
            KeyVaultClient kv = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(GetToken));
            return await kv.GetKeyAsync(secretUri);
        }

        public async Task<SecretBundle> getSecret(string secretUri)
        {
            KeyVaultClient kv = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(GetToken));
            return await kv.GetSecretAsync(secretUri);
        }

        public static X509Certificate2 GetX509Certificate2(byte[] pfxBytes)
        {
            try
            {
                return new X509Certificate2(pfxBytes, string.Empty);
            }
            catch
            {
                return null;
            }
        }
    }
}