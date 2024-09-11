# A powershell script to publish the project with release configuration and move the files to the specified directory
$project = "ReleaseBranchCreator.csproj"
$configuration = "Release"
$outputDirectory = ".\build"

# publish the project
dotnet publish $project -c $configuration

# create the output directory if it doesn't exist
if (-not (Test-Path $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory
}

# move the files to the output directory
Copy-Item -Path ".\bin\$configuration\net8.0\publish\*" -Destination $outputDirectory -Recurse -Force
Copy-Item -Path ".\configs.json" -Destination $outputDirectory -Force