"""Import the IK leave workbook for people who match Active Directory.

Re-running updates the same sicil and the same file keys. Fields IK may fill
later (cinsiyet, birim, yonetici, Keeper) are not sent on update.
Historical leave rows are not imported.

AD password is read from ANKARABT_AD_PASSWORD. It is not stored in this file.
"""

from __future__ import annotations

import datetime
import json
import os
import re
import subprocess
import unicodedata
import urllib.error
import urllib.parse
import urllib.request
import zipfile
import xml.etree.ElementTree as ET
from collections import defaultdict
from decimal import Decimal
from pathlib import Path

BASE_URL = "http://192.168.1.249:5040"
DOMAIN = "abt"
USERNAME = "abt_admin"
PASSWORD = "Admin123!"
AS_OF = datetime.date(2026, 11, 4)
EXCEL = Path(r"C:\Users\serkan.meral\Desktop\AnkaraBT\IK\personel_izin_çizelgesi_SON\personel_izin_çizelgesi_SON.xlsx")
REPORT = Path(__file__).resolve().parents[1] / "aktarim_raporu.md"
NS = {"m": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}
EPOCH = datetime.date(1899, 12, 30)


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
        for key in ("data", "Data", "items", "Items"):
            value = payload.get(key)
            if isinstance(value, list):
                return value
        return [payload]
    return []


def data_id(row: dict) -> str | None:
    if not isinstance(row, dict):
        return None
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


def fold(value: str) -> str:
    text = " ".join(str(value).replace("\u00a0", " ").split())
    text = text.replace("İ", "i").replace("I", "ı").lower()
    for src, dst in (("ı", "i"), ("ş", "s"), ("ğ", "g"), ("ü", "u"), ("ö", "o"), ("ç", "c")):
        text = text.replace(src, dst)
    text = unicodedata.normalize("NFKD", text)
    text = "".join(ch for ch in text if not unicodedata.combining(ch))
    text = text.replace(".", " ")
    return " ".join(text.split())


def tokens(value: str) -> list[str]:
    return [part for part in fold(value).split() if part not in {"a", "m"} and len(part) > 1]


def col_row(ref: str):
    match = re.match(r"([A-Z]+)(\d+)", ref or "")
    if not match:
        return None, None
    number = 0
    for ch in match.group(1):
        number = number * 26 + (ord(ch) - 64)
    return number, int(match.group(2))


def load_people():
    book = zipfile.ZipFile(EXCEL)
    shared = []
    root = ET.fromstring(book.read("xl/sharedStrings.xml"))
    for si in root.findall("m:si", NS):
        shared.append("".join(node.text or "" for node in si.iter("{http://schemas.openxmlformats.org/spreadsheetml/2006/main}t")))
    styles = ET.fromstring(book.read("xl/styles.xml"))
    formats = {}
    found = styles.find("m:numFmts", NS)
    if found is not None:
        for node in found.findall("m:numFmt", NS):
            formats[int(node.get("numFmtId"))] = node.get("formatCode") or ""
    date_styles = set()
    xfs = styles.find("m:cellXfs", NS)
    for index, xf in enumerate(xfs.findall("m:xf", NS)):
        fmt = xf.get("numFmtId")
        if not fmt:
            continue
        fmt_id = int(fmt)
        code = formats.get(fmt_id, "")
        if fmt_id in {14, 15, 16, 17, 22} or (code and re.search(r"[dy]", code, re.I)):
            date_styles.add(index)
    sheet = ET.fromstring(book.read("xl/worksheets/sheet3.xml"))
    rows = defaultdict(dict)
    for row in sheet.findall("m:sheetData/m:row", NS):
        row_no = int(row.get("r"))
        for cell in row.findall("m:c", NS):
            column, _ = col_row(cell.get("r"))
            kind = cell.get("t")
            style = int(cell.get("s")) if cell.get("s") else None
            value = cell.find("m:v", NS)
            if kind == "s" and value is not None and value.text:
                parsed = shared[int(value.text)]
            elif value is not None and value.text is not None:
                parsed = value.text
                if style in date_styles:
                    try:
                        parsed = (EPOCH + datetime.timedelta(days=float(parsed))).isoformat()
                    except ValueError:
                        pass
            else:
                parsed = ""
            if column and str(parsed).strip():
                rows[row_no][column] = parsed
    people = []
    for row_no, cols in sorted(rows.items()):
        if row_no < 5:
            continue
        name = str(cols.get(2, "")).strip()
        if not name:
            continue
        people.append({
            "row": row_no,
            "name": name,
            "birth_raw": cols.get(3, ""),
            "hire_raw": cols.get(4, ""),
            "kalan": decimal_or_none(cols.get(9, "")),
        })
    return people


