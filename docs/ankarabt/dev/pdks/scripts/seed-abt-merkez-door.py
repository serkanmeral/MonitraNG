"""Create or refresh the merkez door row and print its monitor URL.

The raw kiosk token is not stored. The dataset keeps only its SHA-256.
The admin password is read from the existing izin setup script and is not printed.
"""

from __future__ import annotations

import hashlib
import json
import re
import secrets
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

BASE_URL = "http://192.168.1.249:5040"
DOOR_KEY = "merkez"
TITLE = "Merkez Giriş"
UI = "http://localhost:3000"
IZIN_SETUP = Path(__file__).resolve().parents[2] / "izin" / "scripts" / "setup-abt-izin.py"


def password() -> str:
    text = IZIN_SETUP.read_text(encoding="utf-8")
    match = re.search(r'^PASSWORD\s*=\s*"([^"]+)"', text, re.M)
    if not match:
        raise RuntimeError("Izin kurulum dosyasinda sifre satiri yok")
    return match.group(1)


def request(method: str, path: str, token: str | None = None, body: object | None = None):
    data = None
    headers = {"Accept": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    if body is not None:
        data = json.dumps(body, ensure_ascii=False).encode("utf-8")
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(BASE_URL + path, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            raw = resp.read().decode("utf-8")
            return json.loads(raw) if raw else None
    except urllib.error.HTTPError as ex:
        detail = ex.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"{method} {path} -> {ex.code} {detail}") from ex


def items(payload):
    if isinstance(payload, list):
        return payload
    if isinstance(payload, dict):
        for key in ("data", "Data", "items", "Items"):
            value = payload.get(key)
            if isinstance(value, list):
                return value
            if isinstance(value, dict):
                return [value]
        return [payload]
    return []


def data_id(row: dict) -> str | None:
    return row.get("__dataId") or row.get("dataId") or row.get("DataId")


def main() -> None:
    token_payload = request(
        "POST",
        "/keeper/api/auth/token",
        body={"username": "abt_admin", "password": password(), "domain": "abt"},
    )
    access = token_payload.get("accessToken") if isinstance(token_payload, dict) else None
    if not access:
        raise RuntimeError("Token alinamadi")

    filt = urllib.parse.quote(f"doorKey:eq:{DOOR_KEY}")
    found = items(request("GET", f"/data/api/v1/data/pdks_kapilar?limit=5&filter={filt}", access))
    existing = next((row for row in found if row.get("doorKey") == DOOR_KEY), None)
    if existing and data_id(existing):
        print("kapı duruyor, jeton ve lokasyon aynen kaldı")
        return

    raw = secrets.token_urlsafe(32)
    body = {
        "doorKey": DOOR_KEY,
        "title": TITLE,
        "directionMode": "auto",
        "active": True,
        "kioskTokenHash": hashlib.sha256(raw.encode("utf-8")).hexdigest(),
    }
    request("POST", "/data/api/v1/data/pdks_kapilar", access, body)
    print("kapı olustu: merkez")
    print(f"ad: {TITLE}")
    print(f"url: {UI}/kapi/{DOOR_KEY}?k={urllib.parse.quote(raw, safe='')}")


if __name__ == "__main__":
    main()
