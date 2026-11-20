"""Point the Merkez location at the door work-summary board."""

from __future__ import annotations

import importlib.util
from pathlib import Path

HERE = Path(__file__).resolve().parent
SPEC = importlib.util.spec_from_file_location("seed", HERE / "seed-abt-merkez-lokasyon.py")
SEED = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
SPEC.loader.exec_module(SEED)


def relation_id(value):
    if isinstance(value, dict):
        return value.get("__dataId") or value.get("dataId")
    return value or None


def main() -> None:
    token = SEED.request(
        "POST",
        "/keeper/api/auth/token",
        body={"username": "abt_admin", "password": SEED.password(), "domain": "abt"},
    )
    access = token.get("accessToken") if isinstance(token, dict) else None
    if not access:
        raise RuntimeError("Token alinamadi")

    location = SEED.find_one(access, "pdks_lokasyonlar", "locationKey", "merkez")
    location_id = SEED.data_id(location or {})
    if not location_id:
        raise RuntimeError("Merkez lokasyonu yok")

    body = {
        "locationKey": "merkez",
        "title": location.get("title") or "Merkez",
        "active": True if location.get("active") is None else location.get("active"),
        "dashboardSlug": "kapi-is-ozeti",
    }
    ayar = relation_id(location.get("ayar"))
    if ayar:
        body["ayar"] = ayar
    SEED.request("PUT", f"/data/api/v1/data/pdks_lokasyonlar/{location_id}", access, body)
    print("lokasyon panosu: kapi-is-ozeti")


if __name__ == "__main__":
    main()