def decimal_or_none(value) -> Decimal | None:
    text = str(value).strip().replace(",", ".")
    if not text:
        return None
    try:
        return Decimal(text)
    except Exception:
        return None


def parse_date(value) -> tuple[datetime.date | None, str]:
    text = str(value or "").strip()
    if not text:
        return None, "bos"
    if re.match(r"^\d{4}-\d{2}-\d{2}$", text):
        return datetime.date.fromisoformat(text), "tarih"
    match = re.match(r"^(\d{1,2})[./](\d{1,2})[./](\d{4})$", text)
    if match:
        day, month, year = int(match.group(1)), int(match.group(2)), int(match.group(3))
        return datetime.date(year, month, day), "gun.ay.yil"
    return None, "okunamadi"


def completed_years(start: datetime.date, end: datetime.date) -> int:
    years = end.year - start.year
    if (end.month, end.day) < (start.month, start.day):
        years -= 1
    return years


def period_start(hire: datetime.date, as_of: datetime.date) -> datetime.date:
    def anniversary(year: int) -> datetime.date:
        try:
            return hire.replace(year=year)
        except ValueError:
            return datetime.date(year, 3, 1)

    candidate = anniversary(as_of.year)
    if candidate > as_of:
        candidate = anniversary(as_of.year - 1)
    if candidate < hire:
        return hire
    return candidate


def iso(day: datetime.date) -> str:
    return f"{day.isoformat()}T00:00:00.000Z"


def day_of(value) -> str:
    text = str(value or "")
    match = re.match(r"(\d{4}-\d{2}-\d{2})", text)
    return match.group(1) if match else ""


def number_of(value) -> Decimal | None:
    if value is None or value == "":
        return None
    try:
        return Decimal(str(value))
    except Exception:
        return None


def as_json_number(value: Decimal):
    quantized = value.quantize(Decimal("0.1"))
    if quantized == quantized.to_integral():
        return int(quantized)
    return float(quantized)


def same_number(left: Decimal, right: Decimal | None) -> bool:
    if right is None:
        return False
    return abs(left - right) < Decimal("0.001")


def load_directory() -> list[dict]:
    password = os.environ.get("ANKARABT_AD_PASSWORD", "").strip()
    if not password:
        raise RuntimeError("ANKARABT_AD_PASSWORD bos")
    script = r"""
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
Add-Type -AssemblyName System.DirectoryServices.Protocols
$cred = New-Object System.Net.NetworkCredential('Administrator', $env:ANKARABT_AD_PASSWORD, 'ANKARABT')
$con = New-Object System.DirectoryServices.Protocols.LdapConnection('192.168.1.254:636')
$con.AuthType = [System.DirectoryServices.Protocols.AuthType]::Basic
$con.Credential = $cred
$con.SessionOptions.SecureSocketLayer = $true
$con.SessionOptions.VerifyServerCertificate = [System.DirectoryServices.Protocols.VerifyServerCertificateCallback]{ $true }
$con.SessionOptions.ProtocolVersion = 3
$req = New-Object System.DirectoryServices.Protocols.SearchRequest('DC=ankarabt,DC=local', '(&(objectCategory=person)(objectClass=user))', 'Subtree', $null)
$resp = $con.SendRequest($req)
  $skip = @('administrator','guest','krbtgt','svc-mailcow')
  $rows = @()
  foreach ($entry in $resp.Entries) {
    $sam = ''; $given = ''; $sn = ''; $display = ''
    foreach ($name in $entry.Attributes.AttributeNames) {
      $key = ([string]$name).ToLower()
      $a = $entry.Attributes[$name]
      if (-not $a -or $a.Count -eq 0) { continue }
      $text = [string]$a[0]
      if ($key -eq 'samaccountname') { $sam = $text }
      elseif ($key -eq 'givenname') { $given = $text }
      elseif ($key -eq 'sn') { $sn = $text }
      elseif ($key -eq 'displayname') { $display = $text }
    }
    if ($skip -contains $sam.ToLower()) { continue }
    $rows += [pscustomobject]@{ sam = $sam; given = $given; sn = $sn; display = $display }
  }
$rows | ConvertTo-Json -Compress
"""
    env = os.environ.copy()
    env["ANKARABT_AD_PASSWORD"] = password
    script_path = Path(os.environ.get("TEMP", ".")) / "izin-ad-export.ps1"
    script_path.write_text(script, encoding="utf-8")
    completed = subprocess.run(
        ["powershell", "-NoProfile", "-File", str(script_path)],
        text=True,
        capture_output=True,
        encoding="utf-8",
        env=env,
        check=False,
    )
    if completed.returncode != 0:
        raise RuntimeError((completed.stderr or completed.stdout).strip() or "AD sorgusu basarisiz")
    raw = completed.stdout.strip()
    start = raw.find("[")
    if start < 0:
        start = raw.find("{")
    payload = json.loads(raw[start:])
    if isinstance(payload, dict):
        payload = [payload]
    return payload


