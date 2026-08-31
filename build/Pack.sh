#!/usr/bin/env bash

set -euo pipefail

if [[ $# -ne 1 ]]; then
    echo "Usage: $0 <package-version>" >&2
    echo "Example: $0 0.1.0" >&2
    exit 2
fi

package_version="$1"
if [[ ! "$package_version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?$ ]]; then
    echo "Package version must be a three-part NuGet version such as 0.1.0 or 0.1.0-preview.1." >&2
    exit 2
fi

package_numeric_version="${package_version%%-*}"
IFS='.' read -r package_major package_minor package_patch <<< "$package_numeric_version"
for version_part in "$package_major" "$package_minor" "$package_patch"; do
    if [[ ${#version_part} -gt 5 ]] || (( 10#$version_part > 65534 )); then
        echo "Package version components must be between 0 and 65534 so they can be represented in AssemblyFileVersion." >&2
        exit 2
    fi
done
package_file_version="$package_major.$package_minor.$package_patch.0"

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "$script_directory/.." && pwd)"
source_root="$repository_root/src/WinFormsXaml"
nuspec_path="$repository_root/packaging/WinFormsXaml.nuspec"
package_readme_path="$repository_root/packaging/PackageREADME.md"
schema_path="$repository_root/schemas/WinFormsXaml.xsd"
schema_validator_source_path="$script_directory/SchemaContractValidator.cs"
schema_fixture_paths=()
while IFS= read -r schema_fixture_path; do
    schema_fixture_paths+=("$schema_fixture_path")
done < <(
    find "$repository_root/samples" -type f -name '*.xml' -print |
        LC_ALL=C sort
)
package_root="$repository_root/artifacts/package"
build_directory="$package_root/build"
stage_directory="$package_root/stage"
package_output_directory="$package_root/output"
toolchain_root="$repository_root/artifacts/toolchain/pack"
pinned_compiler_package="Microsoft.Net.Compilers"
pinned_compiler_version="1.3.2"
pinned_reference_package="Microsoft.NETFramework.ReferenceAssemblies.net20"
pinned_reference_version="1.0.3"
pinned_nuget_source="https://api.nuget.org/v3/index.json"
toolchain_restore_directory=""
schema_validator_temp_directory=""

cleanup_temporary_directories() {
    if [[ -n "${toolchain_restore_directory:-}" ]]; then
        case "$toolchain_restore_directory" in
            "$repository_root"/artifacts/toolchain/pack.restore.*)
                rm -rf -- "$toolchain_restore_directory"
                ;;
            *)
                echo "Refusing to clean unexpected toolchain restore directory '$toolchain_restore_directory'." >&2
                ;;
        esac
    fi

    if [[ -n "${schema_validator_temp_directory:-}" ]]; then
        case "$schema_validator_temp_directory" in
            */winformsxaml-schema-validator.*)
                rm -rf -- "$schema_validator_temp_directory"
                ;;
            *)
                echo "Refusing to clean unexpected schema-validator directory '$schema_validator_temp_directory'." >&2
                ;;
        esac
    fi
}
trap cleanup_temporary_directories EXIT

require_command() {
    if ! command -v "$1" >/dev/null 2>&1; then
        echo "Required command '$1' was not found on PATH." >&2
        exit 1
    fi
}

require_command wine
require_command winepath

nuget_path="${NUGET_EXE:-}"
if [[ -z "$nuget_path" ]]; then
    if command -v nuget >/dev/null 2>&1; then
        nuget_path="$(command -v nuget)"
    elif command -v nuget.exe >/dev/null 2>&1; then
        nuget_path="$(command -v nuget.exe)"
    else
        echo "NuGet CLI was not found." >&2
        echo "Install 'nuget' on PATH or set NUGET_EXE to the full path of nuget.exe." >&2
        exit 1
    fi
fi

if [[ ! -f "$nuget_path" && ! -x "$nuget_path" ]]; then
    echo "NuGet CLI was not found at '$nuget_path'." >&2
    exit 1
fi

nuget_uses_wine=false
case "$nuget_path" in
    *.[eE][xX][eE]) nuget_uses_wine=true ;;
esac

run_nuget() {
    if [[ "$nuget_uses_wine" == true ]]; then
        WINEDEBUG="${WINEDEBUG:--all}" wine "$nuget_path" "$@"
    else
        "$nuget_path" "$@"
    fi
}

nuget_path_argument() {
    if [[ "$nuget_uses_wine" == true ]]; then
        winepath -w "$1"
    else
        printf '%s\n' "$1"
    fi
}

version_at_least() {
    local current_version="$1"
    local required_version="$2"
    local current_parts
    local required_parts
    local index

    IFS='.' read -r -a current_parts <<< "$current_version"
    IFS='.' read -r -a required_parts <<< "$required_version"

    for index in 0 1 2 3; do
        local current_part="${current_parts[$index]:-0}"
        local required_part="${required_parts[$index]:-0}"

        if (( 10#$current_part > 10#$required_part )); then
            return 0
        fi
        if (( 10#$current_part < 10#$required_part )); then
            return 1
        fi
    done

    return 0
}

minimum_nuget_version="5.10.0"
nuget_help_output="$(run_nuget help 2>&1 || true)"
nuget_version="$(
    printf '%s\n' "$nuget_help_output" |
        sed -nE 's/.*NuGet Version:[[:space:]]*([0-9]+(\.[0-9]+){2,3}).*/\1/p' |
        head -n 1
)"

if [[ -z "$nuget_version" ]]; then
    echo "Could not determine the NuGet CLI version from '$nuget_path'." >&2
    echo "NuGet CLI $minimum_nuget_version or newer is required for package README support." >&2
    exit 1
fi
if ! version_at_least "$nuget_version" "$minimum_nuget_version"; then
    echo "NuGet CLI $nuget_version is too old; $minimum_nuget_version or newer is required for package README support." >&2
    exit 1
fi

echo "Using NuGet CLI $nuget_version."

framework_reference_names=(
    mscorlib.dll
    System.dll
    System.Data.dll
    System.Drawing.dll
    System.Windows.Forms.dll
    System.Xml.dll
)

validate_framework_reference_root() {
    local candidate_root="$1"
    local reference_name

    for reference_name in "${framework_reference_names[@]}"; do
        if [[ ! -s "$candidate_root/$reference_name" ]]; then
            return 1
        fi
    done

    return 0
}

validate_cached_toolchain() {
    local candidate_root="$1"
    local candidate_compiler=
    local candidate_references=

    candidate_compiler="$candidate_root/$pinned_compiler_package.$pinned_compiler_version/tools/csc.exe"
    candidate_references="$candidate_root/$pinned_reference_package.$pinned_reference_version/build/.NETFramework/v2.0"

    [[ -s "$candidate_compiler" ]] &&
        validate_framework_reference_root "$candidate_references"
}

compiler_host_path="${WINFORMSXAML_CSC_HOST:-}"
framework_reference_path="${WINFORMSXAML_REFERENCE_ROOT:-}"
use_explicit_framework_references=false
using_pinned_compiler=false

if [[ -n "${WINFORMSXAML_CSC:-}" ]]; then
    compiler_path="$WINFORMSXAML_CSC"

    if [[ -n "$compiler_host_path" || -n "$framework_reference_path" ]]; then
        use_explicit_framework_references=true
    fi
else
    if [[ -n "$compiler_host_path" || -n "$framework_reference_path" ]]; then
        echo "WINFORMSXAML_CSC is required when WINFORMSXAML_CSC_HOST or WINFORMSXAML_REFERENCE_ROOT is set." >&2
        exit 1
    fi

    if ! validate_cached_toolchain "$toolchain_root"; then
        echo "The pinned Bash/Wine package toolchain is not cached."
        echo "Restoring $pinned_compiler_package $pinned_compiler_version and $pinned_reference_package $pinned_reference_version from $pinned_nuget_source."

        mkdir -p "$repository_root/artifacts/toolchain"
        toolchain_restore_directory="$(
            mktemp -d "$repository_root/artifacts/toolchain/pack.restore.XXXXXX"
        )"

        restore_output_path="$(
            nuget_path_argument "$toolchain_restore_directory"
        )"

        run_nuget install "$pinned_compiler_package" \
            -Version "$pinned_compiler_version" \
            -OutputDirectory "$restore_output_path" \
            -Source "$pinned_nuget_source" \
            -NonInteractive \
            -ForceEnglishOutput
        run_nuget install "$pinned_reference_package" \
            -Version "$pinned_reference_version" \
            -OutputDirectory "$restore_output_path" \
            -Source "$pinned_nuget_source" \
            -NonInteractive \
            -ForceEnglishOutput

        if ! validate_cached_toolchain "$toolchain_restore_directory"; then
            echo "The restored Bash/Wine package toolchain is incomplete." >&2
            exit 1
        fi

        case "$toolchain_root" in
            "$repository_root"/artifacts/toolchain/pack) ;;
            *)
                echo "Refusing to replace unexpected toolchain cache '$toolchain_root'." >&2
                exit 1
                ;;
        esac

        rm -rf -- "$toolchain_root"
        mv "$toolchain_restore_directory" "$toolchain_root"
        toolchain_restore_directory=""
    fi

    compiler_path="$toolchain_root/$pinned_compiler_package.$pinned_compiler_version/tools/csc.exe"
    framework_reference_path="$toolchain_root/$pinned_reference_package.$pinned_reference_version/build/.NETFramework/v2.0"
    use_explicit_framework_references=true
    using_pinned_compiler=true
