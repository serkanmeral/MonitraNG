"""Install the Mng.Ui welcome-message dataset, form, and menu on the abt domain.

Re-running refreshes the same dataset name, formCode, and pageCode.
The password is read from the izin setup script and is not printed.
"""

from __future__ import annotations

import json
import re
import sys
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

BASE_URL = "http://192.168.1.249:5040"
DOMAIN = "abt"
USERNAME = "abt_admin"
ROOT = Path(__file__).resolve().parents[1] / "datasets"
IZIN_SETUP = Path(__file__).resolve().parents[2] / "izin" / "scripts" / "setup-abt-izin.py"

PERMS = {
    "admins": {"view": True, "create": True, "update": True, "delete": True, "export": True},
    "managers": {"view": True, "create": True, "update": True, "delete": True, "export": True},
    "Yonetim": {"view": True, "create": True, "update": True, "delete": True, "export": True},
    "idari isler": {"view": True, "create": True, "update": True, "delete": True, "export": True},
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
    if payload is None:
        return []
    if isinstance(payload, list):
        return payload
    if not isinstance(payload, dict):
        return []
    for key in ("data", "Data", "items", "Items", "results", "Results"):
        value = payload.get(key)
        if isinstance(value, list):
            return value
        if isinstance(value, dict):
            return [value]
    return [payload]


def data_id(row: dict) -> str | None:
    if not row:
        return None
    return row.get("__dataId") or row.get("dataId") or row.get("DataId")


def token() -> str:
    payload = request(
        "POST",
        "/keeper/api/auth/token",
        body={"username": USERNAME, "password": password(), "domain": DOMAIN},
    )
    access = payload.get("accessToken") if isinstance(payload, dict) else None
    if not access:
        raise RuntimeError("Token yanitinda accessToken yok")
    return access


def ensure_category(access: str) -> str:
    payload = request("GET", "/data/api/v1/dataset-categories?pageSize=200", access)
    for row in items(payload):
        if row.get("categoryName") == "Arayuz":
            found = data_id(row)
            print(f"kategori var: {found}")
            return found or ""
    created = request(
        "POST",
        "/data/api/v1/dataset-categories",
        access,
        {
            "categoryName": "Arayuz",
            "categoryDescription": "Mng.Ui karsilama mesajlari",
            "isSystemCategory": False,
        },
    )
    created_id = data_id(created if isinstance(created, dict) else {})
    if not created_id and items(created):
        created_id = data_id(items(created)[0])
    if not created_id:
        payload = request("GET", "/data/api/v1/dataset-categories?pageSize=200", access)
        for row in items(payload):
            if row.get("categoryName") == "Arayuz":
                created_id = data_id(row)
                break
    if not created_id:
        raise RuntimeError("Arayuz kategorisi olusmadi")
    print(f"kategori olustu: {created_id}")
    return created_id


def dataset_body(schema: dict, category_id: str) -> dict:
    body = {
        "description": schema.get("description"),
        "forceSchema": schema.get("forceSchema", True),
        "logging": schema.get("logging", "none"),
        "publishMode": schema.get("publish_mode", "none"),
        "fields": schema.get("fields") or [],
        "validations": schema.get("validations") or [],
        "queries": schema.get("queries") or [],
        "queryActions": schema.get("queryActions") or [],
        "indexList": schema.get("indexList") or [],
        "category": category_id,
    }
    if schema.get("permissions"):
        body["permissions"] = schema["permissions"]
    return body


def install_dataset(access: str, category_id: str) -> None:
    schemas = json.loads((ROOT / "ui_karsilama_datasets.json").read_text(encoding="utf-8"))
    for schema in schemas:
        name = schema["name"]
        body = dataset_body(schema, category_id)
        encoded = urllib.parse.quote(name)
        try:
            request("GET", f"/data/api/v1/datasets/{encoded}", access)
            exists = True
        except RuntimeError as ex:
            if "-> 404" not in str(ex):
                raise
            exists = False
        if exists:
            request("PUT", f"/data/api/v1/datasets/{encoded}", access, body)
            action = "guncellendi"
        else:
            body["name"] = name
            request("POST", "/data/api/v1/datasets", access, body)
            action = "olustu"
        detail = request("GET", f"/data/api/v1/datasets/{encoded}", access)
        row = detail if isinstance(detail, dict) and detail.get("name") else items(detail)[0]
        print(f"dataset {name} {action}: alan={len(row.get('fields') or [])}")


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


def main() -> int:
    access = token()
    print("token alindi")
    category_id = ensure_category(access)
    install_dataset(access, category_id)
    forms = json.loads((ROOT / "ui_karsilama_automated_forms.json").read_text(encoding="utf-8"))
    for form in forms:
        upsert_form(access, form)

    parent = find_by(access, "@side_menu", "pageCode", "ankarabt")
    parent_id = data_id(parent or {})
    if not parent_id:
        raise RuntimeError("AnkaraBT menu basligi yok")

    upsert_menu(
        access,
        {
            "order": 310,
            "itemType": "item",
            "level": 1,
            "parentId": parent_id,
            "pageType": "user",
            "pageCode": "ankarabt.karsilama",
            "title": "Karşılama mesajları",
            "icon": "MessageIcon",
            "iconType": "tabler",
            "to": "/apps/automated-forms/view/ui-karsilama-mesajlari-form",
            "type": "internal",
            "disabled": False,
            "permissions": {"groups": PERMS},
        },
    )
    print("karsilama kurulumu bitti")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as ex:
        print(str(ex), file=sys.stderr)
        raise SystemExit(1)
