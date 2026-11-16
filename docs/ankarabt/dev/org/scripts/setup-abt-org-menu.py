"""Add the organization chart page under the AnkaraBT side menu.

Re-running updates the same pageCode. The password is read from the izin setup
script and is not printed.
"""

from __future__ import annotations

import re
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

BASE_URL = "http://192.168.1.249:5040"
IZIN_SETUP = Path(__file__).resolve().parents[2] / "izin" / "scripts" / "setup-abt-izin.py"
PERMS = {
    "admins": {"view": True, "create": True, "update": True, "delete": True, "export": True},
    "managers": {"view": True, "create": True, "update": True, "delete": True, "export": True},
    "Yonetim": {"view": True, "create": True, "update": True, "delete": True, "export": True},
    "idari isler": {"view": True, "create": True, "update": True, "delete": False, "export": True},
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
        import json
        data = json.dumps(body, ensure_ascii=False).encode("utf-8")
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(BASE_URL + path, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            raw = resp.read().decode("utf-8")
            import json
            return json.loads(raw) if raw else None
    except urllib.error.HTTPError as ex:
        detail = ex.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"{method} {path} -> {ex.code} {detail}") from ex


def items(payload):
    if isinstance(payload, list):
        return payload
    if isinstance(payload, dict):
        for key in ("data", "items"):
            value = payload.get(key)
            if isinstance(value, list):
                return value
        return [payload]
    return []


def data_id(row: dict) -> str | None:
    return row.get("__dataId") or row.get("dataId")


def find_by(access: str, field: str, value: str) -> dict | None:
    filt = urllib.parse.quote(f"{field}:eq:{value}")
    rows = items(request("GET", f"/data/api/v1/data/@side_menu?limit=5&filter={filt}", access))
    return rows[0] if rows else None


def upsert(access: str, body: dict) -> str:
    existing = find_by(access, "pageCode", body["pageCode"])
    row_id = data_id(existing or {})
    if row_id:
        request("PUT", f"/data/api/v1/data/@side_menu/{row_id}", access, body)
        print(f"menu guncellendi: {body['title']}")
        return row_id
    created = request("POST", "/data/api/v1/data/@side_menu", access, body)
    row_id = data_id(created if isinstance(created, dict) else {})
    if not row_id:
        nested = created.get("data") if isinstance(created, dict) else None
        row_id = data_id(nested if isinstance(nested, dict) else {})
    if not row_id:
        row_id = data_id(find_by(access, "pageCode", body["pageCode"]) or {})
    if not row_id:
        raise RuntimeError(f"menu olusmadi: {body['pageCode']}")
    print(f"menu olustu: {body['title']}")
    return row_id


def main() -> None:
    token_payload = request(
        "POST",
        "/keeper/api/auth/token",
        body={"username": "abt_admin", "password": password(), "domain": "abt"},
    )
    access = token_payload.get("accessToken") if isinstance(token_payload, dict) else None
    if not access:
        raise RuntimeError("Token alinamadi")
    parent = find_by(access, "pageCode", "ankarabt")
    parent_id = data_id(parent or {})
    if not parent_id:
        raise RuntimeError("AnkaraBT menu basligi yok")
    upsert(access, {
        "order": 297,
        "itemType": "item",
        "level": 1,
        "parentId": parent_id,
        "pageType": "user",
        "pageCode": "organizasyon",
        "title": "Organizasyon",
        "icon": "HierarchyIcon",
        "iconType": "tabler",
        "to": "/apps/organizasyon",
        "type": "internal",
        "disabled": False,
        "permissions": {"groups": PERMS},
    })
    print("organizasyon menusu hazir")


if __name__ == "__main__":
    main()
