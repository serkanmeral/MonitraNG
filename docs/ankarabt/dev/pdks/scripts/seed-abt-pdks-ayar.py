"""Write the institution hours row.

tekil is ayar. Entry 09:00, exit 18:00, tolerance 15 minutes.
A door or location that picks another row overrides this one.
The password is read from the izin setup script and is not printed.
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
BODY = {
    "tekil": "ayar",
    "girisSaati": "09:00",
    "cikisSaati": "18:00",
    "toleransDakika": 15,
    "panoYenilemeSaniye": 60,
    "karsilamaGiris": "İyi çalışmalar.",
    "karsilamaZamaninda": "Zamanında geldiğiniz için teşekkürler.",
    "karsilamaGec": "Yolunuz düştü, yine de hoş geldiniz.",
    "karsilamaCikis": "Güle güle.",
}


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
        data = json.dumps(body).encode("utf-8")
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

    filt = urllib.parse.quote("tekil:eq:ayar")
    rows = items(request("GET", f"/data/api/v1/data/pdks_ayarlar?limit=5&filter={filt}&expand=false", access))
    existing = next((row for row in rows if row.get("tekil") == "ayar"), None)
    row_id = data_id(existing or {})
    if row_id:
        request("PUT", f"/data/api/v1/data/pdks_ayarlar/{row_id}", access, BODY)
        print("saat guncellendi: 09:00 18:00 tolerans 15")
        return
    request("POST", "/data/api/v1/data/pdks_ayarlar", access, BODY)
    print("saat yazildi: 09:00 18:00 tolerans 15")


if __name__ == "__main__":
    main()
