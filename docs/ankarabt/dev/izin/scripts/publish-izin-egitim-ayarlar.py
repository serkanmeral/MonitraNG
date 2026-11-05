"""Publish leave training pages into Document Intelligence.

Folder chain under the library root:
  MonitraNG / Eğitim / IK / İzin / Ayarlar

If a root folder named Dokümanlar already exists, MonitraNG is created under it.
Re-running updates the same markdown page.
"""

from __future__ import annotations

import json
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

BASE_URL = "http://192.168.1.249:5040"
DOMAIN = "abt"
USERNAME = "abt_admin"
PASSWORD = "Admin123!"
API = "/documents/api/v1/resources"
ROOT = Path(__file__).resolve().parents[1] / "egitim"
PAGES = [
    {
        "folder": "Ayarlar",
        "title": "Ayarlar ne işe yarar",
        "aliases": ("Ayarlar",),
        "file": "ayarlar.md",
    },
    {
        "folder": "Ayarlar",
        "title": "Hak bantları ne işe yarar",
        "aliases": ("Hak bantları",),
        "file": "hak-bantlari.md",
    },
    {
        "folder": "Ayarlar",
        "title": "İzin türleri ne işe yarar",
        "aliases": ("İzin türleri",),
        "file": "izin-turleri.md",
    },
    {
        "folder": "Ayarlar",
        "title": "Tatil kalıpları ne işe yarar",
        "aliases": ("Tatil kalıpları",),
        "file": "tatil-kaliplari.md",
    },
    {
        "folder": "Ayarlar",
        "title": "Tatil günleri ne işe yarar",
        "aliases": ("Tatil günleri",),
        "file": "tatil-gunleri.md",
    },
]


def request(method: str, path: str, token: str | None = None, body: object | None = None):
    data = None
    headers = {"Accept": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    if body is not None:
        data = json.dumps(body, ensure_ascii=False).encode("utf-8")
        headers["Content-Type"] = "application/json; charset=utf-8"
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
        for key in ("items", "Items", "data", "Data"):
            value = payload.get(key)
            if isinstance(value, list):
                return value
        return [payload]
    return []


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


def children(access: str, parent_id: str | None) -> list:
    path = API + "/children"
    if parent_id:
        path += "?parentId=" + urllib.parse.quote(parent_id)
    return items(request("GET", path, access))


def ensure_folder(access: str, name: str, parent_id: str | None) -> str:
    for row in children(access, parent_id):
        if row.get("type") == "folder" and row.get("name") == name:
            print(f"klasor duruyor: {name} {row.get('id')}")
            return row["id"]
    body = {"name": name}
    if parent_id:
        body["parentId"] = parent_id
    created = request("POST", API + "/folder", access, body)
    folder_id = created.get("id") if isinstance(created, dict) else None
    if not folder_id:
        raise RuntimeError(f"Klasor olusmadi: {name} {created}")
    print(f"klasor olustu: {name} {folder_id}")
    return folder_id


def save_page(access: str, parent_id: str, title: str, aliases: tuple[str, ...], content: str) -> str:
    names = {title, *aliases}
    existing = None
    for row in children(access, parent_id):
        if row.get("type") == "markdown" and row.get("title") in names:
            existing = row
            break
    if existing:
        version = existing.get("currentVersionNumber") or 1
        request(
            "PUT",
            f"{API}/markdown/{existing['id']}",
            access,
            {
                "title": title,
                "content": content,
                "expectedVersionNumber": version,
                "isDraft": False,
            },
        )
        print(f"sayfa guncellendi: {existing['id']}")
        return existing["id"]
    created = request(
        "POST",
        API + "/markdown",
        access,
        {"parentId": parent_id, "title": title, "content": content, "isDraft": False},
    )
    page_id = created.get("id") if isinstance(created, dict) else None
    if not page_id:
        raise RuntimeError(f"Sayfa olusmadi: {created}")
    print(f"sayfa olustu: {page_id}")
    return page_id


def main() -> int:
    access = token()
    print("token alindi")
    roots = children(access, None)
    print("kok:", ", ".join(f"{row.get('name')}({row.get('type')})" for row in roots) or "(bos)")
    parent = None
    library = next(
        (
            row
            for row in roots
            if row.get("type") == "folder" and row.get("name") in ("Dokümanlar", "Dökümanlar", "Dokumanlar")
        ),
        None,
    )
    if library:
        parent = library["id"]
        print(f"kutuphane: {library.get('name')} {parent}")
    izin = parent
    for name in ("MonitraNG", "Eğitim", "IK", "İzin"):
        izin = ensure_folder(access, name, izin)
    for page in PAGES:
        folder_id = ensure_folder(access, page["folder"], izin)
        content = (ROOT / page["file"]).read_text(encoding="utf-8")
        page_id = save_page(access, folder_id, page["title"], page["aliases"], content)
        print(f"yol: Dokümanlar / MonitraNG / Eğitim / IK / İzin / {page['folder']} / {page['title']}")
        print(f"sayfa: /apps/document-intelligence/r/{page_id}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as ex:
        print(f"HATA: {ex}")
        raise SystemExit(1)
