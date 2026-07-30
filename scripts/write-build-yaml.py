#!/usr/bin/env python3
import pathlib
import sys

output, version, target_abi, jellyfin_version = sys.argv[1:]
content = f"""---
name: "Filmber Sync"
guid: "791da508-0885-48c9-b41f-efdce37d1f4a"
version: "{version}"
targetAbi: "{target_abi}"
jellyfinVersion: "{jellyfin_version}"
framework: "net9.0"
overview: "Synchronize Jellyfin playback with Filmber"
description: "One-way Jellyfin playback synchronization with Filmber."
category: "General"
owner: "Filmber"
artifacts:
  - "Jellyfin.Plugin.FilmberSync.dll"
changelog: "Initial pairing, durable playback progress, and watched sync."
"""
path = pathlib.Path(output)
path.parent.mkdir(parents=True, exist_ok=True)
path.write_text(content, encoding="utf-8")
