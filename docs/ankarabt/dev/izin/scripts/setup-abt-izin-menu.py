"""Add the leave side menu under AnkaraBT.

Writes @side_menu rows. It does not turn on automated-form sideMenuConfig.
Re-running updates the same pageCode.
"""

from __future__ import annotations

import json
import urllib.error
import urllib.parse
import urllib.request

BASE_URL = "http://192.168.1.249:5040"
DOMAIN = "abt"
USERNAME = "abt_admin"
PASSWORD = "Admin123!"
ANKARA_PAGE = "ankarabt"
WORKSPACE_ID = "4398ffa9-7160-4058-8717-cc9e8fd585b0"
BOARD_ID = "6fac069d-c19e-4e1c-a911-d76b385958b2"

FULL = {
    "admins": {"view": True, "create": True, "update": True, "delete": True, "export": True},
    "managers": {"view": True, "create": True, "update": True, "delete": True, "export": True},
    "users": {"view": True, "create": True, "update": True, "delete": False, "export": False},
    "guests": {"view": True, "create": False, "update": False, "delete": False, "export": False},
    "Abt Calisan": {"view": False, "create": False, "update": False, "delete": False, "export": False},
    "Yonetim": {"view": True, "create": True, "update": True, "delete": True, "export": True},
    "idari isler": {"view": True, "create": True, "update": True, "delete": True, "export": True},
}
STAFF_VIEW = {
    **FULL,
    "Abt Calisan": {"view": True, "create": False, "update": False, "delete": False, "export": False},
    "users": {"view": True, "create": False, "update": False, "delete": False, "export": False},
}


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
    if payload is None:
        return []
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


def token() -> str:
    payload = request(
        "POST",
        "/keeper/api/auth/token",
        body={"username": USERNAME, "password": PASSWORD, "domain": DOMAIN},
    )
    access = payload.get("accessToken") if isinstance(payload, dict) else None
    if not access:
        raise RuntimeError("Token yanitinda accessToken yok")
    return access


def find_by_code(access: str, page_code: str) -> dict | None:
    filt = urllib.parse.quote(f"pageCode:eq:{page_code}")
    rows = items(request("GET", f"/data/api/v1/data/@side_menu?limit=5&filter={filt}", access))
    return rows[0] if rows else None


def upsert(access: str, body: dict) -> str:
    existing = find_by_code(access, body["pageCode"])
    if existing and data_id(existing):
        row_id = data_id(existing)
        request("PUT", f"/data/api/v1/data/@side_menu/{row_id}", access, body)
        print(f"guncellendi: {body['title']}")
        return row_id
    created = request("POST", "/data/api/v1/data/@side_menu", access, body)
    row_id = data_id(created if isinstance(created, dict) else {})
    if not row_id:
        row_id = data_id(find_by_code(access, body["pageCode"]) or {})
    if not row_id:
        raise RuntimeError(f"menu olusmadi: {body['pageCode']}")
    print(f"olustu: {body['title']} {row_id}")
    return row_id


def remove(access: str, page_code: str) -> None:
    existing = find_by_code(access, page_code)
    row_id = data_id(existing or {})
    if not row_id:
        print(f"yok: {page_code}")
        return
    request("DELETE", f"/data/api/v1/data/@side_menu/{row_id}", access)
    print(f"silindi: {page_code}")


def item(page_code: str, title: str, to: str, icon: str, order: int, parent_id: str, permissions: dict) -> dict:
    return {
        "order": order,
        "itemType": "item",
        "level": 2,
        "parentId": parent_id,
        "pageType": "user",
        "pageCode": page_code,
        "title": title,
        "icon": icon,
        "iconType": "tabler",
        "to": to,
        "type": "internal",
        "disabled": False,
        "permissions": {"groups": permissions},
    }


def main() -> int:
    access = token()
    print("token alindi")
    ankara = find_by_code(access, ANKARA_PAGE)
    if not ankara or not data_id(ankara):
        raise RuntimeError("AnkaraBT menu basligi yok")
    parent_id = upsert(access, {
        "order": 297,
        "itemType": "item",
        "level": 1,
        "parentId": data_id(ankara),
        "pageType": "user",
        "pageCode": "dynamicForms.izin.menuParent",
        "title": "Personel İzin",
        "icon": "CalendarIcon",
        "iconType": "tabler",
        "to": "",
        "type": "internal",
        "disabled": False,
    })
    board = (
        "/apps/operation-core/workspace"
        f"?workspaceId={WORKSPACE_ID}&boardId={BOARD_ID}"
    )
    children = [
        ("dynamicForms.izin.onay.menuTitle", "Onay kuyruğu", board, "ChecklistIcon", 298, STAFF_VIEW),
        ("dynamicForms.izin.personel.menuTitle", "Personel kartı", "/apps/automated-forms/view/izin-personel-form", "UsersIcon", 299, FULL),
        ("dynamicForms.izin.tanimlar.menuTitle", "Tanımlar", "/apps/izin/tanimlar", "SettingsIcon", 300, FULL),
        ("dynamicForms.izin.bakiye.menuTitle", "Kişi bakiyesi", "/apps/reporting/run/rpt_izin_bakiye", "ReportIcon", 306, FULL),
    ]
    for page_code, title, to, icon, order, perms in children:
        upsert(access, item(page_code, title, to, icon, order, parent_id, perms))
    for page_code in (
        "dynamicForms.izin.ayarlar.menuTitle",
        "dynamicForms.izin.bant.menuTitle",
        "dynamicForms.izin.tur.menuTitle",
        "dynamicForms.izin.neden.menuTitle",
        "dynamicForms.izin.kalip.menuTitle",
        "dynamicForms.izin.gun.menuTitle",
        "dynamicForms.izin.kullanim.menuTitle",
    ):
        remove(access, page_code)
    print("izin menusu hazir")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
