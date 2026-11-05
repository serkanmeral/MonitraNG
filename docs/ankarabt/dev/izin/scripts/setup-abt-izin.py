"""Install leave datasets, seed rows, and automated forms on the abt domain.

Does not create the Operation Core workspace or the approval chain.
Re-running skips seed rows that already exist and refreshes schemas and forms.
"""

from __future__ import annotations

import json
import sys
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

BASE_URL = "http://192.168.1.249:5040"
DOMAIN = "abt"
USERNAME = "abt_admin"
PASSWORD = "Admin123!"
ROOT = Path(__file__).resolve().parents[1] / "datasets"
SEED_KEYS = {
    "izin_ayarlar": "tekil",
    "izin_hak_bantlari": "kod",
    "izin_turleri": "kod",
    "izin_mazeret_nedenleri": "kod",
    "izin_tatil_kaliplari": "kod",
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
            return resp.status, json.loads(raw) if raw else None
    except urllib.error.HTTPError as ex:
        detail = ex.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"{method} {path} -> {ex.code} {detail}") from ex


def items(payload):
    if payload is None:
        return []
    if isinstance(payload, list):
        return payload
    for key in ("data", "Data", "items", "Items", "results", "Results"):
        value = payload.get(key) if isinstance(payload, dict) else None
        if isinstance(value, list):
            return value
        if isinstance(value, dict):
            return [value]
    if isinstance(payload, dict):
        return [payload]
    return []


def data_id(row: dict) -> str | None:
    return row.get("__dataId") or row.get("dataId") or row.get("DataId")


def token() -> str:
    _, payload = request(
        "POST",
        "/keeper/api/auth/token",
        body={"username": USERNAME, "password": PASSWORD, "domain": DOMAIN},
    )
    access = payload.get("accessToken") if isinstance(payload, dict) else None
    if not access:
        raise RuntimeError("Token yanitinda accessToken yok")
    return access


def ensure_category(access: str) -> str:
    _, payload = request("GET", "/data/api/v1/dataset-categories?pageSize=200", access)
    for row in items(payload):
        if row.get("categoryName") == "Izin":
            found = data_id(row)
            print(f"kategori var: {found}")
            return found
    _, created = request(
        "POST",
        "/data/api/v1/dataset-categories",
        access,
        {
            "categoryName": "Izin",
            "categoryDescription": "Personel izin sozlukleri",
            "isSystemCategory": False,
        },
    )
    created_id = data_id(created if isinstance(created, dict) else {}) or data_id(items(created)[0])
    if not created_id:
        _, payload = request("GET", "/data/api/v1/dataset-categories?pageSize=200", access)
        for row in items(payload):
            if row.get("categoryName") == "Izin":
                created_id = data_id(row)
                break
    if not created_id:
        raise RuntimeError("Izin kategorisi olusmadi")
    print(f"kategori olustu: {created_id}")
    return created_id


def dataset_body(schema: dict, category_id: str | None) -> dict:
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
    }
    if category_id:
        body["category"] = category_id
    return body


def install_datasets(access: str, category_id: str) -> None:
    schemas = json.loads((ROOT / "izin_datasets.json").read_text(encoding="utf-8"))
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
        _, detail = request("GET", f"/data/api/v1/datasets/{encoded}", access)
        row = detail if isinstance(detail, dict) and detail.get("name") else items(detail)[0]
        print(
            f"dataset {name} {action}: sorgu={row.get('queriesCount', len(row.get('queries') or []))} "
            f"aksiyon={len(row.get('queryActions') or [])}"
        )


def install_seed(access: str) -> None:
    seed = json.loads((ROOT / "izin_seed.json").read_text(encoding="utf-8"))
    for dataset, rows in seed.items():
        key = SEED_KEYS[dataset]
        for row in rows:
            value = row[key]
            filt = urllib.parse.quote(f"{key}:eq:{value}")
            _, found = request("GET", f"/data/api/v1/data/{dataset}?limit=1&filter={filt}", access)
            if items(found):
                print(f"tohum duruyor: {dataset}.{value}")
                continue
            request("POST", f"/data/api/v1/data/{dataset}", access, row)
            print(f"tohum yazildi: {dataset}.{value}")


def install_forms(access: str) -> None:
    forms = json.loads((ROOT / "izin_automated_forms.json").read_text(encoding="utf-8"))
    for form in forms:
        code = form["formCode"]
        filt = urllib.parse.quote(f"formCode:eq:{code}")
        _, found = request("GET", f"/data/api/v1/data/@automated_forms?limit=1&filter={filt}", access)
        existing = items(found)
        if existing:
            form_id = data_id(existing[0])
            request("PUT", f"/data/api/v1/data/@automated_forms/{form_id}", access, form)
            print(f"form guncellendi: {code}")
        else:
            request("POST", "/data/api/v1/data/@automated_forms", access, form)
            print(f"form olustu: {code}")


def main() -> int:
    access = token()
    print("token alindi")
    category_id = ensure_category(access)
    install_datasets(access, category_id)
    install_seed(access)
    install_forms(access)
    print("izin kurulumu bitti")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as ex:
        print(f"HATA: {ex}", file=sys.stderr)
        sys.exit(1)
