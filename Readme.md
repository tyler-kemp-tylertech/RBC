# Release Branch Creator

This tool will go through all of the repos listed in the repos.json file
and cut a release branch for them.
This process includes 
    - Asking for the date of push to prod.
    - Making http calls to Github to find the name of the default branch of the repo
    - Creating a new branch based off the default branch with the name "release\MM.DD.YYYY"
    - A workflow should start for each repo automatically to push those branches to our QA environment

## Prerequisites

- .NET 8 SDK
- A Github Personal access token with repo permissions stored in an environment variable called `GITHUB_TOKEN`

## Usage 
1. Run the ReleaseBranchCreator.exe and follow the instructions 

## Notes

1. This app makes calls to all of the repos in the repos.json file to create a new release branch that is used for our push to prod process.
2. The repos.json file is copied into the build directory when running the build.ps1 script. The exe depends on it being in the same directory. 
3. If you need to manually create a release branch for a repo, edit the repos.json file in the build directory (or whereever the .exe is located) to not include that repo and then run the app
