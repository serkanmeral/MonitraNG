"""Install PDKS datasets on the abt domain.

Creates or refreshes pdks_ayarlar, pdks_lokasyonlar, pdks_kapilar and pdks_gecisler.
Does not write the settings row. Entry and exit times are not known yet.

Password is read from PDKS_PASSWORD. Other values default to the abt gateway.
"""

from __future__ import annotations

import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

BASE_URL = os.environ.get("PDKS_BASE_URL", "http://192.168.1.249:5040").rstrip("/")
DOMAIN = os.environ.get("PDKS_DOMAIN", "abt")
USERNAME = os.environ.get("PDKS_USERNAME", "abt_admin")
PASSWORD = os.environ.get("PDKS_PASSWORD", "")
ROOT = Path(__file__).resolve().parents[1] / "datasets"


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
    return row.get("__dataId") or row.get("dataId") or row.get("DataId")


def token() -> str:
    if not PASSWORD:
        raise RuntimeError("PDKS_PASSWORD gerekli")
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
        if row.get("categoryName") == "Pdks":
            found = data_id(row)
            print(f"kategori var: {found}")
            return found or ""
    _, created = request(
        "POST",
        "/data/api/v1/dataset-categories",
        access,
        {
            "categoryName": "Pdks",
            "categoryDescription": "Kapi gecis kayitlari",
            "isSystemCategory": False,
        },
    )
    created_id = data_id(created if isinstance(created, dict) else {})
    if not created_id and items(created):
        created_id = data_id(items(created)[0])
    if not created_id:
        _, payload = request("GET", "/data/api/v1/dataset-categories?pageSize=200", access)
        for row in items(payload):
            if row.get("categoryName") == "Pdks":
                created_id = data_id(row)
                break
    if not created_id:
        raise RuntimeError("Pdks kategorisi olusmadi")
    print(f"kategori olustu: {created_id}")
    return created_id


def dataset_body(schema: dict, category_id: str) -> dict:
    return {
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


def install_datasets(access: str, category_id: str) -> None:
    schemas = json.loads((ROOT / "pdks_datasets.json").read_text(encoding="utf-8"))
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
        field_count = len(row.get("fields") or [])
        print(f"dataset {name} {action}: alan={field_count}")


def main() -> int:
    access = token()
    print("token alindi")
    category_id = ensure_category(access)
    install_datasets(access, category_id)
    print("pdks dataset kurulumu bitti")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as ex:
        print(str(ex), file=sys.stderr)
        raise SystemExit(1)