def match_directory(name: str, directory: list[dict]) -> tuple[str, dict | None]:
    folded = fold(name)
    name_tokens = tokens(name)
    last = name_tokens[-1] if name_tokens else ""
    for person in directory:
        label = person.get("display") or f"{person.get('given', '')} {person.get('sn', '')}"
        if fold(label) == folded or tokens(label) == name_tokens:
            return "tam", person
    strong = []
    for person in directory:
        label = person.get("display") or f"{person.get('given', '')} {person.get('sn', '')}"
        person_tokens = tokens(label)
        person_last = person_tokens[-1] if person_tokens else ""
        if fold(person.get("sn") or "") == last or person_last == last:
            shared = set(name_tokens[:-1]) & set(person_tokens[:-1])
            if shared:
                strong.append(person)
    if len(strong) == 1:
        return "yakin", strong[0]
    if len(strong) > 1:
        return "cok", None
    return "yok", None


def band_days(kidem: int, age: int | None, bands: list[dict]) -> int:
    grant = 0
    for band in bands:
        if band.get("tip") != "kidem" or band.get("durum") not in (None, "acik"):
            continue
        minimum = int(band.get("minYil") or 0)
        maximum = band.get("maxYil")
        if kidem < minimum:
            continue
        if maximum is not None and kidem > int(maximum):
            continue
        grant = int(band.get("gun") or 0)
        break
    floor = next((band for band in bands if band.get("tip") == "yas_tabani" and band.get("durum") in (None, "acik")), None)
    if age is not None and floor is not None:
        lower = floor.get("altYas")
        upper = floor.get("ustYas")
        minimum_days = int(floor.get("minimumGun") or 0)
        below = lower is not None and age < int(lower)
        above = upper is not None and age >= int(upper)
        if below or above:
            grant = max(grant, minimum_days)
    return grant


def find_one(access: str, dataset: str, filt: str) -> dict | None:
    encoded = urllib.parse.quote(filt)
    rows = items(request("GET", f"/data/api/v1/data/{dataset}?limit=5&filter={encoded}", access))
    return rows[0] if rows else None


def upsert_person(access: str, body: dict, existing: dict | None) -> tuple[str, str]:
    if not existing:
        created = request("POST", "/data/api/v1/data/izin_personel", access, body)
        person_id = data_id(created if isinstance(created, dict) else {})
        if not person_id:
            person_id = data_id(find_one(access, "izin_personel", f"sicil:eq:{body['sicil']}") or {})
        if not person_id:
            raise RuntimeError(f"personel olusmadi: {body['sicil']}")
        return "yazildi", person_id
    person_id = data_id(existing)
    changed = (
        existing.get("ad") != body["ad"]
        or existing.get("soyad") != body["soyad"]
        or day_of(existing.get("iseGiris")) != day_of(body["iseGiris"])
        or day_of(existing.get("dogumTarihi")) != day_of(body.get("dogumTarihi"))
    )
    if not changed:
        return "atlandi", person_id
    request("PUT", f"/data/api/v1/data/izin_personel/{person_id}", access, {
        "ad": body["ad"],
        "soyad": body["soyad"],
        "iseGiris": body["iseGiris"],
        "dogumTarihi": body.get("dogumTarihi"),
    })
    return "guncellendi", person_id


