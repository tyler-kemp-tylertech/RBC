using System.Diagnostics;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Newtonsoft.Json.Linq;

namespace ReleaseBranchCreator
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("This tool will create release branches for Tyler Payments and Enterprise Payments repos and trigger app deployments.");
                Console.WriteLine("For additional deployment information refer to https://confl.tylertech.com/display/TES/Release+and+Hotfix+branch+creation+and+deployment");

                string currentPath = Directory.GetCurrentDirectory();
                string configsPath = Path.Combine(currentPath, "configs.json");

                if (!File.Exists(configsPath))
                {
                    Console.WriteLine("ERROR: configs.json not found!");
                    return;
                }

                var configs = JArray.Parse(File.ReadAllText(configsPath));

                // Get release date
                Console.Write("Enter the date of push to prod (Ex: 04.07.2022): ");
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

                // Move up 2 directories where the repos should be located
                Directory.SetCurrentDirectory(Path.Combine(currentPath, "..", ".."));
                string? accessToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
                if (string.IsNullOrEmpty(accessToken))
                {
                    Console.WriteLine("ERROR: Github token not found in environment variables!");
                    return;
                }

                foreach (var config in configs)
                {
                    CreateRelease(config, releaseDate, accessToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }
        }

        static void CreateRelease(JToken config, string releaseDate, string accessToken)
        {
            string? repositoryPath = config["repository"]?.ToString();
            string? defaultBranch = config["defaultbranch"]?.ToString();

            if (string.IsNullOrEmpty(repositoryPath))
            {
                Console.WriteLine("ERROR: could not read repository from config!");
                return;
            }

            if (string.IsNullOrEmpty(defaultBranch))
            {
                Console.WriteLine("ERROR: could not read default branch from config!");
                return;
            }

            Console.WriteLine($"Cutting release for {repositoryPath}...");
            if (Directory.Exists(repositoryPath))
            {
                using (var repo = new Repository(repositoryPath))
                {
                    // Stash changes
                    try
                    {
                        Commands.Stage(repo, "*");
                        repo.Stashes.Add(repo.Config.BuildSignature(DateTimeOffset.Now), "Stashing changes");
                        Console.WriteLine("Changes stashed successfully.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error stashing changes: {ex.Message}");
                        return;
                    }

                    // Checkout the default branch
                    try
                    {
                        Commands.Checkout(repo, defaultBranch);
                        Console.WriteLine($"Checked out to {defaultBranch} branch.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error checking out to {defaultBranch} branch: {ex.Message}");
                        return;
                    }

                    // Pull latest changes
                    try
                    {
                        var signature = repo.Config.BuildSignature(DateTimeOffset.Now);
                        var credentialsProvider = new CredentialsHandler(
                            (url, usernameFromUrl, types) =>
                                new UsernamePasswordCredentials
                                {
                                    Username = accessToken,
                                    Password = string.Empty
                                }
                        );

                        var pullOptions = new PullOptions
                        {
                            FetchOptions = new FetchOptions
                            {
                                CredentialsProvider = credentialsProvider
                            }
                        };

                        Commands.Pull(repo, signature, pullOptions);
                        Console.WriteLine("Pulled latest changes successfully.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error pulling latest changes: {ex.Message}");
                        return;
                    }

                    // Create release branch
                    string releaseBranch = $"TESTING/{releaseDate}";
                    try
                    {
                        // check if the release branch already exists
                        if (repo.Branches[releaseBranch] != null)
                        {
                            Console.WriteLine($"Release branch {releaseBranch} already exists for {repositoryPath}...");
                        }
                        else
                        {
                            repo.CreateBranch(releaseBranch);
                            Console.WriteLine($"Created release branch: {releaseBranch}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating release branch {releaseBranch}: {ex.Message}");
                        return;
                    }

                    // Checkout the release branch
                    try
                    {
                        Commands.Checkout(repo, releaseBranch);
                        Console.WriteLine($"Checked out to release branch: {releaseBranch}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error checking out to release branch {releaseBranch}: {ex.Message}");
                        return;
                    }

                    // Push release branch to remote
                    try
                    {
                        var remote = repo.Network.Remotes["origin"];
                        var pushOptions = new PushOptions
                        {
                            CredentialsProvider = new CredentialsHandler(
                                (url, usernameFromUrl, types) =>
                                    new UsernamePasswordCredentials
                                    {
                                        Username = accessToken,
                                        Password = string.Empty
                                    }
                            )
                        };

                        repo.Network.Push(remote, $"refs/heads/{releaseBranch}", pushOptions);
                        Console.WriteLine($"Pushed release branch {releaseBranch} to remote.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error pushing release branch {releaseBranch} to remote: {ex.Message}");
                        return;
                    }

                    // Open browser to verify branches
                    string url = $"https://github.com/tyler-technologies/{repositoryPath}/compare/{defaultBranch}...{releaseBranch}?expand=1";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });

                    Console.WriteLine($"Successfully created release branch for {repositoryPath}...");
                }
            }
            else
            {
                Console.WriteLine($"ERROR: {repositoryPath} not found!!");
            }
        }
    }
}