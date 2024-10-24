using System.Diagnostics;
using System.Net.Http.Headers;
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
                List<string> branchCreationFailures = [];

                if (!File.Exists(repoConfig))
                {
                    Console.WriteLine("ERROR: repos.json not found!");
                    return;
                }

                var config = JObject.Parse(File.ReadAllText(repoConfig));
                var ownerName = config["ownerName"]?.ToString() ?? string.Empty;
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
                Console.Write("Enter the date of push to prod (Ex: 04.07.2022):");
                string? releaseDate = Console.ReadLine();

                if (string.IsNullOrEmpty(releaseDate))
                {
                    Console.WriteLine("ERROR: Release date is required!");
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

                    Console.WriteLine($"Creating release branch for {repoName}...");
                    var newBranchName = $"release/{releaseDate}";

                    string defaultBranch = await GetDefaultBranch(ownerName, repoName, accessToken);
                    if (string.IsNullOrEmpty(defaultBranch))
                    {
                        Console.WriteLine($"ERROR: Default branch not found for {repoName}!");
                        branchCreationFailures.Add(repoName);
                        continue;
                    }

                    string defaultBranchSha = await GetBranchSha(ownerName, repoName, defaultBranch, accessToken);
                    if (string.IsNullOrEmpty(defaultBranchSha))
                    {
                        Console.WriteLine($"ERROR: Default branch sha not found for {repoName}/{defaultBranch}!");
                        branchCreationFailures.Add(repoName);
                        continue;
                    }

                    var successfullyCreatedBranch = await CreateBranch(ownerName, repoName, newBranchName, defaultBranchSha, accessToken);
                    if (!successfullyCreatedBranch)
                    {
                        Console.WriteLine($"ERROR: Branch creation failed for {repoName}!");
                        branchCreationFailures.Add(repoName);
                        continue;
                    }

                    // Open browser to verify branches
                    string url = $"https://github.com/tyler-technologies/{repoName}/compare/{defaultBranch}...{newBranchName}?expand=1";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });

                    Console.WriteLine($"Branch {newBranchName} created successfully for {repoName}.");
                }

                if (branchCreationFailures.Count != 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Failed to create branches for the following repos:");
                    Console.WriteLine("Try again or manually create the branches for these repos.");
                    foreach (var repo in branchCreationFailures)
                    {
                        Console.WriteLine(repo);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }
        }

        private static async Task<string> GetDefaultBranch(string ownerName, string repoName, string accessToken)
        {
            string url = $"https://api.github.com/repos/{ownerName}/{repoName}";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ReleaseBranchCreator", "1.0"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", accessToken);

                try
                {
                    var response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync();
                    var repoInfo = JObject.Parse(content);

                    return repoInfo?["default_branch"]?.ToString() ?? string.Empty;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"ERROR while getting default branch: {e.Message}");
                    return string.Empty;
                }
            }
        }

        private static async Task<string> GetBranchSha(string ownerName, string repoName, string branchName, string accessToken)
        {
            string url = $"https://api.github.com/repos/{ownerName}/{repoName}/git/refs/heads/{branchName}";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ReleaseBranchCreator", "1.0"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", accessToken);

                try
                {
                    var response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync();
                    var branchInfo = JObject.Parse(content);

                    return branchInfo?["object"]?["sha"]?.ToString() ?? string.Empty;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"ERROR while getting the branch sha: {e.Message}");
                    return string.Empty;
                }
            }
        }

        private static async Task<bool> CreateBranch(string ownerName, string repoName, string newBranchName, string branchSha, string accessToken)
        {
            string url = $"https://api.github.com/repos/{ownerName}/{repoName}/git/refs";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ReleaseBranchCreator", "1.0"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", accessToken);

                var requestBody = new Dictionary<string, string>
                {
                    { "ref", $"refs/heads/{newBranchName}" },
                    { "sha", branchSha }
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var responseContent = string.Empty;
                try
                {
                    var response = await client.PostAsync(url, content);
                    responseContent = await response.Content.ReadAsStringAsync();

                    response.EnsureSuccessStatusCode();
                    return true;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"ERROR while creating the branch: {e.Message}");
                    Console.WriteLine($"Response: {responseContent}");
                    return false;
                }
            }
        }
    }
}