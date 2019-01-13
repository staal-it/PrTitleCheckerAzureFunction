using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace PrTitleChecker
{
    public static class PrTitleChecker
    {
        private static string accountName = "<AccountName>";   // Account name
        private static string projectName = "<ProjectName>";   // Project name
        private static string repositoryName = "<RepositoryName>";   // Repository name
       
        private static string pat = "<YOUR-KEY>";

        [FunctionName("PrTitleChecker")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            try
            {
                // Get request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);
                
                log.LogDebug("Data Received: {0}", requestBody);

                // Get the pull request id
                int pullRequestId;
                if (!Int32.TryParse(data.resource.pullRequestId.ToString(), out pullRequestId))
                {
                    log.LogError("Failed to parse the pull request id from the service hooks payload.");
                };

                // Get the pull request title
                string pullRequestTitle = data.resource.title;

                log.LogInformation("Service Hook Received for PR: " + pullRequestId + " " + pullRequestTitle);
                
                PostStatusOnPullRequest(pullRequestId, ComputeStatus(pullRequestTitle));

                return new OkObjectResult("Ok");
            }
            catch (Exception ex)
            {
                log.LogError(ex.ToString());
                return new BadRequestObjectResult(ex.Message);
            }
        }

        private static void PostStatusOnPullRequest(int pullRequestId, string status)
        {
            string Url = string.Format(
                @"https://dev.azure.com/{0}/{1}/_apis/git/repositories/{2}/pullrequests/{3}/statuses?api-version=4.1-preview.1",
                accountName,
                projectName,
                repositoryName,
                pullRequestId);

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(
                        ASCIIEncoding.ASCII.GetBytes(
                        string.Format("{0}:{1}", "", pat))));

                var method = new HttpMethod("POST");
                var request = new HttpRequestMessage(method, Url)
                {
                    Content = new StringContent(status, Encoding.UTF8, "application/json")
                };

                using (HttpResponseMessage response = client.SendAsync(request).Result)
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        private static string ComputeStatus(string pullRequestTitle)
        {
            string state = "succeeded";
            string description = "Ready for review";

            if (!pullRequestTitle.ToLower().StartsWith("[abc]"))
            {
                state = "pending";
                description = "PR Title incorrect";
            }

            return JsonConvert.SerializeObject(
                new
                {
                    State = state,
                    Description = description,
                    TargetUrl = "http://visualstudio.microsoft.com",

                    Context = new
                    {
                        Name = "pr-title-checker",
                        Genre = "pr-azure-function-ci"
                    }
                });
        }
    }
}
