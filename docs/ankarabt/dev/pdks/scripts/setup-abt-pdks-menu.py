"""Install PDKS forms and the side menu under AnkaraBT.

PDKS
  Tanımlamalar
    Lokasyonlar, Kapılar, Saatler
  Gün listesi
  Anlık durum
  Rapor

Forms live in @automated_forms. Their own side-menu switch stays off.
Re-running updates the same formCode and pageCode.
The password is read from the izin setup script and is not printed.
Delete stays off so a door or location key used by old passages is not removed.
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
FORMS = Path(__file__).resolve().parents[1] / "datasets" / "pdks_automated_forms.json"

PERMS = {
    "admins": {"view": True, "create": True, "update": True, "delete": False, "export": True},
    "managers": {"view": True, "create": True, "update": True, "delete": False, "export": True},
    "Yonetim": {"view": True, "create": True, "update": True, "delete": False, "export": True},
    "idari isler": {"view": True, "create": True, "update": True, "delete": False, "export": True},
}
VIEW = {
    "admins": {"view": True, "create": False, "update": False, "delete": False, "export": False},
    "managers": {"view": True, "create": False, "update": False, "delete": False, "export": False},
    "Yonetim": {"view": True, "create": False, "update": False, "delete": False, "export": False},
    "idari isler": {"view": True, "create": False, "update": False, "delete": False, "export": False},
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
        data = json.dumps(body, ensure_ascii=False).encode("utf-8")
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(BASE_URL + path, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            raw = resp.read().decode("utf-8")
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


def find_by(access: str, dataset: str, field: str, value: str) -> dict | None:
    filt = urllib.parse.quote(f"{field}:eq:{value}")
    rows = items(request("GET", f"/data/api/v1/data/{dataset}?limit=5&filter={filt}", access))
    return rows[0] if rows else None


def upsert_form(access: str, form: dict) -> None:
    code = form["formCode"]
    existing = find_by(access, "@automated_forms", "formCode", code)
    row_id = data_id(existing or {})
    if row_id:
        request("PUT", f"/data/api/v1/data/@automated_forms/{row_id}", access, form)
        print(f"form guncellendi: {code}")
        return
    request("POST", "/data/api/v1/data/@automated_forms", access, form)
    print(f"form olustu: {code}")


def upsert_menu(access: str, body: dict) -> str:
    existing = find_by(access, "@side_menu", "pageCode", body["pageCode"])
    row_id = data_id(existing or {})
    if row_id:
        request("PUT", f"/data/api/v1/data/@side_menu/{row_id}", access, body)
        print(f"menu guncellendi: {body['title']}")
        return row_id
    created = request("POST", "/data/api/v1/data/@side_menu", access, body)
    row_id = data_id(created if isinstance(created, dict) else {})
    if not row_id:
        row_id = data_id(find_by(access, "@side_menu", "pageCode", body["pageCode"]) or {})
    if not row_id:
        raise RuntimeError(f"menu olusmadi: {body['pageCode']}")
    print(f"menu olustu: {body['title']}")
    return row_id


def group(page_code: str, title: str, icon: str, order: int, parent_id: str, level: int) -> dict:
    return {
        "order": order,
        "itemType": "item",
        "level": level,
        "parentId": parent_id,
        "pageType": "user",
        "pageCode": page_code,
        "title": title,
        "icon": icon,
        "iconType": "tabler",
        "to": "",
        "type": "internal",
        "disabled": False,
        "permissions": {"groups": PERMS},
    }


def leaf(page_code: str, title: str, to: str, icon: str, order: int, parent_id: str) -> dict:
    body = group(page_code, title, icon, order, parent_id, 3)
    body["to"] = to
    return body


def main() -> None:
    token_payload = request(
        "POST",
        "/keeper/api/auth/token",
        body={"username": "abt_admin", "password": password(), "domain": "abt"},
    )
    access = token_payload.get("accessToken") if isinstance(token_payload, dict) else None
    if not access:
        raise RuntimeError("Token alinamadi")

    forms = json.loads(FORMS.read_text(encoding="utf-8"))
    for form in forms:
        upsert_form(access, form)

    parent = find_by(access, "@side_menu", "pageCode", "ankarabt")
    parent_id = data_id(parent or {})
    if not parent_id:
        raise RuntimeError("AnkaraBT menu basligi yok")

    pdks_id = upsert_menu(access, group("pdks", "PDKS", "DoorIcon", 296, parent_id, 1))
    defs_id = upsert_menu(access, group("pdks.tanimlar", "Tanımlamalar", "SettingsIcon", 1, pdks_id, 2))
    pages = [
        ("pdks.tanimlar.lokasyonlar", "Lokasyonlar", "/apps/automated-forms/view/pdks-lokasyonlar-form", "MapPinIcon", 1),
        ("pdks.tanimlar.kapilar", "Kapılar", "/apps/automated-forms/view/pdks-kapilar-form", "DoorIcon", 2),
        ("pdks.tanimlar.saatler", "Saatler", "/apps/automated-forms/view/pdks-ayarlar-form", "ClockIcon", 3),
    ]
    for page_code, title, to, icon, order in pages:
        upsert_menu(access, leaf(page_code, title, to, icon, order, defs_id))
    day_list = group("pdks.gun-listesi", "Gün listesi", "ListIcon", 2, pdks_id, 2)
    day_list["to"] = "/apps/pdks/gun-listesi"
    day_list["permissions"] = {"groups": VIEW}
    upsert_menu(access, day_list)
    live = group("pdks.anlik-durum", "Anlık durum", "UsersIcon", 3, pdks_id, 2)
    live["to"] = "/apps/pdks/anlik-durum"
    live["permissions"] = {"groups": VIEW}
    upsert_menu(access, live)
    report = group("pdks.rapor", "Rapor", "ReportIcon", 4, pdks_id, 2)
    report["to"] = "/apps/pdks/rapor"
    report["permissions"] = {"groups": VIEW}
    upsert_menu(access, report)
    print("pdks menusu hazir")


if __name__ == "__main__":
    main()
