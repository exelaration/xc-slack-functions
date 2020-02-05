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
    public static class StartAzureVM
    {
        [FunctionName("StartAzureVM")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            dynamic data = await req.ReadFormAsync();
            string text = data["text"];
            string responseUrl = data["response_url"];

            if (text != null && responseUrl != null)
            {
                AuthenticateAndStartAsync(text, responseUrl);
                return new OkObjectResult($"Starting VM {text}");
            }
            else
            {
                return new BadRequestObjectResult("Missing machine name or response URL.");
            }
        }

        private static async Task AuthenticateAndStartAsync(string machineName, string responseUrl)
        {
            try
            {
                var azure = await Task.Run(() => GetAzureSubscription());
                await StartVirtualMachine(azure, "XC", machineName);
                await SendSlackResponse($"VM {machineName} started", responseUrl);
            }
            catch
            {
                await SendSlackResponse($"Error starting VM: {machineName}", responseUrl);
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

        private static async Task StartVirtualMachine(IAzure azure, string groupName, string vmName)
        {
            await azure.VirtualMachines.StartAsync(groupName, vmName);
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