fi

if [[ ! -s "$compiler_path" ]]; then
    echo "The requested C# compiler was not found at '$compiler_path'." >&2
    exit 1
fi
if [[ -n "$compiler_host_path" && ! -s "$compiler_host_path" ]]; then
    echo "The requested Windows compiler host was not found at '$compiler_host_path'." >&2
    exit 1
fi
if [[ "$use_explicit_framework_references" == true ]] &&
    [[ -z "$framework_reference_path" ]]; then
    echo "WINFORMSXAML_REFERENCE_ROOT is required with a custom compiler host." >&2
    echo "Point it at the directory containing the .NET Framework 2.0 reference assemblies." >&2
    exit 1
fi
if [[ "$use_explicit_framework_references" == true ]] &&
    ! validate_framework_reference_root "$framework_reference_path"; then
    echo "The .NET Framework 2.0 reference directory is incomplete: '$framework_reference_path'." >&2
    exit 1
fi

if [[ "$using_pinned_compiler" == true ]]; then
    compiler_banner="$(
        WINEDEBUG="${WINEDEBUG:--all}" wine "$compiler_path" /help 2>&1 || true
    )"
    if [[ "$compiler_banner" != *"Visual C# Compiler version 1.3.1.60621"* ]]; then
        echo "The pinned Microsoft C# compiler could not be verified under Wine." >&2
        echo "The active Wine prefix needs a .NET Framework 4.5-compatible runtime (Wine Mono is supported)." >&2
        exit 1
    fi
    echo "Using cached $pinned_compiler_package $pinned_compiler_version with Microsoft .NET Framework 2.0 reference assemblies $pinned_reference_version."
