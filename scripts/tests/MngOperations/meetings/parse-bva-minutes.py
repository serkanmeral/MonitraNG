# Parse BVA_KP meeting minutes (.docx) into JSON.
# Skips Word lock files (~$...). Does not call the API.

import json
import re
import sys
import zipfile
import xml.etree.ElementTree as ET
from datetime import datetime, timedelta, timezone
from pathlib import Path

W = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"
TZ = timezone(timedelta(hours=3))


def tr_key(value: str) -> str:
    text = " ".join((value or "").split())
    chars = []
    for ch in text:
        if ch == "İ":
            chars.append("i")
        elif ch == "I":
            chars.append("ı")
        else:
            chars.append(ch.lower())
    return "".join(chars)


def para_text(paragraph) -> str:
    return "".join((node.text or "") for node in paragraph.iter(W + "t")).strip()


def cell_lines(cell) -> str:
    lines = []
    for paragraph in cell.findall(W + "p"):
        text = para_text(paragraph)
        if text:
            lines.append(text)
    return " ".join(lines).strip()


def table_rows(table) -> list:
    rows = []
    for row in table.findall(W + "tr"):
        cells = [cell_lines(cell) for cell in row.findall(W + "tc")]
        if any(cells):
            rows.append(cells)
    return rows


def walk(element, blocks: list) -> None:
    for child in list(element):
        if child.tag == W + "p":
            text = para_text(child)
            if text:
                blocks.append(("p", text))
        elif child.tag == W + "tbl":
            rows = table_rows(child)
            if rows:
                blocks.append(("tbl", rows))
        elif child.tag == W + "sdt":
            walk(child, blocks)


def looks_like_people(rows) -> bool:
    if not rows or len(rows[0]) < 3:
        return False
    head = [tr_key(cell) for cell in rows[0][:4]]
    return any(tr_key("ADI SOYADI") in cell or tr_key("AD SOYAD") in cell for cell in head)


def looks_like_actions(rows) -> bool:
    if not rows or len(rows[0]) < 3:
        return False
    head = [tr_key(cell) for cell in rows[0][:4]]
    has_no = any(cell in {"no", "sira", tr_key("SIRA")} for cell in head)
    has_action = any(tr_key("İŞLEM") in cell for cell in head)
    return has_no and has_action


def pair_map(cells) -> dict:
    found = {}
    index = 0
    while index < len(cells) - 1:
        label = tr_key(cells[index]).rstrip(":")
        if label in {
            tr_key("TOPLANTI ADI / KONUSU"),
            tr_key("TOPLANTI ADI"),
            tr_key("KONU"),
            tr_key("TARİH"),
            tr_key("SAAT"),
            tr_key("YER"),
        }:
            found[label] = cells[index + 1].strip()
            index += 2
            continue
        index += 1
    return found


def parse_date(value: str):
    text = (value or "").strip()
    match = re.search(r"(\d{1,2})[./](\d{1,2})[./](\d{4})", text)
    if not match:
        return None
    day, month, year = int(match.group(1)), int(match.group(2)), int(match.group(3))
    try:
        return datetime(year, month, day).date()
    except ValueError:
        return None


def parse_time(value: str):
    text = (value or "").strip()
    match = re.search(r"(\d{1,2})[:.](\d{2})", text)
    if not match:
        return None
    hour, minute = int(match.group(1)), int(match.group(2))
    if hour > 23 or minute > 59:
        return None
    return hour, minute


def split_firms(value: str) -> list:
    firms = []
    seen = set()
    for part in (value or "").split("/"):
        name = " ".join(part.split())
        key = tr_key(name)
        if not name or key in seen:
            continue
        seen.add(key)
        firms.append(name)
    return firms


def excerpt(text: str, limit: int = 400) -> str:
    value = " ".join(text.split())
    if len(value) <= limit:
        return value
    return value[: limit - 3].rstrip() + "..."


