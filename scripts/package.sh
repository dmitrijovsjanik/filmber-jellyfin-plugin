#!/usr/bin/env bash
set -euo pipefail

repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
jellyfin_version="${JELLYFIN_VERSION:-10.11.8}"
artifacts_dir="${repo_dir}/artifacts/jellyfin-${jellyfin_version}"
publish_dir="${artifacts_dir}/publish"
plugin_dir="${artifacts_dir}/plugin"
repository_dir="${artifacts_dir}/repository"
public_repository_dir="${repo_dir}/repository"
plugin_version="0.2.0.1"
target_abi="10.11.0.0"
zip_name="filmber-sync_${plugin_version}_jellyfin-${jellyfin_version}.zip"
release_tag="v${plugin_version}"
source_url="https://github.com/dmitrijovsjanik/filmber-jellyfin-plugin/releases/download/${release_tag}/${zip_name}"

cd "${repo_dir}"

"${repo_dir}/scripts/dotnet.sh" publish \
  "Jellyfin.Plugin.FilmberSync/Jellyfin.Plugin.FilmberSync.csproj" \
  --configuration Release \
  --output "artifacts/jellyfin-${jellyfin_version}/publish" \
  -p:JellyfinVersion="${jellyfin_version}"

mkdir -p "${plugin_dir}" "${repository_dir}" "${public_repository_dir}"
cp "${publish_dir}/Jellyfin.Plugin.FilmberSync.dll" "${plugin_dir}/"

(
  cd "${plugin_dir}"
  zip -q -FS "${repository_dir}/${zip_name}" Jellyfin.Plugin.FilmberSync.dll
)

checksum="$(openssl dgst -md5 -r "${repository_dir}/${zip_name}" | awk '{print $1}')"
timestamp="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"

python3 "${repo_dir}/scripts/write-manifest.py" \
  "${repository_dir}/manifest.json" \
  "${zip_name}" \
  "${plugin_version}" \
  "${target_abi}" \
  "${checksum}" \
  "${timestamp}" \
  "${source_url}"

cp "${repository_dir}/manifest.json" "${public_repository_dir}/manifest.json"

python3 "${repo_dir}/scripts/write-build-yaml.py" \
  "${artifacts_dir}/build.yaml" \
  "${plugin_version}" \
  "${target_abi}" \
  "${jellyfin_version}"

echo "${repository_dir}/${zip_name}"
echo "${repository_dir}/manifest.json"