fi

compiler_language_arguments=()
compiler_reference_arguments=()

if [[ "$use_explicit_framework_references" == true ]]; then
    # /noconfig prevents Roslyn's adjacent csc.rsp from importing host
    # framework assemblies before the explicit .NET 2 reference set.
    compiler_language_arguments+=(/noconfig /langversion:2 /nostdlib+)

    for reference_name in "${framework_reference_names[@]}"; do
        compiler_reference_arguments+=(
            "/reference:$(winepath -w "$framework_reference_path/$reference_name")"
        )
    done
else
    compiler_reference_arguments+=(
        /reference:System.dll
        /reference:System.Data.dll
        /reference:System.Drawing.dll
        /reference:System.Windows.Forms.dll
        /reference:System.Xml.dll
    )
fi

run_compiler() {
    if [[ -n "$compiler_host_path" ]]; then
        WINEDEBUG="${WINEDEBUG:--all}" wine \
            "$compiler_host_path" \
            "$(winepath -w "$compiler_path")" \
            "${compiler_language_arguments[@]}" \
            "$@"
    else
        WINEDEBUG="${WINEDEBUG:--all}" wine \
            "$compiler_path" \
            "${compiler_language_arguments[@]}" \
            "$@"
    fi
}

case "$build_directory" in
    "$repository_root"/artifacts/package/build) ;;
    *) echo "Refusing to clean unexpected build directory '$build_directory'." >&2; exit 1 ;;
