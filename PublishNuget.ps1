
[CmdletBinding()]
Param(
    [string]$BuildNumber,
    [string]$Artifactory,
    [string]$ApiKey
)


if (Test-Path 'output') {
  Remove-Item 'output' -Force -Recurse
}

dotnet restore
dotnet build --configuration Release -p:Deterministic=true -p:BuildNumber=$BuildNumber
dotnet pack OpenTelemetry.proj --configuration Release --no-build --output "output" 

Get-ChildItem -Path "output" -Filter "*.nupkg" | 
Foreach-Object {
  $packageId = $_ -replace '.nupkg' -replace '\.([0-9]+)\.([0-9]+)\.([0-9]+)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+)?$'
  $path = Join-Path -Path "output" -ChildPath $_
  # Write-Output "PackageId $packageId " "Nuget file $path"

  nuget push $path -Source $Artifactory/$packageId/$packageId -ApiKey $ApiKey
}
