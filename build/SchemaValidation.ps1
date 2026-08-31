$script:WinFormsXamlSchemaContractValidatorSourcePath =
    [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'SchemaContractValidator.cs'))

function Get-WinFormsXamlSchemaContractValidatorType {
    $validatorTypeName =
        'WinFormsXaml.BuildTools.SchemaContractValidator'

    foreach ($assembly in [AppDomain]::CurrentDomain.GetAssemblies()) {
        $validatorType = $assembly.GetType($validatorTypeName, $false)
        if ($null -ne $validatorType) {
            return $validatorType
        }
    }

    if (-not (Test-Path `
        -LiteralPath $script:WinFormsXamlSchemaContractValidatorSourcePath `
        -PathType Leaf)) {
        throw (
            "The schema contract validator source was not found at " +
            "'$script:WinFormsXamlSchemaContractValidatorSourcePath'.")
    }

    $addTypeParameters = @{
        Path = $script:WinFormsXamlSchemaContractValidatorSourcePath
        PassThru = $true
    }
    $powerShellEdition = [string] $PSVersionTable['PSEdition']
    if ([String]::IsNullOrEmpty($powerShellEdition) -or
        $powerShellEdition -eq 'Desktop') {
        $addTypeParameters['ReferencedAssemblies'] = @('System.Xml.dll')
    }

    $compiledTypes = @(Add-Type @addTypeParameters)
    foreach ($compiledType in $compiledTypes) {
        if ($compiledType.FullName -eq $validatorTypeName) {
            return $compiledType
        }
    }

    throw "Add-Type did not load the expected validator type '$validatorTypeName'."
}

function Assert-WinFormsXamlSchemaContract {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $SchemaPath,

        [Parameter(Mandatory = $true)]
        [string[]] $FixturePaths
    )

    $validatorType = Get-WinFormsXamlSchemaContractValidatorType
    $validateMethod = $validatorType.GetMethod('Validate')
    if ($null -eq $validateMethod) {
        throw 'The schema contract validator does not expose Validate.'
    }

    $validatorArguments = [Array]::CreateInstance([object], 2)
    $validatorArguments.SetValue($SchemaPath, 0)
    $validatorArguments.SetValue(([string[]] $FixturePaths), 1)

    try {
        [void] $validateMethod.Invoke($null, $validatorArguments)
    }
    catch {
        $validationException = $_.Exception
        while ($null -ne $validationException.InnerException -and
            ($validationException -is [System.Management.Automation.MethodInvocationException] -or
             $validationException -is [Reflection.TargetInvocationException])) {
            $validationException = $validationException.InnerException
        }

        throw $validationException
    }
}
