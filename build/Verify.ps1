[CmdletBinding()]
param(
    [switch] $SkipClassicSolutionValidation,
    [switch] $SkipTests,
    [switch] $SkipDocs,
    [switch] $RequireNativeMarquee
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

if ($RequireNativeMarquee -and $SkipTests) {
    throw '-RequireNativeMarquee cannot be combined with -SkipTests.'
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sourceRoot = Join-Path $repositoryRoot 'src/WinFormsXaml'
$classicProject = Join-Path $sourceRoot 'WinFormsXaml.csproj'
$classicSolution = Join-Path $repositoryRoot 'WinFormsXaml.sln'
$classicProjects = @(
    'src/WinFormsXaml/WinFormsXaml.csproj',
    'samples/HelloWorld/HelloWorld.csproj',
    'samples/ComponentsGallery/ComponentsGallery.csproj',
    'samples/BindingPlayground/BindingPlayground.csproj',
    'samples/PresetStudio/PresetStudio.csproj',
    'samples/ItemsExplorer/ItemsExplorer.csproj',
    'tests/WinFormsXaml.Tests/WinFormsXaml.Tests.csproj',
    'tests/WinFormsXaml.LayoutTests/WinFormsXaml.LayoutTests.csproj',
    'tests/WinFormsXaml.ItemsTests/WinFormsXaml.ItemsTests.csproj',
    'tests/WinFormsXaml.NativeMarqueeValidation/WinFormsXaml.NativeMarqueeValidation.csproj',
    'benchmarks/WinFormsXaml.Benchmarks/WinFormsXaml.Benchmarks.csproj',
    'benchmarks/WinFormsXaml.InteractiveBenchmarks/WinFormsXaml.InteractiveBenchmarks.csproj'
)
$schemaPath = Join-Path $repositoryRoot 'schemas/WinFormsXaml.xsd'
$schemaFixturePaths = @(
    Get-ChildItem `
        -LiteralPath (Join-Path $repositoryRoot 'samples') `
        -Filter '*.xml' `
        -Recurse |
        Where-Object { -not $_.PSIsContainer } |
        Sort-Object FullName |
        ForEach-Object { $_.FullName }

    Get-ChildItem `
        -LiteralPath (Join-Path $repositoryRoot 'benchmarks/WinFormsXaml.InteractiveBenchmarks/Fixtures') `
        -Filter '*.xml' |
        Where-Object { -not $_.PSIsContainer } |
        Sort-Object FullName |
        ForEach-Object { $_.FullName }
)

. (Join-Path $PSScriptRoot 'SchemaValidation.ps1')

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

function Get-RequiredCommandPath {
    param([Parameter(Mandatory = $true)][string] $Name)

    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw "Required command '$Name' was not found on PATH."
    }

    return $command.Source
}

function Test-ClassicSourceParity {
    [xml] $projectXml = Get-Content -LiteralPath $classicProject -Raw
    $namespaceManager = New-Object System.Xml.XmlNamespaceManager($projectXml.NameTable)
    $namespaceManager.AddNamespace('msb', 'http://schemas.microsoft.com/developer/msbuild/2003')

    $declaredSources = @(
        $projectXml.SelectNodes('//msb:Compile[@Include]', $namespaceManager) |
            ForEach-Object { $_.GetAttribute('Include').Replace('\', '/') } |
            Sort-Object -Unique
    )

    $sourcePrefix = $sourceRoot.TrimEnd([char[]] '\/') + [System.IO.Path]::DirectorySeparatorChar
    $actualSources = @(
        Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -Recurse |
            Where-Object {
                -not $_.PSIsContainer -and
                $_.FullName -notmatch '[\\/](bin|obj)[\\/]'
            } |
            ForEach-Object {
                $_.FullName.Substring($sourcePrefix.Length).Replace('\', '/')
            } |
            Sort-Object -Unique
    )

    $missingFromProject = @($actualSources | Where-Object { $declaredSources -notcontains $_ })
    $missingFromDisk = @($declaredSources | Where-Object { $actualSources -notcontains $_ })

    if ($missingFromProject.Count -ne 0 -or $missingFromDisk.Count -ne 0) {
        $details = @()
        if ($missingFromProject.Count -ne 0) {
            $details += "Source files absent from the classic project: $($missingFromProject -join ', ')"
        }
        if ($missingFromDisk.Count -ne 0) {
            $details += "Classic project entries absent from disk: $($missingFromDisk -join ', ')"
        }
        throw ($details -join [Environment]::NewLine)
    }

    Write-Host "Classic project source list matches all $($actualSources.Count) runtime source files."
}

function Test-ClassicSolutionStructure {
    if (-not (Test-Path -LiteralPath $classicSolution -PathType Leaf)) {
        throw "The Visual Studio 2005 solution was not found at '$classicSolution'."
    }

    $solutionText = [IO.File]::ReadAllText($classicSolution)
    $expectedHeader =
        'Microsoft Visual Studio Solution File, Format Version 9.00'

    if (-not $solutionText.StartsWith(
        $expectedHeader,
        [StringComparison]::Ordinal)) {
        throw "The classic solution must retain the Visual Studio 2005 format header '$expectedHeader'."
    }

    $projectEntryCount =
        [Regex]::Matches($solutionText, '(?m)^Project\(').Count
    if ($projectEntryCount -ne $classicProjects.Count) {
        throw "The classic solution contains $projectEntryCount project entries; expected $($classicProjects.Count)."
    }

    foreach ($project in $classicProjects) {
        $absoluteProject = Join-Path $repositoryRoot $project
        if (-not (Test-Path -LiteralPath $absoluteProject -PathType Leaf)) {
            throw "The classic solution project was not found: '$absoluteProject'."
        }

        $solutionProjectPath = $project.Replace('/', '\')
        if ($solutionText.IndexOf(
            ('"' + $solutionProjectPath + '"'),
            [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw "The classic solution does not reference '$solutionProjectPath'."
        }

        [xml] $projectXml = Get-Content -LiteralPath $absoluteProject -Raw
        $namespaceManager = New-Object System.Xml.XmlNamespaceManager(
            $projectXml.NameTable)
        $namespaceManager.AddNamespace(
            'msb',
            'http://schemas.microsoft.com/developer/msbuild/2003')
        $projectGuidNode = $projectXml.SelectSingleNode(
            '//msb:ProjectGuid',
            $namespaceManager)

        if ($null -eq $projectGuidNode -or
            [String]::IsNullOrEmpty($projectGuidNode.InnerText)) {
            throw "The classic project '$project' does not declare ProjectGuid."
        }

        $projectGuid = $projectGuidNode.InnerText.Trim()
        foreach ($configuration in @('Debug', 'Release')) {
            $activeConfiguration =
                "$projectGuid.$configuration|Any CPU.ActiveCfg = " +
                "$configuration|Any CPU"
            $buildConfiguration =
                "$projectGuid.$configuration|Any CPU.Build.0 = " +
                "$configuration|Any CPU"

            if ($solutionText.IndexOf(
                $activeConfiguration,
                [StringComparison]::OrdinalIgnoreCase) -lt 0) {
                throw "The classic solution is missing '$activeConfiguration'."
            }
            if ($solutionText.IndexOf(
                $buildConfiguration,
                [StringComparison]::OrdinalIgnoreCase) -lt 0) {
                throw "The classic solution is missing '$buildConfiguration'."
            }
        }
    }

    Write-Host "Visual Studio 2005 solution structure contains all $($classicProjects.Count) projects and Debug/Release mappings."
}

Push-Location $repositoryRoot
try {
    Test-ClassicSourceParity
    Assert-WinFormsXamlSchemaContract `
        -SchemaPath $schemaPath `
        -FixturePaths $schemaFixturePaths

    $msbuild = $null

    if (-not $SkipClassicSolutionValidation) {
        Test-ClassicSolutionStructure
        $msbuild = Get-RequiredCommandPath 'msbuild'
    }
    else {
        Write-Host 'WINFORMSXAML_CLASSIC_SOLUTION: SKIP - requested by -SkipClassicSolutionValidation; source parity and SDK validation still run.'
    }

    $dotnet = Get-RequiredCommandPath 'dotnet'
    $validationProjects = @(
        'build/WinFormsXaml.Validation.csproj',
        'build/WinFormsXaml.NativeMarqueeValidation.Validation.csproj',
        'build/WinFormsXaml.Tests.Validation.csproj',
        'build/WinFormsXaml.LayoutTests.Validation.csproj',
        'build/WinFormsXaml.ItemsTests.Validation.csproj',
        'build/HelloWorld.Validation.csproj',
        'build/WinFormsXaml.Benchmarks.Validation.csproj',
        'build/WinFormsXaml.InteractiveBenchmarks.Validation.csproj'
    )
    $validationPackagesRoot = [IO.Path]::GetFullPath(
        (Join-Path $repositoryRoot 'build/obj/packages'))

    foreach ($project in $validationProjects) {
        Invoke-CheckedCommand $dotnet @(
            'restore',
            $project,
            '--packages',
            $validationPackagesRoot
        )
        Invoke-CheckedCommand $dotnet @(
            'build',
            $project,
            '--configuration',
            'Release',
            '--no-restore',
            "-p:RestorePackagesPath=$validationPackagesRoot"
        )
    }

    if (-not $SkipClassicSolutionValidation) {
        $net20PackageRoot = Join-Path $validationPackagesRoot (
            'microsoft.netframework.referenceassemblies.net20/1.0.3')
        $net20TargetFrameworkRoot = Join-Path $net20PackageRoot 'build'
        $net20FrameworkPath = Join-Path $net20TargetFrameworkRoot (
            '.NETFramework/v2.0')

        if (-not (Test-Path -LiteralPath (
            Join-Path $net20FrameworkPath 'mscorlib.dll') -PathType Leaf)) {
            throw "The restored .NET Framework 2.0 reference assemblies were not found under '$net20FrameworkPath'."
        }

        $net20TargetFrameworkRoot =
            [IO.Path]::GetFullPath($net20TargetFrameworkRoot).TrimEnd(
                [char[]] '\/') + [IO.Path]::DirectorySeparatorChar
        $net20FrameworkPath = [IO.Path]::GetFullPath($net20FrameworkPath)

        for ($projectIndex = 0;
            $projectIndex -lt $classicProjects.Count;
            $projectIndex++) {
            $classicBuildArguments = @(
                $classicProjects[$projectIndex],
                '/t:Rebuild',
                '/p:Configuration=Release',
                '/p:Platform=AnyCPU',
                "/p:TargetFrameworkRootPath=$net20TargetFrameworkRoot",
                "/p:FrameworkPathOverride=$net20FrameworkPath",
                '/p:TreatWarningsAsErrors=true',
                '/verbosity:minimal'
            )

            Invoke-CheckedCommand $msbuild $classicBuildArguments
        }
    }

    if (-not $SkipTests) {
        $nativeMarqueeExecutable =
            'build/bin/Release/net20/WinFormsXaml.NativeMarqueeValidation.exe'
        $testExecutables = @(
            'build/bin/Release/net20/WinFormsXaml.Tests.exe',
            'build/bin/Release/net20/WinFormsXaml.LayoutTests.exe',
            'build/bin/Release/net20/WinFormsXaml.ItemsTests.exe'
        )
        $isWindowsHost = $env:OS -eq 'Windows_NT'
        $runner = $null

        $absoluteNativeMarqueePath =
            Join-Path $repositoryRoot $nativeMarqueeExecutable
        if (-not (Test-Path -LiteralPath $absoluteNativeMarqueePath -PathType Leaf)) {
            throw "Expected native marquee validation executable was not built: $absoluteNativeMarqueePath"
        }

        if ($isWindowsHost) {
            Write-Host "> $absoluteNativeMarqueePath"
            & $absoluteNativeMarqueePath
            $nativeMarqueeExitCode = $LASTEXITCODE

            if ($nativeMarqueeExitCode -eq 2) {
                $skipMessage =
                    'Windows-native marquee validation reported SKIP. The host did not provide an enabled version 6 Common Controls marquee path.'

                if ($RequireNativeMarquee) {
                    throw "$skipMessage This verification requires PASS."
                }

                Write-Warning $skipMessage
            }
            elseif ($nativeMarqueeExitCode -ne 0) {
                throw "Windows-native marquee validation failed with exit code $nativeMarqueeExitCode."
            }
        }
        else {
            $skipMessage =
                'WINFORMSXAML_NATIVE_MARQUEE: SKIP - direct Windows_NT execution is required; Wine and Mono are not accepted as Windows-native marquee evidence.'

            if ($RequireNativeMarquee) {
                throw "$skipMessage This verification requires PASS."
            }

            Write-Host $skipMessage
        }

        if (-not $isWindowsHost) {
            $runnerCommand = Get-Command 'wine' -ErrorAction SilentlyContinue
            if ($null -eq $runnerCommand) {
                $runnerCommand = Get-Command 'mono' -ErrorAction SilentlyContinue
            }
            if ($null -eq $runnerCommand) {
                throw 'Running the .NET Framework tests outside Windows requires Wine or Mono. Use -SkipTests for compile-only verification.'
            }
            $runner = $runnerCommand.Source
        }

        foreach ($testExecutable in $testExecutables) {
            $absoluteTestPath = Join-Path $repositoryRoot $testExecutable
            if (-not (Test-Path -LiteralPath $absoluteTestPath -PathType Leaf)) {
                throw "Expected test executable was not built: $absoluteTestPath"
            }

            if ($isWindowsHost) {
                Invoke-CheckedCommand $absoluteTestPath
            }
            else {
                Invoke-CheckedCommand $runner @($absoluteTestPath)
            }
        }
    }
    else {
        Write-Host 'WINFORMSXAML_TEST_EXECUTION: SKIP - requested by -SkipTests; all validation projects were still compiled.'
    }

    if (-not $SkipDocs) {
        $npm = Get-RequiredCommandPath 'npm'
        Invoke-CheckedCommand $npm @('--prefix', 'docs', 'ci')
        Invoke-CheckedCommand $npm @('--prefix', 'docs', 'run', 'docs:build')
    }
    else {
        Write-Host 'WINFORMSXAML_DOCS_BUILD: SKIP - requested by -SkipDocs; XML schema validation still ran.'
    }

    Write-Host 'Verification completed successfully.'
}
finally {
    Pop-Location
}
