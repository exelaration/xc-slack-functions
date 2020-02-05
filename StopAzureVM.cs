using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace NextUpSolutions
{
    public static class StopAzureVM
    {
        [FunctionName("StopAzureVM")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            dynamic data = await req.ReadFormAsync();
            string text = data["text"];
            string responseUrl = data["response_url"];

            if (text != null && responseUrl != null)
            {
                AuthenticateAndStopAsync(text, responseUrl);
                return new OkObjectResult($"Stopping VM {text}");
            }
            else
            {
                return new BadRequestObjectResult("Missing machine name or response URL.");
            }
        }

        private static async Task AuthenticateAndStopAsync(string machineName, string responseUrl)
        {
            try
            {
                var azure = await Task.Run(() => GetAzureSubscription());
                await StopVirtualMachine(azure, "XC", machineName);
                await SendSlackResponse($"VM {machineName} stopped", responseUrl);
            }
            catch
            {
                await SendSlackResponse($"Error stopping VM: {machineName}", responseUrl);
            }
        }

        private static IAzure GetAzureSubscription()
        {
            var credentials = GetCredentials();
            
            return Azure
                .Configure()
                .Authenticate(credentials)
                .WithDefaultSubscription();
        }

        private static AzureCredentials GetCredentials()
        {
            return SdkContext.AzureCredentialsFactory.FromSystemAssignedManagedServiceIdentity(MSIResourceType.AppService, AzureEnvironment.AzureGlobalCloud);
        }

        private static async Task StopVirtualMachine(IAzure azure, string groupName, string vmName)
        {
            await azure.VirtualMachines.DeallocateAsync(groupName, vmName);
        }

        private static async Task SendSlackResponse(string text, string responseUrl)
        {
            var client = new HttpClient();
            var slackResponse = new SlackResponse() { Text = text, ResponseType = "ephemeral"};

            await client.PostAsJsonAsync(responseUrl, slackResponse);
        }

        private class SlackResponse
        {
            [JsonProperty("text")]
            public string Text { get; set; }

            [JsonProperty("response_type")]
            public string ResponseType { get; set; }
        }
    }
}
