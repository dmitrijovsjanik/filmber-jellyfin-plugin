#!/usr/bin/env python3
import argparse
import json
import uuid
from datetime import datetime, timedelta, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, urlparse


class Handler(BaseHTTPRequestHandler):
    output_path: Path
    pairings: dict[str, dict[str, str]] = {}

    def read_json(self) -> dict:
        if self.headers.get("Transfer-Encoding", "").lower() != "chunked":
            length = int(self.headers.get("Content-Length", "0"))
            return json.loads(self.rfile.read(length) or b"{}")

        body = bytearray()
        while True:
            size = int(self.rfile.readline().split(b";", 1)[0], 16)
            if size == 0:
                self.rfile.readline()
                break

            body.extend(self.rfile.read(size))
            self.rfile.read(2)

        return json.loads(body or b"{}")

    def send_json(self, status: int, payload: object) -> None:
        body = json.dumps(payload).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_POST(self) -> None:
        if self.path == "/api/external/pair-init":
            self.create_pairing()
            return

        if self.path == "/api/external/sync":
            self.accept_sync()
            return

        self.send_error(404)

    def do_GET(self) -> None:
        parsed = urlparse(self.path)
        if parsed.path != "/api/external/pair-poll":
            self.send_error(404)
            return

        pairing_id = parse_qs(parsed.query).get("id", [""])[0]
        pairing = self.pairings.get(pairing_id)
        if pairing is None:
            self.send_json(404, {"error": "not_found"})
            return

        expires_at = (datetime.now(timezone.utc) + timedelta(days=30)).isoformat()
        self.send_json(
            200,
            {
                "status": "approved",
                "token": "local-filmber-token",
                "user": {"id": "local-filmber-user", "firstName": "Local"},
                "session": {
                    "id": "local-session",
                    "expiresAt": expires_at,
                    "clientType": "jellyfin",
                    "externalUserId": pairing["externalUserId"],
                },
            },
        )

    def do_DELETE(self) -> None:
        if self.path != "/api/external/session":
            self.send_error(404)
            return

        if self.headers.get("Authorization") != "Bearer local-filmber-token":
            self.send_json(401, {"error": "unauthorized"})
            return

        self.send_json(200, {"success": True})

    def create_pairing(self) -> None:
        try:
            payload = self.read_json()
        except (ValueError, json.JSONDecodeError):
            self.send_error(400)
            return

        external_user_id = payload.get("externalUserId")
        if (
            payload.get("clientType") != "jellyfin"
            or not isinstance(external_user_id, str)
            or len(external_user_id) != 32
        ):
            self.send_json(400, {"error": "invalid_pairing_identity"})
            return

        pairing_id = str(uuid.uuid4())
        self.pairings[pairing_id] = {"externalUserId": external_user_id}
        self.send_json(
            200,
            {
                "pairingId": pairing_id,
                "code": "LOCAL1",
                "expiresAt": (
                    datetime.now(timezone.utc) + timedelta(minutes=10)
                ).isoformat(),
            },
        )

    def accept_sync(self) -> None:
        if self.headers.get("Authorization") != "Bearer local-filmber-token":
            self.send_json(401, {"error": "unauthorized"})
            return

        try:
            payload = self.read_json()
        except (ValueError, json.JSONDecodeError):
            self.send_error(400)
            return

        operations = payload.get("ops")
        if not isinstance(operations, list):
            self.send_json(400, {"error": "invalid_ops"})
            return

        with self.output_path.open("a", encoding="utf-8") as stream:
            stream.write(json.dumps(payload, ensure_ascii=False) + "\n")

        self.send_json(
            200,
            {
                "results": [
                    {
                        "ts": operation.get("ts"),
                        "clientOpId": operation.get("clientOpId"),
                        "ok": True,
                    }
                    for operation in operations
                ]
            },
        )

    def log_message(self, format_string: str, *args: object) -> None:
        print(format_string % args)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=8787)
    parser.add_argument("--output", default="docker/mock-events.jsonl")
    args = parser.parse_args()

    Handler.output_path = Path(args.output)
    Handler.output_path.parent.mkdir(parents=True, exist_ok=True)
    server = ThreadingHTTPServer(("0.0.0.0", args.port), Handler)
    print(f"Local Filmber contract simulator listening on port {args.port}")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