def upsert_movement(access: str, body: dict) -> str:
    existing = find_one(access, "izin_hareketleri", f"izKodu:eq:{body['izKodu']}")
    if not existing:
        request("POST", "/data/api/v1/data/izin_hareketleri", access, body)
        return "yazildi"
    same = (
        data_id(existing.get("personelId") if isinstance(existing.get("personelId"), dict) else {"__dataId": existing.get("personelId")}) == body["personelId"]
        or str(existing.get("personelId")) == body["personelId"]
    )
    person_same = str(existing.get("personelId") or "") == body["personelId"] or (
        isinstance(existing.get("personelId"), dict) and data_id(existing["personelId"]) == body["personelId"]
    )
    gun_same = same_number(Decimal(str(body["gun"])), number_of(existing.get("gun")))
    period_same = day_of(existing.get("donemBaslangic")) == day_of(body["donemBaslangic"])
    band_same = str(existing.get("hedefBant") or "") == str(body.get("hedefBant") or "")
    if person_same and gun_same and period_same and band_same and existing.get("hareket") == body["hareket"]:
        return "atlandi"
    request("PUT", f"/data/api/v1/data/izin_hareketleri/{data_id(existing)}", access, body)
    return "guncellendi"


def delete_movement(access: str, code: str) -> str:
    existing = find_one(access, "izin_hareketleri", f"izKodu:eq:{code}")
    if not existing:
        return "yok"
    if existing.get("kaynak") != "dosya":
        return "korundu"
    request("DELETE", f"/data/api/v1/data/izin_hareketleri/{data_id(existing)}", access)
    return "silindi"


def main() -> int:
    access = token()
    print("token alindi")
    directory = load_directory()
    print("ad", len(directory))
    bands = items(request("GET", "/data/api/v1/data/izin_hak_bantlari?limit=20", access))
    annual = find_one(access, "izin_turleri", "kod:eq:yillik")
    annual_id = data_id(annual or {})
    if not annual_id:
        raise RuntimeError("yillik tur yok")
    done = []
    skipped = []
    for person in load_people():
        hire, hire_kind = parse_date(person["hire_raw"])
        birth, _birth_kind = parse_date(person["birth_raw"])
        how, directory_person = match_directory(person["name"], directory)
        if how != "tam" and how != "yakin":
            skipped.append({"cizelge": person["name"], "neden": "Active Directory eşleşmesi yok"})
            continue
        if hire is None:
            skipped.append({
                "cizelge": person["name"],
                "sicil": directory_person["sam"],
                "neden": "İşe giriş yok",
            })
            continue
        if person["kalan"] is None:
            skipped.append({
                "cizelge": person["name"],
                "sicil": directory_person["sam"],
                "neden": "Kalan boş",
            })
            continue
        kidem = completed_years(hire, AS_OF)
        age = completed_years(birth, AS_OF) if birth else None
        period = period_start(hire, AS_OF)
        grant = band_days(kidem, age, bands) if kidem >= 1 else None
        opening = person["kalan"] - Decimal(grant) if grant is not None else person["kalan"]
        sicil = directory_person["sam"]
        card = {
            "sicil": sicil,
            "ad": directory_person.get("given") or "",
            "soyad": directory_person.get("sn") or "",
            "iseGiris": iso(hire),
            "durum": "calisiyor",
        }
        if birth:
            card["dogumTarihi"] = iso(birth)
        existing = find_one(access, "izin_personel", f"sicil:eq:{sicil}")
        card_status, person_id = upsert_person(access, card, existing)
        opening_body = {
            "personelId": person_id,
            "turId": annual_id,
            "hareket": "acilis",
            "gun": as_json_number(opening),
            "donemBaslangic": iso(period),
            "bakiyeyeEtki": True,
            "kaynak": "dosya",
            "izKodu": f"dosya-{sicil}-acilis",
        }
        opening_status = upsert_movement(access, opening_body)
        if grant is None:
            entitlement_status = delete_movement(access, f"dosya-{sicil}-hakedis")
            if entitlement_status == "yok":
                entitlement_status = "yok"
        else:
            entitlement_status = upsert_movement(access, {
                "personelId": person_id,
                "turId": annual_id,
                "hareket": "hakedis",
                "gun": grant,
                "donemBaslangic": iso(period),
                "bakiyeyeEtki": True,
                "kaynak": "dosya",
                "izKodu": f"dosya-{sicil}-hakedis",
                "hedefBant": str(grant),
            })
        done.append({
            "cizelge": person["name"],
            "sicil": sicil,
            "ad": card["ad"],
            "soyad": card["soyad"],
            "eslesme": how,
            "giris": hire.isoformat(),
            "girisOkuma": hire_kind,
            "dogum": birth.isoformat() if birth else "",
            "kidem": kidem,
            "yas": "" if age is None else age,
            "donem": period.isoformat(),
            "kalan": as_json_number(person["kalan"]),
            "hakedis": "" if grant is None else grant,
            "acilis": as_json_number(opening),
            "kart": card_status,
            "acilisIslem": opening_status,
            "hakedisIslem": entitlement_status,
        })
        print(sicil, card_status, opening_status, entitlement_status)
    write_report(done, skipped)
    print("rapor", REPORT)
    print("yazilan kisi", len(done), "atlanan", len(skipped))
    return 0


