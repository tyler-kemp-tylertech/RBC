using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ReleaseBranchCreator
{
    class Program
    {
        static void Main(string[] args)
        {
            Task.Run(CreateReleaseBranches).GetAwaiter().GetResult();
        }

        private static async Task CreateReleaseBranches()
        {
            try
            {
                Console.WriteLine("This tool will create release branches for Tyler Payments and Enterprise Payments repos and trigger app deployments.");
                Console.WriteLine("For additional deployment information refer to https://confl.tylertech.com/display/TES/Release+and+Hotfix+branch+creation+and+deployment");

                string currentPath = Directory.GetCurrentDirectory();
                string repoConfig = Path.Combine(currentPath, "repos.json");

                if (!File.Exists(repoConfig))
                {
                    Console.WriteLine("ERROR: repos.json not found!");
                    return;
                }

                var config = JObject.Parse(File.ReadAllText(repoConfig));
                string ownerName = config["ownerName"]?.ToString() ?? string.Empty;
                var repositories = config["repositories"]?.ToObject<JArray>();

                if (string.IsNullOrEmpty(ownerName))
                {
                    Console.WriteLine("ERROR: Missing Owner Name in configuration.");
                    return;
                }

                if (repositories == null || repositories.Count == 0)
                {
                    Console.WriteLine("ERROR: No repositories found in configuration.");
                    return;
                }

                // Get release date
                Console.Write("Enter the push to prod date (Ex. 01.15.2024): ");
                string? releaseDate = Console.ReadLine();

                if (string.IsNullOrEmpty(releaseDate))
                {
                    Console.WriteLine("ERROR: Release date is required.");
                    return;
                }

                if (!DateTime.TryParse(releaseDate, out _))
                {
                    Console.WriteLine("ERROR: Invalid date format! Please use MM.DD.YYYY");
                    return;
                }

                string? accessToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
                if (string.IsNullOrEmpty(accessToken))
                {
                    Console.WriteLine("ERROR: Github token not found in environment variables!");
                    return;
                }

                foreach (var repo in repositories)
                {
                    string repoName = repo["repository"]?.ToString() ?? string.Empty;

                    Console.WriteLine($"Triggering cut release branch workflow for {repoName}...");
                    await TriggerCutReleaseBranchWorkflow(ownerName, repoName, releaseDate, accessToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }
        }

        private static async Task TriggerCutReleaseBranchWorkflow(string ownerName, string repoName, string releaseDate, string accessToken)
        {
            string defaultBranch = await GetDefaultBranch(ownerName, repoName, accessToken);
            string url = $"https://api.github.com/repos/{ownerName}/{repoName}/actions/workflows/cut-release-branch.yml/dispatches";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("ReleaseBranchCreator", "1.0"));
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", accessToken);

                var payload = new
                {
                    @ref = defaultBranch,
                    inputs = new
                    {
                        branch_name = "release/" + releaseDate
                    }
                };

                var jsonPayload = JsonConvert.SerializeObject(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                try
                {
                    var response = await client.PostAsync(url, content);
                    var responseContent = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Workflow triggered successfully.");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to trigger workflow: {response.StatusCode} - {response.ReasonPhrase}");
                        Console.WriteLine($"Response content: {responseContent}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: {ex.Message}");
                }
            }
        }

        private static async Task<string> GetDefaultBranch(string ownerName, string repoName, string accessToken)
        {
            string url = $"https://api.github.com/repos/{ownerName}/{repoName}";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("ReleaseBranchCreator", "1.0"));
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", accessToken);

                try
                {
                    var response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var jsonResponse = await response.Content.ReadAsStringAsync();
                        var repoDetails = JsonConvert.DeserializeObject<JObject>(jsonResponse);
                        return repoDetails?["default_branch"]?.ToString() ?? string.Empty;
                    }
                    else
                    {
                        Console.WriteLine($"Failed to get repository details: {response.StatusCode} - {response.ReasonPhrase}");
                        return string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: {ex.Message}");
                    return string.Empty;
                }
            }
        }
    }
}