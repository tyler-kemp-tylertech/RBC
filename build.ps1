# A powershell script to publish the project with release configuration and move the files to the specified directory
$project = "ReleaseBranchCreator.csproj"
$configuration = "Release"
$outputDirectory = ".\build"

# publish the project for win-x64
dotnet publish $project -c $configuration -r win-x64 --self-contained true

# create the output directory if it doesn't exist
if (-not (Test-Path $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory
}

# move the files to the output directory
Copy-Item -Path ".\bin\$configuration\net8.0\win-x64\publish\*" -Destination $outputDirectory -Recurse -Force
Copy-Item -Path ".\repos.json" -Destination $outputDirectory -Force

# create a shortcut for the executable
$exePath = Join-Path $PSScriptRoot -ChildPath "/build/ReleaseBranchCreator.exe"
$shortcutPath = Join-Path (Split-Path -Path $PSScriptRoot -Parent) -ChildPath "ReleaseBranchCreator.lnk"

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = (Split-Path -Path $exePath -Parent)
$shortcut.Save()