esac
case "$stage_directory" in
    "$repository_root"/artifacts/package/stage) ;;
    *) echo "Refusing to clean unexpected stage directory '$stage_directory'." >&2; exit 1 ;;
esac
case "$package_output_directory" in
    "$repository_root"/artifacts/package/output) ;;
    *) echo "Refusing to clean unexpected package output directory '$package_output_directory'." >&2; exit 1 ;;
esac

if [[ ! -s "$schema_path" ]]; then
    echo "The WinFormsXaml editor schema was not found at '$schema_path'." >&2
    exit 1
fi
if [[ ! -s "$schema_validator_source_path" ]]; then
    echo "The schema contract validator source was not found at '$schema_validator_source_path'." >&2
    exit 1
fi
for schema_fixture_path in "${schema_fixture_paths[@]}"; do
    if [[ ! -f "$schema_fixture_path" ]]; then
        echo "The schema-validation fixture was not found at '$schema_fixture_path'." >&2
        exit 1
    fi
done

schema_validator_temp_directory="$(
    mktemp -d "${TMPDIR:-/tmp}/winformsxaml-schema-validator.XXXXXX"
)"

schema_validator_path="$schema_validator_temp_directory/SchemaContractValidator.exe"
echo "Compiling the offline schema contract validator for .NET Framework 2.0..."
run_compiler \
    /nologo \
    /target:exe \
    /optimize+ \
    /warn:4 \
    /warnaserror+ \
    "/out:$(winepath -w "$schema_validator_path")" \
    "${compiler_reference_arguments[@]}" \
    "$(winepath -w "$schema_validator_source_path")"

if [[ ! -s "$schema_validator_path" ]]; then
    echo "The schema contract validator was not created at '$schema_validator_path'." >&2
    exit 1
fi

schema_validator_arguments=("$(winepath -w "$schema_path")")
for schema_fixture_path in "${schema_fixture_paths[@]}"; do
    schema_validator_arguments+=("$(winepath -w "$schema_fixture_path")")
done

echo "Validating the WinFormsXaml schema contract offline..."
WINEDEBUG="${WINEDEBUG:--all}" wine "$schema_validator_path" \
    "${schema_validator_arguments[@]}"

cleanup_temporary_directories
schema_validator_temp_directory=""

rm -rf "$build_directory" "$stage_directory" "$package_output_directory"
mkdir -p \
    "$build_directory" \
    "$stage_directory/lib/net20" \
    "$stage_directory/content" \
    "$stage_directory/contentFiles/any/any" \
    "$stage_directory/schemas" \
    "$package_output_directory"

assembly_info_path="$source_root/Properties/AssemblyInfo.cs"
package_assembly_info_path="$build_directory/PackageAssemblyInfo.cs"

if [[ ! -f "$assembly_info_path" ]]; then
    echo "Assembly metadata was not found at '$assembly_info_path'." >&2
    exit 1
