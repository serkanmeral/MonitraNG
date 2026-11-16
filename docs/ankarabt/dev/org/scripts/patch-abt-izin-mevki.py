"""Add an optional approver seat to the live leave request.

The field is izin_onayMevki. Onayla and Reddet read it. An empty field keeps
the existing group. Re-running does not remove other fields or transitions.

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
DOMAIN = "abt"
USERNAME = "abt_admin"
IZIN_SETUP = Path(__file__).resolve().parents[2] / "izin" / "scripts" / "setup-abt-izin.py"
FIELD_KEY = "izin_onayMevki"
APPROVE_KEYS = {"onayla", "reddet"}


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
            return resp.status, json.loads(raw) if raw else None
    except urllib.error.HTTPError as ex:
        detail = ex.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"{method} {path} -> {ex.code} {detail}") from ex


def items(payload):
    if payload is None:
        return []
    if isinstance(payload, list):
        return payload
    if isinstance(payload, dict):
        for key in ("data", "Data", "items", "Items", "results", "Results"):
            value = payload.get(key)
            if isinstance(value, list):
                return value
            if isinstance(value, dict):
                return [value]
        if payload.get("__dataId") or payload.get("dataId"):
            return [payload]
    return []


def data_id(row: dict) -> str | None:
    return row.get("__dataId") or row.get("dataId") or row.get("DataId")


def ref_id(value) -> str | None:
    if isinstance(value, str) and value.strip():
        return value.strip()
    if isinstance(value, dict):
        return data_id(value) or value.get("id")
    return None


def token() -> str:
    _, payload = request(
        "POST",
        "/keeper/api/auth/token",
        body={"username": USERNAME, "password": password(), "domain": DOMAIN},
    )
    access = payload.get("accessToken") if isinstance(payload, dict) else None
    if not access:
        raise RuntimeError("Token yanitinda accessToken yok")
    return access


def find_one(access: str, collection: str, name: str) -> dict:
    filt = urllib.parse.quote(f"name:eq:{name}")
    _, found = request("GET", f"/data/api/v1/data/{collection}?limit=1&expand=false&filter={filt}", access)
    rows = items(found)
    if not rows:
        raise RuntimeError(f"kayit yok: {collection} {name}")
    return rows[0]


def writable(row: dict) -> dict:
    skip = {"createdAt", "updatedAt", "createdBy", "updatedBy"}
    return {key: value for key, value in row.items() if not str(key).startswith("__") and key not in skip}


def save(access: str, collection: str, row: dict) -> None:
    row_id = data_id(row)
    if not row_id:
        raise RuntimeError(f"kimlik yok: {collection}")
    request("PUT", f"/data/api/v1/data/{collection}/{row_id}", access, writable(row))


def ensure_field(access: str) -> str:
    body = {
        "key": FIELD_KEY,
        "label": "Onaylayan mevki",
        "fieldType": "relation",
        "scope": "pool",
        "category": "leave",
        "relationDatasetName": "org_mevkiler",
        "options": {
            "lookup": {
                "source": "dataset",
                "presentation": "autocomplete",
                "valueField": "__dataId",
                "labelField": "ad",
                "searchFields": ["ad"],
                "pageSize": 50,
            }
        },
    }
    filt = urllib.parse.quote(f"key:eq:{FIELD_KEY}")
    _, found = request("GET", f"/data/api/v1/data/op_fields?limit=1&expand=false&filter={filt}", access)
    rows = items(found)
    if rows:
        row_id = data_id(rows[0])
        request("PUT", f"/data/api/v1/data/op_fields/{row_id}", access, body)
        print(f"alan duruyor: {FIELD_KEY}")
        return row_id
    _, created = request("POST", "/data/api/v1/data/op_fields", access, body)
    row_id = data_id(created if isinstance(created, dict) else {})
    if not row_id:
        rows = items(created)
        row_id = data_id(rows[0]) if rows else None
    if not row_id:
        raise RuntimeError("alan olusmadi")
    print(f"alan olustu: {FIELD_KEY}")
    return row_id


def append_id(values, row_id: str) -> bool:
    current = [ref_id(item) for item in (values or [])]
    current = [item for item in current if item]
    if row_id in current:
        return False
    current.append(row_id)
    values[:] = current
    return True


def show_field(row: dict) -> bool:
    changed = False
    layout = row.get("layout") if isinstance(row.get("layout"), dict) else {}
    sections = layout.get("sections") if isinstance(layout.get("sections"), list) else []
    if sections and isinstance(sections[0], dict):
        fields = sections[0].get("fields") if isinstance(sections[0].get("fields"), list) else []
        if FIELD_KEY not in fields:
            fields.append(FIELD_KEY)
            sections[0]["fields"] = fields
            layout["sections"] = sections
            row["layout"] = layout
            changed = True
    behaviors = row.get("fieldBehaviors") if isinstance(row.get("fieldBehaviors"), dict) else {}
    current = behaviors.get(FIELD_KEY) if isinstance(behaviors.get(FIELD_KEY), dict) else {}
    if current.get("visible") is not True:
        current["visible"] = True
        behaviors[FIELD_KEY] = current
        row["fieldBehaviors"] = behaviors
        changed = True
    return changed


def main() -> int:
    access = token()
    print("token alindi")
    field_id = ensure_field(access)

    workspace = find_one(access, "op_workspaces", "Personel izin")
    enabled = workspace.get("enabledFieldIds")
    if not isinstance(enabled, list):
        enabled = []
        workspace["enabledFieldIds"] = enabled
    if append_id(enabled, field_id):
        save(access, "op_workspaces", workspace)
        print("calisma alanina eklendi")
    else:
        print("calisma alaninda duruyor")

    for collection, name in (("op_forms", "IZN - Talep formu"), ("op_profiles", "IZN - Talep profili")):
        row = find_one(access, collection, name)
        if show_field(row):
            save(access, collection, row)
            print(f"forma eklendi: {name}")
        else:
            print(f"formda duruyor: {name}")

    flow = find_one(access, "op_state_flows", "IZN - Ana akis")
    touched = False
    for step in flow.get("transitions") or []:
        if step.get("transitionKey") not in APPROVE_KEYS:
            continue
        perms = step.get("permissions") if isinstance(step.get("permissions"), dict) else {}
        if perms.get("mevkiField") != FIELD_KEY:
            perms["mevkiField"] = FIELD_KEY
            step["permissions"] = perms
            touched = True
    if touched:
        save(access, "op_state_flows", flow)
        print("onay gecislerine mevki alani yazildi")
    else:
        print("onay gecislerinde mevki alani duruyor")

    workspace_id = data_id(workspace)
    request("POST", f"/operations/api/v1/workspaces/{workspace_id}/metadata-cache/reload", access, {})
    print("onbellek yenilendi")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
