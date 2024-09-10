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
- A Github Personal access token with repo permissions stored in an environment variable called "GITHUB_TOKEN"

## Installation

1. Clone the repository:
    ```sh
    git clone https://github.com/tyler-kemp-tylertech/RBC 
    ```
2. Navigate to the project directory:
    ```sh
    cd rbc
    ```
3. Build:
    ```sh
    dotnet build
    ```

## Notes

1. This app assumes that it will be run 2 directories down from where the repos are located. 
Typically this should be in the payments-dev-compose/release-scripts directory