def parse_docx(path: Path) -> dict:
    warnings = []
    with zipfile.ZipFile(path) as package:
        root = ET.fromstring(package.read("word/document.xml"))
    body = root.find(W + "body")
    blocks = []
    walk(body, blocks)

    name = ""
    date_text = ""
    time_text = ""
    location = ""
    participants = []
    topics = []
    actions = []
    body_seen = False

    for kind, payload in blocks:
        if kind != "tbl":
            continue
        rows = payload
        if looks_like_people(rows):
            for row in rows[1:]:
                if len(row) < 2:
                    continue
                person = " ".join((row[1] if len(row) > 1 else "").split())
                org = " ".join((row[2] if len(row) > 2 else "").split())
                title = " ".join((row[3] if len(row) > 3 else "").split())
                if person and not person.isdigit():
                    participants.append({"name": person, "organization": org, "title": title})
            continue
        if looks_like_actions(rows):
            continue
        mapped = {}
        for row in rows:
            mapped.update(pair_map(row))
        name_key = tr_key("TOPLANTI ADI / KONUSU")
        short_key = tr_key("TOPLANTI ADI")
        konu_key = tr_key("KONU")
        if name_key in mapped or short_key in mapped or konu_key in mapped:
            name = mapped.get(name_key) or mapped.get(short_key) or mapped.get(konu_key) or name
        if tr_key("TARİH") in mapped:
            date_text = mapped[tr_key("TARİH")]
        if tr_key("SAAT") in mapped:
            time_text = mapped[tr_key("SAAT")]
        if tr_key("YER") in mapped:
            location = mapped[tr_key("YER")]
        blob = tr_key(" ".join(" ".join(row) for row in rows))
        if tr_key("GÖRÜŞÜLEN KONULAR") in blob or tr_key("İŞLEM MADDELERİ") in blob:
            body_seen = True

    if not body_seen:
        # Some files keep the narrative as loose paragraphs.
        capture = False
        for kind, payload in blocks:
            if kind != "p":
                continue
            key = tr_key(payload).rstrip(":")
            if key.startswith(tr_key("GÖRÜŞÜLEN KONULAR")):
                capture = True
                continue
            if key.startswith(tr_key("İŞLEM MADDELERİ")) or key.startswith(tr_key("HAZIRLAYAN")):
                capture = False
                continue
            if capture:
                topics.append(payload)
    else:
        for kind, payload in blocks:
            if kind != "tbl":
                continue
            blob = tr_key(" ".join(" ".join(row) for row in payload))
            if tr_key("GÖRÜŞÜLEN KONULAR") not in blob and tr_key("İŞLEM MADDELERİ") not in blob:
                continue
            # Re-walk the matching table's cells in document order.
            for table in body.iter(W + "tbl"):
                rows = table_rows(table)
                blob = tr_key(" ".join(" ".join(row) for row in rows))
                if tr_key("GÖRÜŞÜLEN KONULAR") not in blob and tr_key("İŞLEM MADDELERİ") not in blob:
                    continue
                if looks_like_actions(rows) or looks_like_people(rows):
                    continue
                for cell in table.iter(W + "tc"):
                    cell_blob = tr_key(" ".join(para_text(p) for p in cell.findall(W + "p")))
                    if tr_key("GÖRÜŞÜLEN KONULAR") not in cell_blob and tr_key("İŞLEM MADDELERİ") not in cell_blob:
                        continue
                    mode = None
                    for child in list(cell):
                        if child.tag == W + "p":
                            text = para_text(child)
                            key = tr_key(text).rstrip(":")
                            if key.startswith(tr_key("GÖRÜŞÜLEN KONULAR")):
                                mode = "topics"
                                continue
                            if key.startswith(tr_key("İŞLEM MADDELERİ")):
                                mode = "actions"
                                continue
                            if key.startswith(tr_key("HAZIRLAYAN")):
                                mode = None
                                continue
                            if mode == "topics" and text:
                                topics.append(text)
                        elif child.tag == W + "tbl" and mode == "actions":
                            action_rows = table_rows(child)
                            if not looks_like_actions(action_rows):
                                continue
                            for row in action_rows[1:]:
                                code = " ".join((row[0] if row else "").split())
                                title = " ".join((row[1] if len(row) > 1 else "").split())
                                firms = split_firms(row[2] if len(row) > 2 else "")
                                due = parse_date(row[3] if len(row) > 3 else "")
                                if not title:
                                    warnings.append(f"bos islem satiri: {code or '?'}")
                                    continue
                                actions.append({
                                    "code": code[:32],
                                    "title": title,
                                    "firms": firms,
                                    "due": f"d:{due.isoformat()}" if due else None,
                                })
                    break
                break

    meeting_date = parse_date(date_text)
    clock = parse_time(time_text)
    if not name:
        warnings.append("toplanti adi yok")
    if not meeting_date:
        warnings.append(f"tarih okunamadi: {date_text or '-'}")
    if not clock:
        warnings.append(f"saat okunamadi: {time_text or '-'}")
        clock = (9, 0)
    start = None
    end = None
    if meeting_date and clock:
        start_dt = datetime(meeting_date.year, meeting_date.month, meeting_date.day, clock[0], clock[1], tzinfo=TZ)
        start = start_dt.isoformat()
        end = (start_dt + timedelta(hours=1)).isoformat()

    topic_text = "\n\n".join(topics).strip()
    if not topic_text:
        warnings.append("gorusulen konular bos")

    if len(name) > 256:
        warnings.append("toplanti adi kisaltildi")
        name = name[:256]
    if len(location) > 256:
        warnings.append("yer kisaltildi")
        location = location[:256]

    return {
        "file": path.name,
        "name": name,
        "start": f"t:{start}" if start else None,
        "end": f"t:{end}" if end else None,
        "location": location,
        "participants": participants,
        "topics": topic_text,
        "agenda": excerpt(topic_text) if topic_text else "",
        "actions": actions,
        "warnings": warnings,
    }


def main() -> None:
    source = Path(sys.argv[1])
    output = Path(sys.argv[2])
    meetings = []
    skipped = []
    for path in sorted(source.glob("*.docx")):
        if path.name.startswith("~$"):
            skipped.append(path.name)
            continue
        try:
            meetings.append(parse_docx(path))
        except Exception as error:  # noqa: BLE001 — one bad file must not stop the batch
            meetings.append({
                "file": path.name,
                "name": "",
                "start": None,
                "end": None,
                "location": "",
                "participants": [],
                "topics": "",
                "agenda": "",
                "actions": [],
                "warnings": [f"okunamadi: {error}"],
            })
    output.write_text(
        json.dumps({"skippedFiles": skipped, "meetings": meetings}, ensure_ascii=False),
        encoding="utf-8",
    )
    print(f"parsed={len(meetings)} skippedFiles={len(skipped)}")


if __name__ == "__main__":
    main()
