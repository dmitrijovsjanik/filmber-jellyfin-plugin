#!/usr/bin/env python3
import json
import pathlib
import sys

output, zip_name, version, target_abi, checksum, timestamp = sys.argv[1:]
manifest = [
    {
        "guid": "791da508-0885-48c9-b41f-efdce37d1f4a",
        "name": "Filmber Sync",
        "description": "One-way Jellyfin playback synchronization with Filmber.",
        "overview": "Synchronize Jellyfin playback with Filmber.",
        "owner": "Filmber",
        "category": "General",
        "versions": [
            {
                "version": version,
                "changelog": "Initial pairing, durable playback progress, and watched sync.",
                "targetAbi": target_abi,
                "sourceUrl": f"http://host.docker.internal:8765/{zip_name}",
                "checksum": checksum,
                "timestamp": timestamp,
            }
        ],
    }
]
path = pathlib.Path(output)
path.parent.mkdir(parents=True, exist_ok=True)
path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
