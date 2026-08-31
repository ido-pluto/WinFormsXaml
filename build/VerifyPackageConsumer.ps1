[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $PackageVersion,

    [string] $PackageDirectory,

    [string] $DotNetPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

if ($PackageVersion -notmatch
    '^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?$') {
    throw 'PackageVersion must be a three-part NuGet version such as 0.1.0 or 0.1.0-preview.1.'
}

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$consumerProject =
    Join-Path $repositoryRoot 'packaging/consumer/WinFormsXaml.PackageConsumer.csproj'
$consumerRoot = Join-Path $repositoryRoot 'artifacts/package/consumer'
$packagesPath = Join-Path $consumerRoot 'packages'
$intermediatePath = Join-Path $consumerRoot 'obj'
$outputPath = Join-Path $consumerRoot 'bin'
$nugetConfigPath = Join-Path $consumerRoot 'NuGet.Config'
$schemaFixturePaths = @(
    Get-ChildItem `
        -LiteralPath (Join-Path $repositoryRoot 'samples') `
        -Filter '*.xml' `
        -Recurse |
        Where-Object { -not $_.PSIsContainer } |
        Sort-Object FullName |
        ForEach-Object { $_.FullName }
)

. (Join-Path $PSScriptRoot 'SchemaValidation.ps1')

function Get-RequiredCommandPath {
    param([Parameter(Mandatory = $true)][string] $Name)

    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw "Required command '$Name' was not found on PATH."
    }

    return $command.Source
}

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [string[]] $Arguments = @()
    )

    Write-Host "> $FilePath $($Arguments -join ' ')"
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath"
    }
}

if ([String]::IsNullOrEmpty($DotNetPath)) {
    $DotNetPath = Get-RequiredCommandPath 'dotnet'
}
elseif (-not (Test-Path -LiteralPath $DotNetPath -PathType Leaf)) {
    throw "dotnet was not found at '$DotNetPath'."
}

if ([String]::IsNullOrEmpty($PackageDirectory)) {
    $PackageDirectory = Join-Path $repositoryRoot 'artifacts/package/output'
}
$PackageDirectory = [IO.Path]::GetFullPath($PackageDirectory)

$localPackage = Join-Path $PackageDirectory "WinFormsXaml.$PackageVersion.nupkg"
if (-not (Test-Path -LiteralPath $localPackage -PathType Leaf)) {
    throw "The expected local package does not exist: '$localPackage'."
}
if ((Get-Item -LiteralPath $localPackage).Length -eq 0) {
    throw "The expected local package is empty: '$localPackage'."
}

$expectedConsumerRoot =
    [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/package/consumer'))
if ([IO.Path]::GetFullPath($consumerRoot) -ne $expectedConsumerRoot) {
    throw "Refusing to clean unexpected consumer root '$consumerRoot'."
}

if (Test-Path -LiteralPath $consumerRoot) {
    Remove-Item -LiteralPath $consumerRoot -Recurse -Force
}
[void] (New-Item -ItemType Directory -Path $consumerRoot -Force)

$escapedPackageDirectory =
    [Security.SecurityElement]::Escape($PackageDirectory)
$nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-winformsxaml" value="$escapedPackageDirectory" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local-winformsxaml">
      <package pattern="WinFormsXaml" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="Microsoft.NETFramework.ReferenceAssemblies*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
"@
[IO.File]::WriteAllText(
    $nugetConfigPath,
    $nugetConfig,
    (New-Object System.Text.UTF8Encoding -ArgumentList $false))

$commonProperties = @(
    "-p:WinFormsXamlPackageVersion=$PackageVersion",
    "-p:RestorePackagesPath=$packagesPath",
    "-p:BaseIntermediateOutputPath=$($intermediatePath.TrimEnd([char[]] '\/') + [IO.Path]::DirectorySeparatorChar)",
    "-p:MSBuildProjectExtensionsPath=$($intermediatePath.TrimEnd([char[]] '\/') + [IO.Path]::DirectorySeparatorChar)"
)

Write-Host "Restoring an isolated consumer from $localPackage..."
Invoke-CheckedCommand $DotNetPath (@(
    'restore',
    $consumerProject,
    '--configfile',
    $nugetConfigPath,
    '--no-cache'
) + $commonProperties)

$restoredPackageRoot =
    Join-Path $packagesPath ("winformsxaml/" + $PackageVersion)
$packagedSymbols =
    Join-Path $restoredPackageRoot 'lib/net20/WinFormsXaml.pdb'

if (-not (Test-Path -LiteralPath $packagedSymbols -PathType Leaf)) {
    throw "The restored package is missing its .NET Framework 2.0 symbols: '$packagedSymbols'."
}
if ((Get-Item -LiteralPath $packagedSymbols).Length -eq 0) {
    throw "The restored package contains an empty .NET Framework 2.0 symbols file: '$packagedSymbols'."
}

$packagedSchemas = @(
    (Join-Path $restoredPackageRoot 'content/WinFormsXaml.xsd'),
    (Join-Path $restoredPackageRoot 'contentFiles/any/any/WinFormsXaml.xsd'),
    (Join-Path $restoredPackageRoot 'schemas/WinFormsXaml.xsd')
)

foreach ($packagedSchema in $packagedSchemas) {
    if (-not (Test-Path -LiteralPath $packagedSchema -PathType Leaf)) {
        throw "The restored package is missing its XML editor schema: '$packagedSchema'."
    }
    if ((Get-Item -LiteralPath $packagedSchema).Length -eq 0) {
        throw "The restored package contains an empty XML editor schema: '$packagedSchema'."
    }

    Assert-WinFormsXamlSchemaContract `
        -SchemaPath $packagedSchema `
        -FixturePaths $schemaFixturePaths
}

$restoredNuspec = Join-Path $restoredPackageRoot 'winformsxaml.nuspec'
if (-not (Test-Path -LiteralPath $restoredNuspec -PathType Leaf)) {
    throw "The restored package nuspec was not found at '$restoredNuspec'."
}

[xml] $restoredNuspecXml = Get-Content -LiteralPath $restoredNuspec -Raw
$schemaContentDeclarations = @(
    $restoredNuspecXml.SelectNodes(
        "/*[local-name()='package']/*[local-name()='metadata']" +
        "/*[local-name()='contentFiles']/*[local-name()='files']") |
        Where-Object {
            $_.GetAttribute('include').Replace('\', '/') -eq
                'any/any/WinFormsXaml.xsd'
        }
)
if ($schemaContentDeclarations.Count -ne 1) {
    throw 'The restored package must declare exactly one PackageReference schema content file.'
}

$schemaContentDeclaration = $schemaContentDeclarations[0]
if ($schemaContentDeclaration.GetAttribute('buildAction') -cne 'None' -or
    $schemaContentDeclaration.GetAttribute('copyToOutput') -cne 'false' -or
    $schemaContentDeclaration.GetAttribute('flatten') -cne 'true') {
    throw 'The restored PackageReference schema must declare buildAction=None, copyToOutput=false, and flatten=true.'
}

Write-Host 'Compiling the isolated package consumer...'
Invoke-CheckedCommand $DotNetPath (@(
    'build',
    $consumerProject,
    '--configuration',
    'Release',
    '--no-restore',
    '--output',
    $outputPath
) + $commonProperties)

$consumerAssembly = Join-Path $outputPath 'WinFormsXaml.PackageConsumer.exe'
$packagedAssembly = Join-Path $outputPath 'WinFormsXaml.dll'
foreach ($requiredFile in @($consumerAssembly, $packagedAssembly)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Clean package consumer output was not created: '$requiredFile'."
    }
    if ((Get-Item -LiteralPath $requiredFile).Length -eq 0) {
        throw "Clean package consumer output is empty: '$requiredFile'."
    }
}

$unexpectedOutputSchemas = @(
    Get-ChildItem -LiteralPath $outputPath -Filter 'WinFormsXaml.xsd' -File -Recurse
)
if ($unexpectedOutputSchemas.Count -ne 0) {
    throw "The PackageReference schema must not be copied to consumer output: '$($unexpectedOutputSchemas[0].FullName)'."
}

Write-Host 'Running the isolated package consumer...'
Invoke-CheckedCommand $consumerAssembly

Write-Host "Clean consumer compile and runtime smoke test succeeded against $localPackage."
