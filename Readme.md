# Release Branch Creator

This tool will go through all of the TP/PEP repos listed in the config.json file
and cut a release branch for them.
This process includes 
    - Stashing local changes
    - Checking out the main/master branch
    - Pulling the latest changes
    - Creating the release branch with the given release date
    - Pushing the branch up to GitHub
    - A workflow should start for each repo automatically to push those branches to our QA environment

## Prerequisites

- .NET 8 SDK
- A Github Personal access token with repo permissions stored in an environment variable called `GITHUB_TOKEN`

## Usage 
1. Run the ReleaseBranchCreator.exe from the shortcut in the release-scripts directory and follow the instructions 

## Notes

1. This app uses the configs.json file to locate all of the repos to cut branches for. 
2. The configs.json file is copied into the build directory when running the build.ps1 script
3. If you need to manually create a release branch for a repo, edit the configs.json file in the build directory and then run the app