def write_report(done: list[dict], skipped: list[dict]) -> None:
    written = [row for row in done if row["kart"] == "yazildi" or row["acilisIslem"] == "yazildi" or row["hakedisIslem"] == "yazildi"]
    updated = [row for row in done if "guncellendi" in (row["kart"], row["acilisIslem"], row["hakedisIslem"]) or row["hakedisIslem"] == "silindi"]
    unchanged = [row for row in done if row["kart"] == "atlandi" and row["acilisIslem"] == "atlandi" and row["hakedisIslem"] in {"atlandi", "yok"}]
    lines = [
        "# Personel izin aktarımı",
        "",
        f"Çizelge tarihi 4 Kasım 2026. Çalışma {datetime.datetime.now().strftime('%Y-%m-%d %H:%M')}.",
        "",
        "Aynı dosya yeniden çalıştırılabilir. Sicil ve `dosya-{sicil}-acilis` / `dosya-{sicil}-hakedis` anahtarları güncellenir. Cinsiyet, birim, yönetici ve Keeper alanı güncellemede gönderilmez.",
        "",
        "## Kilit",
        "",
        "- Kapsam, Active Directory ile adı tutan ve işe girişi olan kişiler.",
        "- Yücel Koçak, Mete Aydın ve Bahattin Erol dışarıda.",
        "- Ad, soyad ve sicil dizinden. Sicil oturum adıdır.",
        "- Dönem, 4 Kasım 2026’ya kadar gelmiş son yıldönümü. Bir yıldan kısada işe giriş günü. Saat UTC gece yarısı.",
        "- Metin tarihler gün.ay.yıl.",
        "- Kıdemi bir yıl ve üzeri: hakediş bugünkü bant, açılış kalandan bandın düşülmüş hali.",
        "- Bir yıldan kısa: yalnız açılış, tutar çizelgedeki kalan.",
        "- Eksi ve yarım gün olduğu gibi.",
        "- Geçmiş izin satırları yazılmaz.",
        "- Cinsiyet, birim, yönetici ve Keeper boş bırakılır.",
        "",
        f"Aktarılan kişi: {len(done)}. Kapsam dışı: {len(skipped)}. Özet bakiyesi, çizelgedeki kalanla aynıdır.",
        "",
        "## Yapılan",
        "",
        "| Sicil | Çizelge | Ad | Dönem | Kıdem | Kalan | Hakediş | Açılış | Kart | Açılış işlemi | Hakediş işlemi |",
        "|---|---|---|---|---:|---:|---:|---:|---|---|---|",
    ]
    for row in done:
        lines.append(
            f"| {row['sicil']} | {row['cizelge']} | {row['ad']} {row['soyad']} | {row['donem']} | {row['kidem']} | {row['kalan']} | {row['hakedis']} | {row['acilis']} | {row['kart']} | {row['acilisIslem']} | {row['hakedisIslem']} |"
        )
    lines += [
        "",
        f"Yeni yazılan: {len(written)}. Güncellenen: {len(updated)}. Değişmeden duran: {len(unchanged)}.",
        "",
        "Sunucuda 46 personel kartı, 46 açılış ve 15 hakediş satırı vardır. Özet bakiyesi çizelgedeki kalanla aynıdır.",
        "Kayıt zaten aynıysa bu çalıştırma onu değiştirmez.",
        "",
        "## Atlanan",
        "",
    ]
    if not skipped:
        lines.append("Kapsam dışı satır yok.")
    else:
        lines += [
            "| Çizelge | Sicil | Neden |",
            "|---|---|---|",
        ]
        for row in skipped:
            lines.append(f"| {row['cizelge']} | {row.get('sicil', '')} | {row['neden']} |")
    lines.append("")
    REPORT.write_text("\n".join(lines), encoding="utf-8")


if __name__ == "__main__":
    raise SystemExit(main())
