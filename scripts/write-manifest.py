#!/usr/bin/env python3
import json
import pathlib
import sys

output, zip_name, version, target_abi, checksum, timestamp, source_url = sys.argv[1:]
manifest = [
    {
        "guid": "791da508-0885-48c9-b41f-efdce37d1f4a",
        "name": "Filmber Sync",
        "description": "Save Jellyfin playback progress and watched status to Filmber.",
        "overview": "Connect each Jellyfin user to Filmber with a one-time code. No manual token is required.",
        "owner": "Filmber",
        "category": "General",
        "versions": [
            {
                "version": version,
                "changelog": "Separate Jellyfin onboarding, automatic token pairing, session expiry, and remote revoke.",
                "targetAbi": target_abi,
                "sourceUrl": source_url,
                "checksum": checksum,
                "timestamp": timestamp,
            }
        ],
    }
]
path = pathlib.Path(output)
path.parent.mkdir(parents=True, exist_ok=True)
path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