fi
if [[ "$(grep -Ec 'AssemblyVersion\("[^"]+"\)' "$assembly_info_path")" -ne 1 ]]; then
    echo "Exactly one stable AssemblyVersion declaration is required in '$assembly_info_path'." >&2
    exit 1
fi
if [[ "$(grep -Ec 'AssemblyFileVersion\("[^"]+"\)' "$assembly_info_path")" -ne 1 ]]; then
    echo "Exactly one AssemblyFileVersion declaration is required in '$assembly_info_path'." >&2
    exit 1
fi
if grep -Eq 'AssemblyInformationalVersion\(' "$assembly_info_path"; then
    echo "'$assembly_info_path' already declares AssemblyInformationalVersion; update the package version generation policy before packing." >&2
    exit 1
fi

sed -E \
    "s/AssemblyFileVersion\(\"[^\"]+\"\)/AssemblyFileVersion(\"$package_file_version\")/" \
    "$assembly_info_path" > "$package_assembly_info_path"
printf '\n[assembly: AssemblyInformationalVersion("%s")]\n' \
    "$package_version" >> "$package_assembly_info_path"

source_arguments=()
while IFS= read -r source_path; do
    if [[ "$source_path" == "$assembly_info_path" ]]; then
        continue
    fi
    source_arguments+=("$(winepath -w "$source_path")")
done < <(
    find "$source_root" \
        -type f \
        -name '*.cs' \
        ! -path '*/bin/*' \
        ! -path '*/obj/*' \
        -print | LC_ALL=C sort
)

if [[ ${#source_arguments[@]} -eq 0 ]]; then
    echo "No runtime C# source files were found under '$source_root'." >&2
    exit 1
fi
source_arguments+=("$(winepath -w "$package_assembly_info_path")")

assembly_path="$build_directory/WinFormsXaml.dll"
xml_documentation_path="$build_directory/WinFormsXaml.xml"
symbols_path="$build_directory/WinFormsXaml.pdb"

echo "Compiling WinFormsXaml $package_version for .NET Framework 2.0..."
run_compiler \
    /nologo \
    /target:library \
    /optimize+ \
    /debug:pdbonly \
    /define:WINFORMSXAML_PACKAGE \
    /warn:4 \
    /warnaserror+ \
    "/out:$(winepath -w "$assembly_path")" \
    "/pdb:$(winepath -w "$symbols_path")" \
    "/doc:$(winepath -w "$xml_documentation_path")" \
    "${compiler_reference_arguments[@]}" \
    "${source_arguments[@]}"

if [[ ! -s "$assembly_path" ]]; then
    echo "Expected release assembly was not created: '$assembly_path'." >&2
    exit 1
fi
if [[ ! -s "$xml_documentation_path" ]]; then
    echo "Expected XML documentation was not created: '$xml_documentation_path'." >&2
    exit 1
fi
if [[ ! -s "$symbols_path" ]]; then
    echo "Expected .NET Framework 2.0 symbols were not created: '$symbols_path'." >&2
    exit 1
fi
symbols_signature="$(LC_ALL=C head -c 24 "$symbols_path")"
if [[ "$symbols_signature" != 'Microsoft C/C++ MSF 7.00' ]]; then
    echo "Expected Windows PDB symbols at '$symbols_path'; the compiler produced an unsupported symbol format." >&2
    exit 1
fi

cp "$assembly_path" "$stage_directory/lib/net20/WinFormsXaml.dll"
cp "$xml_documentation_path" "$stage_directory/lib/net20/WinFormsXaml.xml"
cp "$symbols_path" "$stage_directory/lib/net20/WinFormsXaml.pdb"
cp "$package_readme_path" "$stage_directory/README.md"
cp "$schema_path" "$stage_directory/content/WinFormsXaml.xsd"
cp "$schema_path" "$stage_directory/contentFiles/any/any/WinFormsXaml.xsd"
cp "$schema_path" "$stage_directory/schemas/WinFormsXaml.xsd"

echo "Packing WinFormsXaml $package_version..."
case "$nuget_path" in
    *.[eE][xX][eE])
        run_nuget pack "$(winepath -w "$nuspec_path")" \
            -Version "$package_version" \
            -BasePath "$(winepath -w "$stage_directory")" \
            -OutputDirectory "$(winepath -w "$package_output_directory")" \
            -NonInteractive
        ;;
    *)
        run_nuget pack "$nuspec_path" \
            -Version "$package_version" \
            -BasePath "$stage_directory" \
            -OutputDirectory "$package_output_directory" \
            -NonInteractive
        ;;
esac

package_files=("$package_output_directory"/WinFormsXaml.*.nupkg)
if [[ ${#package_files[@]} -ne 1 || ! -s "${package_files[0]}" ]]; then
    echo "NuGet pack did not produce exactly one WinFormsXaml package." >&2
    exit 1
fi

echo "Staged package contents: $stage_directory"
echo "Created package: ${package_files[0]}"
