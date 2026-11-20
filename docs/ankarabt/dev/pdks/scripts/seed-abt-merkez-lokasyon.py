"""Attach the existing merkez door to a Merkez location.

The location key is merkez. The door key becomes merkez-giris.
The kiosk token hash is kept, so the monitor token does not change.
The admin password is read from the izin setup script and is not printed.
"""

from __future__ import annotations

import json
import re
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

BASE_URL = "http://192.168.1.249:5040"
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


def find_one(access: str, dataset: str, field: str, value: str) -> dict | None:
    filt = urllib.parse.quote(f"{field}:eq:{value}")
    rows = items(request("GET", f"/data/api/v1/data/{dataset}?limit=5&filter={filt}&expand=false", access))
    return next((row for row in rows if row.get(field) == value), None)


def main() -> None:
    token_payload = request(
        "POST",
        "/keeper/api/auth/token",
        body={"username": "abt_admin", "password": password(), "domain": "abt"},
    )
    access = token_payload.get("accessToken") if isinstance(token_payload, dict) else None
    if not access:
        raise RuntimeError("Token alinamadi")

    location = find_one(access, "pdks_lokasyonlar", "locationKey", "merkez")
    if location and data_id(location):
        location_id = data_id(location)
        print("lokasyon duruyor: merkez")
    else:
        created = request(
            "POST",
            "/data/api/v1/data/pdks_lokasyonlar",
            access,
            {"locationKey": "merkez", "title": "Merkez", "active": True},
        )
        location_id = data_id(created if isinstance(created, dict) else {})
        if not location_id:
            rows = items(created)
            location_id = data_id(rows[0]) if rows else None
        if not location_id:
            location = find_one(access, "pdks_lokasyonlar", "locationKey", "merkez")
            location_id = data_id(location or {})
        if not location_id:
            raise RuntimeError("Merkez lokasyonu olusmadi")
        print(f"lokasyon olustu: merkez {location_id}")

    door = find_one(access, "pdks_kapilar", "doorKey", "merkez-giris")
    renamed = False
    if door is None:
        door = find_one(access, "pdks_kapilar", "doorKey", "merkez")
        renamed = door is not None
    if not door or not data_id(door):
        raise RuntimeError("Baglanacak kapi yok")

    body = {
        "doorKey": "merkez-giris",
        "title": door.get("title") or "Merkez Giriş",
        "lokasyon": location_id,
        "directionMode": door.get("directionMode") or "auto",
        "active": True if door.get("active") is None else door.get("active"),
        "kioskTokenHash": door.get("kioskTokenHash"),
    }
    if door.get("dashboardSlug"):
        body["dashboardSlug"] = door.get("dashboardSlug")
    request("PUT", f"/data/api/v1/data/pdks_kapilar/{data_id(door)}", access, body)
    print("kapı bağlandı: merkez-giris -> merkez")
    if renamed:
        print("kapı anahtarı merkez iken merkez-giris oldu")


if __name__ == "__main__":
    main()
