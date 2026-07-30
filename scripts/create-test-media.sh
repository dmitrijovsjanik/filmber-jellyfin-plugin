#!/usr/bin/env bash
set -euo pipefail

repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
movie_dir="${repo_dir}/docker/media/Movies/Spike Movie (1999)"
episode_dir="${repo_dir}/docker/media/Shows/Spike Series/Season 01"

mkdir -p "${movie_dir}" "${episode_dir}"

ffmpeg -hide_banner -loglevel error -y \
  -f lavfi -i color=c=black:s=320x180:r=24 \
  -f lavfi -i sine=frequency=440:sample_rate=48000 \
  -t 20 -c:v libx264 -pix_fmt yuv420p -c:a aac \
  "${movie_dir}/Spike Movie (1999).mp4"

ffmpeg -hide_banner -loglevel error -y \
  -f lavfi -i color=c=blue:s=320x180:r=24 \
  -f lavfi -i sine=frequency=660:sample_rate=48000 \
  -t 20 -c:v libx264 -pix_fmt yuv420p -c:a aac \
  "${episode_dir}/Spike Series S01E01.mp4"

python3 "${repo_dir}/scripts/write-test-nfo.py" "${repo_dir}/docker/media"
