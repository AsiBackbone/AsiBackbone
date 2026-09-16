#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
configuration="${CONFIGURATION:-Release}"
package_output="${1:-${PACKAGE_OUTPUT:-artifacts/packages}}"
work_root="${TEMPLATE_SMOKE_WORK_ROOT:-${RUNNER_TEMP:-/tmp}/asi-backbone-template-smoke}"

get_project_version() {
  local project_path="$1"

  dotnet msbuild "$project_path" -getProperty:Version -nologo | tr -d '\r' | awk 'NF { print; exit }'
}

assert_generated_package_version() {
  local project_file="$1"
  local package_id="$2"
  local expected_version="$3"
  local actual_version

  actual_version="$(sed -nE "s/.*<PackageReference Include=\"$package_id\" Version=\"([^\"]+)\".*/\1/p" "$project_file" | head -n 1)"

  if [ -z "$actual_version" ]; then
    echo "Generated project did not contain a PackageReference for $package_id."
    exit 1
  fi

  if [ "$actual_version" != "$expected_version" ]; then
    echo "Generated project PackageReference for $package_id expected version $expected_version but found $actual_version."
    exit 1
  fi
}

make_absolute_path() {
  local path="$1"

  if [[ "$path" = /* || "$path" =~ ^[A-Za-z]:[\\/].* ]]; then
    printf '%s\n' "$path"
  else
    printf '%s/%s\n' "$repo_root" "$path"
  fi
}

to_dotnet_path() {
  local path="$1"

  if command -v cygpath >/dev/null 2>&1; then
    cygpath -w "$path"
  else
    printf '%s\n' "$path"
  fi
}

package_output="$(to_dotnet_path "$(make_absolute_path "$package_output")")"
core_project="$repo_root/src/AsiBackbone.Core/AsiBackbone.Core.csproj"
expected_package_version="${TEMPLATE_SMOKE_PACKAGE_VERSION:-$(get_project_version "$core_project")}"

if [ -z "$expected_package_version" ]; then
  echo "Unable to determine expected AsiBackbone package version. Set TEMPLATE_SMOKE_PACKAGE_VERSION explicitly."
  exit 1
fi

if [ ! -d "$package_output" ]; then
  echo "Package output directory was not found: $package_output"
  exit 1
fi

template_package="$(find "$package_output" -maxdepth 1 -name 'AsiBackbone.Templates.*.nupkg' -type f | sort | tail -n 1)"

if [ -z "$template_package" ]; then
  echo "AsiBackbone.Templates package was not found in $package_output."
  exit 1
fi

rm -rf "$work_root"
mkdir -p "$work_root"

# Use a fresh consumer cache: release-branch builds can share a version with published 5.x packages.
export NUGET_PACKAGES="$(to_dotnet_path "$work_root/.nuget/packages")"

cat > "$work_root/NuGet.config" <<NUGETCONFIG
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-asi-backbone" value="$package_output" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local-asi-backbone">
      <package pattern="AsiBackbone.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
NUGETCONFIG

export DOTNET_CLI_HOME="$work_root/.dotnet-home"
mkdir -p "$DOTNET_CLI_HOME"

dotnet new install "$template_package"

for host_style in plain netcoretemplate; do
  project_name="AsiBackboneTemplateSmoke${host_style}"
  project_dir="$work_root/$project_name"
  generated_project="$project_dir/$project_name.csproj"

  echo "Generating template host style '$host_style' into $project_dir"
  dotnet new asibackbone-webapi \
    --name "$project_name" \
    --output "$project_dir" \
    --hostStyle "$host_style"

  if ! grep -q "\"HostStyle\": \"$host_style\"" "$project_dir/appsettings.json"; then
    echo "Generated appsettings.json did not contain selected host style '$host_style'."
    exit 1
  fi

  for package_id in \
    AsiBackbone.AspNetCore \
    AsiBackbone.Core \
    AsiBackbone.Storage.InMemory \
    AsiBackbone.Analyzers; do
    assert_generated_package_version "$generated_project" "$package_id" "$expected_package_version"
  done

  if grep -Eq 'Version="1\.2\.0"' "$generated_project"; then
    echo "Generated project still references stale 1.2.0 package versions."
    exit 1
  fi

  dotnet restore "$generated_project" \
    --configfile "$work_root/NuGet.config"

  dotnet build "$generated_project" \
    --configuration "$configuration" \
    --no-restore

done
