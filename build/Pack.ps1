[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $PackageVersion,

    [string] $NuGetPath,

    [string] $DotNetPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$minimumNuGetVersion = [Version] '5.10.0'
$packageVersionPattern =
    '^(?<major>[0-9]+)\.(?<minor>[0-9]+)\.(?<patch>[0-9]+)(-[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?$'
$packageVersionMatch = [Regex]::Match($PackageVersion, $packageVersionPattern)

if (-not $packageVersionMatch.Success) {
    throw 'PackageVersion must be a three-part NuGet version such as 0.1.0 or 0.1.0-preview.1.'
}

$numericParts = @(
    $packageVersionMatch.Groups['major'].Value,
    $packageVersionMatch.Groups['minor'].Value,
    $packageVersionMatch.Groups['patch'].Value
)

foreach ($numericPart in $numericParts) {
    $parsedPart = [UInt64]::Parse(
        $numericPart,
        [Globalization.CultureInfo]::InvariantCulture)

    if ($parsedPart -gt 65534) {
        throw 'Package version components must be between 0 and 65534 so they can be represented in AssemblyFileVersion.'
    }
}

$packageFileVersion =
    '{0}.{1}.{2}.0' -f $numericParts[0], $numericParts[1], $numericParts[2]
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$packageProject = Join-Path $repositoryRoot 'build/WinFormsXaml.PackageBuild.csproj'
$nuspecPath = Join-Path $repositoryRoot 'packaging/WinFormsXaml.nuspec'
$packageReadmePath = Join-Path $repositoryRoot 'packaging/PackageREADME.md'
$schemaPath = Join-Path $repositoryRoot 'schemas/WinFormsXaml.xsd'
$schemaFixturePaths = @(
    Get-ChildItem `
        -LiteralPath (Join-Path $repositoryRoot 'samples') `
        -Filter '*.xml' `
        -Recurse |
        Where-Object { -not $_.PSIsContainer } |
        Sort-Object FullName |
        ForEach-Object { $_.FullName }
)
$assemblyInfoPath =
    Join-Path $repositoryRoot 'src/WinFormsXaml/Properties/AssemblyInfo.cs'
$packageRoot = Join-Path $repositoryRoot 'artifacts/package'
$buildDirectory = Join-Path $packageRoot 'build'
$stageDirectory = Join-Path $packageRoot 'stage'
$packageOutputDirectory = Join-Path $packageRoot 'output'
$generatedAssemblyInfo = Join-Path $buildDirectory 'PackageAssemblyInfo.cs'

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

if ([String]::IsNullOrEmpty($NuGetPath)) {
    $NuGetPath = Get-RequiredCommandPath 'nuget'
}
elseif (-not (Test-Path -LiteralPath $NuGetPath -PathType Leaf)) {
    throw "NuGet CLI was not found at '$NuGetPath'."
}

$nugetHelpOutput = @(& $NuGetPath help 2>&1) -join [Environment]::NewLine
$nugetVersionMatch =
    [Regex]::Match($nugetHelpOutput, 'NuGet Version:\s*([0-9]+(?:\.[0-9]+){2,3})')

if (-not $nugetVersionMatch.Success) {
    throw "Could not determine the NuGet CLI version from '$NuGetPath'. NuGet CLI $minimumNuGetVersion or newer is required for package README support."
}

$nugetVersion = [Version] $nugetVersionMatch.Groups[1].Value
if ($nugetVersion -lt $minimumNuGetVersion) {
    throw "NuGet CLI $nugetVersion is too old; $minimumNuGetVersion or newer is required for package README support."
}

Write-Host "Using NuGet CLI $nugetVersion."

$expectedPackageRoot =
    [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/package'))
if ([IO.Path]::GetFullPath($packageRoot) -ne $expectedPackageRoot) {
    throw "Refusing to clean unexpected package root '$packageRoot'."
}

if (-not (Test-Path -LiteralPath $schemaPath -PathType Leaf) -or
    (Get-Item -LiteralPath $schemaPath).Length -eq 0) {
    throw "The WinFormsXaml editor schema was not found at '$schemaPath'."
}

Assert-WinFormsXamlSchemaContract `
    -SchemaPath $schemaPath `
    -FixturePaths $schemaFixturePaths

foreach ($directory in @(
    $buildDirectory,
    $stageDirectory,
    $packageOutputDirectory
)) {
    if (Test-Path -LiteralPath $directory) {
        Remove-Item -LiteralPath $directory -Recurse -Force
    }
}

[void] (New-Item -ItemType Directory -Path $buildDirectory -Force)
[void] (New-Item -ItemType Directory -Path (
    Join-Path $stageDirectory 'lib/net20') -Force)
[void] (New-Item -ItemType Directory -Path (
    Join-Path $stageDirectory 'content') -Force)
[void] (New-Item -ItemType Directory -Path (
    Join-Path $stageDirectory 'contentFiles/any/any') -Force)
[void] (New-Item -ItemType Directory -Path (
    Join-Path $stageDirectory 'schemas') -Force)
[void] (New-Item -ItemType Directory -Path $packageOutputDirectory -Force)

$assemblyInfo = [IO.File]::ReadAllText($assemblyInfoPath)
$fileVersionPattern = 'AssemblyFileVersion\("[^"]+"\)'
$assemblyVersionPattern = 'AssemblyVersion\("([^"]+)"\)'
$assemblyVersionMatches = [Regex]::Matches(
    $assemblyInfo,
    $assemblyVersionPattern)
if ($assemblyVersionMatches.Count -ne 1) {
    throw "Exactly one stable AssemblyVersion declaration is required in '$assemblyInfoPath'."
}
$stableAssemblyVersion = $assemblyVersionMatches[0].Groups[1].Value
if ([Regex]::Matches($assemblyInfo, $fileVersionPattern).Count -ne 1) {
    throw "Exactly one AssemblyFileVersion declaration is required in '$assemblyInfoPath'."
}
if ([Regex]::IsMatch($assemblyInfo, 'AssemblyInformationalVersion\(')) {
    throw "'$assemblyInfoPath' already declares AssemblyInformationalVersion; update the package version generation policy before packing."
}

$packageAssemblyInfo = [Regex]::Replace(
    $assemblyInfo,
    $fileVersionPattern,
    "AssemblyFileVersion(`"$packageFileVersion`")")
$packageAssemblyInfo +=
    [Environment]::NewLine +
    "[assembly: AssemblyInformationalVersion(`"$PackageVersion`")]" +
    [Environment]::NewLine
[IO.File]::WriteAllText(
    $generatedAssemblyInfo,
    $packageAssemblyInfo,
    (New-Object System.Text.UTF8Encoding -ArgumentList $false))

Write-Host "Compiling WinFormsXaml $PackageVersion for .NET Framework 2.0..."
$buildOutputPath =
    $buildDirectory.TrimEnd([char[]] '\/') + [IO.Path]::DirectorySeparatorChar
Invoke-CheckedCommand $DotNetPath @(
    'restore',
    $packageProject
)
Invoke-CheckedCommand $DotNetPath @(
    'build',
    $packageProject,
    '--configuration',
    'Release',
    '--no-restore',
    "-p:PackageAssemblyInfo=$generatedAssemblyInfo",
    "-p:OutputPath=$buildOutputPath"
)

$assemblyPath = Join-Path $buildDirectory 'WinFormsXaml.dll'
$xmlDocumentationPath = Join-Path $buildDirectory 'WinFormsXaml.xml'
$symbolsPath = Join-Path $buildDirectory 'WinFormsXaml.pdb'

foreach ($requiredFile in @(
    $assemblyPath,
    $xmlDocumentationPath,
    $symbolsPath
)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Expected package build output was not created: '$requiredFile'."
    }
    if ((Get-Item -LiteralPath $requiredFile).Length -eq 0) {
        throw "Expected package build output is empty: '$requiredFile'."
    }
}

$symbolsSignatureBytes = New-Object byte[] 24
$symbolsStream = [IO.File]::OpenRead($symbolsPath)
try {
    $symbolsSignatureLength = $symbolsStream.Read(
        $symbolsSignatureBytes,
        0,
        $symbolsSignatureBytes.Length)
}
finally {
    $symbolsStream.Dispose()
}

$symbolsSignature =
    [Text.Encoding]::ASCII.GetString($symbolsSignatureBytes)
if ($symbolsSignatureLength -ne $symbolsSignatureBytes.Length -or
    $symbolsSignature -cne 'Microsoft C/C++ MSF 7.00') {
    throw "Expected Windows PDB symbols at '$symbolsPath'; the compiler produced an unsupported symbol format."
}

$actualAssemblyVersion =
    [Reflection.AssemblyName]::GetAssemblyName($assemblyPath).Version.ToString()
$actualVersionInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($assemblyPath)
if ($actualAssemblyVersion -ne $stableAssemblyVersion) {
    throw "Package AssemblyVersion is '$actualAssemblyVersion'; expected stable version '$stableAssemblyVersion'."
}
if ($actualVersionInfo.FileVersion -ne $packageFileVersion) {
    throw "Package AssemblyFileVersion is '$($actualVersionInfo.FileVersion)'; expected '$packageFileVersion'."
}
if ($actualVersionInfo.ProductVersion -ne $PackageVersion) {
    throw "Package AssemblyInformationalVersion is '$($actualVersionInfo.ProductVersion)'; expected '$PackageVersion'."
}

Copy-Item -LiteralPath $assemblyPath -Destination (
    Join-Path $stageDirectory 'lib/net20/WinFormsXaml.dll')
Copy-Item -LiteralPath $xmlDocumentationPath -Destination (
    Join-Path $stageDirectory 'lib/net20/WinFormsXaml.xml')
Copy-Item -LiteralPath $symbolsPath -Destination (
    Join-Path $stageDirectory 'lib/net20/WinFormsXaml.pdb')
Copy-Item -LiteralPath $packageReadmePath -Destination (
    Join-Path $stageDirectory 'README.md')
Copy-Item -LiteralPath $schemaPath -Destination (
    Join-Path $stageDirectory 'content/WinFormsXaml.xsd')
Copy-Item -LiteralPath $schemaPath -Destination (
    Join-Path $stageDirectory 'contentFiles/any/any/WinFormsXaml.xsd')
Copy-Item -LiteralPath $schemaPath -Destination (
    Join-Path $stageDirectory 'schemas/WinFormsXaml.xsd')

Write-Host "Packing WinFormsXaml $PackageVersion..."
Invoke-CheckedCommand $NuGetPath @(
    'pack',
    $nuspecPath,
    '-Version',
    $PackageVersion,
    '-BasePath',
    $stageDirectory,
    '-OutputDirectory',
    $packageOutputDirectory,
    '-NonInteractive'
)

$packages = @(
    Get-ChildItem -LiteralPath $packageOutputDirectory -Filter 'WinFormsXaml.*.nupkg' -File
)
if ($packages.Count -ne 1 -or $packages[0].Length -eq 0) {
    throw 'NuGet pack did not produce exactly one non-empty WinFormsXaml package.'
}

Write-Host "Staged package contents: $stageDirectory"
Write-Host "Created package: $($packages[0].FullName)"
