$ErrorActionPreference = "Stop"

Write-Host "Publishing NovaIsland.App..."
dotnet publish src\NovaIsland.App\NovaIsland.App.csproj -c Release -r win-x64 --self-contained -o publish_output

Write-Host "Creating Velopack Installer..."
$runNumber = $env:GITHUB_RUN_NUMBER
if ([string]::IsNullOrWhiteSpace($runNumber)) {
    $runNumber = "0"
}
$version = "1.0.$runNumber"
Write-Host "Packaging version: $version"

$vpkPath = "$env:USERPROFILE\.dotnet\tools\vpk.exe"
& $vpkPath pack -u NovaIsland -v $version -p publish_output -e NovaIsland.App.exe -o Releases

Write-Host "Installer created successfully in the 'Releases' folder."