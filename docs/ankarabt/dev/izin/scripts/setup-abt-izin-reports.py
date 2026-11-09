"""Install the leave balance report on the abt domain.

Kisi bakiyesi reads annual summary rows. Period and lifetime usage are expand tabs.
Re-running updates the same category and report by logical id.
The retired rpt_izin_kullanim report is removed.
"""

from __future__ import annotations

import json
import sys
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

BASE_URL = "http://192.168.1.249:5040"
DOMAIN = "abt"
USERNAME = "abt_admin"
PASSWORD = "Admin123!"
ROOT = Path(__file__).resolve().parents[1] / "datasets" / "izin_reports.json"


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
        for key in ("data", "items", "results"):
            value = payload.get(key)
            if isinstance(value, list):
                return value
        if payload.get("__dataId") or payload.get("id"):
            return [payload]
    return []


def data_id(row: dict) -> str | None:
    return row.get("__dataId") or row.get("dataId")


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


def upsert(access: str, dataset: str, logical_id: str, body: dict) -> None:
    now = datetime.now(timezone.utc).isoformat()
    filt = urllib.parse.quote(f"id:eq:{logical_id}")
    _, found = request("GET", f"/data/api/v1/data/{dataset}?limit=1&filter={filt}", access)
    existing = items(found)
    if existing:
        body["createdAt"] = existing[0].get("createdAt") or now
        body["updatedAt"] = now
        row_id = data_id(existing[0])
        request("PUT", f"/data/api/v1/data/{dataset}/{row_id}", access, body)
        print(f"guncellendi: {logical_id}")
        return
    body["createdAt"] = now
    body["updatedAt"] = now
    request("POST", f"/data/api/v1/data/{dataset}", access, body)
    print(f"olustu: {logical_id}")


def remove(access: str, dataset: str, logical_id: str) -> None:
    filt = urllib.parse.quote(f"id:eq:{logical_id}")
    _, found = request("GET", f"/data/api/v1/data/{dataset}?limit=1&filter={filt}", access)
    existing = items(found)
    if not existing:
        print(f"yok: {logical_id}")
        return
    row_id = data_id(existing[0])
    request("DELETE", f"/data/api/v1/data/{dataset}/{row_id}", access)
    print(f"silindi: {logical_id}")


def main() -> int:
    access = token()
    seed = json.loads(ROOT.read_text(encoding="utf-8"))
    upsert(access, "@reporting_categories", seed["category"]["id"], seed["category"])
    for report in seed["reports"]:
        upsert(access, "@reporting_reports", report["id"], report)
    remove(access, "@reporting_reports", "rpt_izin_kullanim")
    print("izin raporlari hazir")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as ex:
        print(ex, file=sys.stderr)
        sys.exit(1)
