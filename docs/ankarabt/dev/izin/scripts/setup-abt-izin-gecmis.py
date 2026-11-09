"""Write spreadsheet leave lines as past usage and repair split balances.

Usage rows do not change the balance. The same izKodu is not written twice.
Erroneous band-difference rows are removed, then the summary is rebuilt
so each person has one open-period card.
"""

from __future__ import annotations

import datetime
import importlib.util
import json
import sys
import urllib.parse
import zipfile
import xml.etree.ElementTree as ET
from collections import defaultdict
from decimal import Decimal
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REPORT = ROOT / "aktarim_raporu.md"
DATASETS = ROOT / "datasets" / "izin_datasets.json"
EXCEL = Path(r"C:\Users\serkan.meral\Desktop\AnkaraBT\IK\personel_izin_çizelgesi_SON\personel_izin_çizelgesi_SON.xlsx")
NS = {"m": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}
EPOCH = datetime.date(1899, 12, 30)

spec = importlib.util.spec_from_file_location(
    "akt", ROOT / "scripts" / "setup-abt-izin-aktarim.py"
)
akt = importlib.util.module_from_spec(spec)
spec.loader.exec_module(akt)


def sheet_rows(book: zipfile.ZipFile, shared: list[str], sheet_name: str):
    sheet = ET.fromstring(book.read(sheet_name))
    rows = defaultdict(dict)
    for row in sheet.findall("m:sheetData/m:row", NS):
        row_no = int(row.get("r"))
        for cell in row.findall("m:c", NS):
            ref = cell.get("r") or ""
            column = 0
            for ch in ref:
                if ch.isalpha():
                    column = column * 26 + (ord(ch) - 64)
                else:
                    break
            kind = cell.get("t")
            value = cell.find("m:v", NS)
            parsed = ""
            if kind == "s" and value is not None and value.text:
                parsed = shared[int(value.text)]
            elif value is not None and value.text is not None:
                parsed = value.text
            if parsed != "":
                rows[row_no][column] = parsed
    return rows


def excel_date(serial) -> datetime.date | None:
    try:
        return EPOCH + datetime.timedelta(days=float(serial))
    except (TypeError, ValueError):
        return None


def load_workbook():
    book = zipfile.ZipFile(EXCEL)
    shared = []
    root = ET.fromstring(book.read("xl/sharedStrings.xml"))
    for node in root.findall("m:si", NS):
        shared.append("".join(part.text or "" for part in node.iter("{http://schemas.openxmlformats.org/spreadsheetml/2006/main}t")))
    return book, shared


def sicil_by_name() -> dict[str, str]:
    mapping = {}
    for line in REPORT.read_text(encoding="utf-8").splitlines():
        if line.startswith("## Atlanan"):
            break
        if not line.startswith("| ") or "---" in line or line.startswith("| Sicil"):
            continue
        parts = [part.strip() for part in line.strip().strip("|").split("|")]
        if len(parts) < 2 or not parts[0] or " " in parts[0]:
            continue
        mapping[akt.fold(parts[1])] = parts[0]
    return mapping


def load_leaves(book, shared) -> list[dict]:
    rows = sheet_rows(book, shared, "xl/worksheets/sheet4.xml")
    leaves = []
    for row_no, cols in sorted(rows.items()):
        if row_no < 5:
            continue
        name = str(cols.get(2, "")).strip()
        if not name or name == "Adı Soyadı":
            continue
        start = excel_date(cols.get(3))
        end = excel_date(cols.get(4))
        days = akt.decimal_or_none(cols.get(7, ""))
        if start is None or end is None or days is None:
            continue
        leaves.append({
            "name": name,
            "start": start,
            "end": end,
            "days": days,
            "note": str(cols.get(8, "")).strip(),
        })
    return leaves


def cards_by_sicil(access: str) -> dict[str, dict]:
    rows = akt.items(akt.request("GET", "/data/api/v1/data/izin_personel?limit=200", access))
    found = {}
    for row in rows:
        sicil = str(row.get("sicil") or "")
        if sicil:
            found[sicil] = row
    return found


def set_summary_hook(access: str, enabled: bool) -> None:
    schemas = json.loads(DATASETS.read_text(encoding="utf-8"))
    hareket = next(schema for schema in schemas if schema["name"] == "izin_hareketleri")
    for action in hareket.get("queryActions") or []:
        if action.get("name") == "ozet":
            action["afterWrite"] = enabled
    current = akt.request("GET", "/data/api/v1/datasets/izin_hareketleri", access)
    if not isinstance(current, dict):
        current = akt.items(current)[0]
    category = current.get("category") or current.get("categoryId")
    if isinstance(category, dict):
        category = category.get("__dataId") or category.get("id") or category.get("dataId")
    body = {
        "description": hareket.get("description"),
        "forceSchema": True,
        "logging": hareket.get("logging", "none"),
        "publishMode": hareket.get("publish_mode", "none"),
        "fields": hareket.get("fields") or [],
        "validations": hareket.get("validations") or [],
        "queries": hareket.get("queries") or [],
        "queryActions": hareket.get("queryActions") or [],
        "indexList": hareket.get("indexList") or [],
    }
    if category:
        body["category"] = category
    akt.request("PUT", "/data/api/v1/datasets/izin_hareketleri", access, body)
    print(f"ozet tetigi: {enabled}")


def delete_where(access: str, dataset: str, filt: str | None, guard) -> int:
    removed = 0
    while True:
        path = f"/data/api/v1/data/{dataset}?limit=100"
        if filt:
            path += "&filter=" + urllib.parse.quote(filt)
        rows = akt.items(akt.request("GET", path, access))
        if not rows:
            return removed
        targets = [row for row in rows if guard(row)]
        if not targets:
            return removed
        for row in targets:
            akt.request("DELETE", f"/data/api/v1/data/{dataset}/{akt.data_id(row)}", access)
            removed += 1


def movement_body(person_id: str, tur_id: str, hire: datetime.date, leave: dict, code: str) -> dict:
    period = akt.period_start(hire, leave["start"])
    body = {
        "personelId": person_id,
        "turId": tur_id,
        "hareket": "kullanim",
        "gun": akt.as_json_number(leave["days"]),
        "donemBaslangic": akt.iso(period),
        "baslangic": akt.iso(leave["start"]),
        "bitis": akt.iso(leave["end"]),
        "bakiyeyeEtki": False,
        "kaynak": "dosya",
        "izKodu": code,
    }
    if leave["note"]:
        body["aciklama"] = leave["note"]
    return body


def main() -> int:
    access = akt.token()
    names = sicil_by_name()
    if len(names) < 40:
        raise RuntimeError(f"sicil eslesmesi eksik: {len(names)}")
    book, shared = load_workbook()
    leaves = load_leaves(book, shared)
    people = cards_by_sicil(access)
    annual = akt.find_one(access, "izin_turleri", "kod:eq:yillik")
    tur_id = akt.data_id(annual or {})
    if not tur_id:
        raise RuntimeError("yillik tur yok")

    set_summary_hook(access, False)
    removed = delete_where(
        access,
        "izin_hareketleri",
        "hareket:eq:bant_farki",
        lambda row: row.get("kaynak") in (None, "", "sistem") and not row.get("izKodu"),
    )
    print(f"bant farki silindi: {removed}")

    written = skipped = unchanged = unmatched = 0
    unknown = set()
    seen = defaultdict(int)
    for leave in leaves:
        sicil = names.get(akt.fold(leave["name"]))
        card = people.get(sicil or "")
        if not card:
            unmatched += 1
            unknown.add(leave["name"])
            continue
        hire_day = akt.day_of(card.get("iseGiris"))
        hire = datetime.date.fromisoformat(hire_day) if hire_day else None
        if hire is None:
            skipped += 1
            continue
        base = f"dosya-{sicil}-kullanim-{leave['start']:%Y%m%d}-{leave['end']:%Y%m%d}-{akt.as_json_number(leave['days'])}"
        seen[base] += 1
        code = base if seen[base] == 1 else f"{base}-{seen[base]}"
        body = movement_body(akt.data_id(card), tur_id, hire, leave, code)
        status = akt.upsert_movement(access, body)
        if status == "yazildi":
            written += 1
        elif status == "atlandi":
            unchanged += 1
        else:
            written += 1
        if (written + unchanged) % 50 == 0:
            print(f"izin {written + unchanged}")

    cleared = delete_where(access, "izin_ozet", None, lambda _row: True)
    print(f"ozet silindi: {cleared}")
    set_summary_hook(access, True)
    original = akt.request
    def long_request(method, path, token=None, body=None):
        previous = akt.urllib.request.urlopen
        def urlopen(req, timeout=120):
            return previous(req, timeout=600)
        akt.urllib.request.urlopen = urlopen
        try:
            return original(method, path, token, body)
        finally:
            akt.urllib.request.urlopen = previous
    long_request("POST", "/data/api/v1/data/izin_hareketleri/actions/ozet", access, {})
    print("ozet yenilendi")
    print(f"yazildi {written} duran {unchanged} atlanan {skipped} eslesmeyen {unmatched}")
    if unknown:
        print("eslesmeyen adlar:", ", ".join(sorted(unknown)))

    card = people.get("dgirisken")
    if card:
        filt = urllib.parse.quote(f"personelId:eq:{akt.data_id(card)}")
        summary = akt.items(akt.request("GET", f"/data/api/v1/data/izin_ozet?limit=10&filter={filt}", access))
        usage = akt.items(akt.request(
            "GET",
            "/data/api/v1/data/izin_hareketleri?limit=20&filter="
            + urllib.parse.quote(f"personelId:eq:{akt.data_id(card)},hareket:eq:kullanim"),
            access,
        ))
        print("danyen ozet", len(summary))
        for row in summary:
            print(
                " ",
                row.get("donemBaslangic"),
                "hak", row.get("hakedis"),
                "acilis", row.get("acilis"),
                "bakiye", row.get("bakiye"),
                "donem", row.get("buDonemGun"),
                "gecmis", row.get("gecmisGun"),
                "acik", row.get("acikDonem"),
            )
        print("danyen kullanim", len(usage))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as ex:
        print(ex, file=sys.stderr)
        sys.exit(1)
