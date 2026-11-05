"""Publish the leave form template and bind it to the approval transition.

The template is created once. A later run does not replace it, so edits made
in Document Intelligence stay. Generated files are not touched.
Re-running refreshes the library folder link and the Onayla transition.
"""

from __future__ import annotations

import base64
import io
import json
import sys
import urllib.error
import urllib.parse
import urllib.request
import zipfile

BASE_URL = "http://192.168.1.249:5040"
DOMAIN = "abt"
USERNAME = "abt_admin"
PASSWORD = "Admin123!"
TEMPLATE_CODE = "IZN_FORM"
FOLDER_NAME = "Izin formlari"
FLOW_NAME = "IZN - Ana akis"

PARAMETERS = [
    ("ad", "Ad"),
    ("soyad", "Soyad"),
    ("sicil", "Sicil"),
    ("birim", "Birim"),
    ("isKodu", "Is kaydi"),
    ("izin_turId", "Izin turu"),
    ("izin_nedenId", "Mazeret nedeni"),
    ("izin_baslangic", "Baslangic"),
    ("izin_bitis", "Bitis"),
    ("izin_yarimGun", "Yarim gun"),
    ("izin_cogul", "Cogul dogum"),
    ("izin_dusenGun", "Dusen gun"),
    ("izin_aciklama", "Aciklama"),
    ("izin_yerineBakacak", "Yerine bakacak"),
    ("generatedAt", "Uretim"),
]

ROWS = [
    ("Personel", "{{ad}} {{soyad}}"),
    ("Sicil", "{{sicil}}"),
    ("Birim", "{{birim}}"),
    ("Izin turu", "{{izin_turId}}"),
    ("Mazeret nedeni", "{{izin_nedenId}}"),
    ("Baslangic", "{{izin_baslangic}}"),
    ("Bitis", "{{izin_bitis}}"),
    ("Yarim gun", "{{izin_yarimGun}}"),
    ("Cogul dogum", "{{izin_cogul}}"),
    ("Dusen gun", "{{izin_dusenGun}}"),
    ("Aciklama", "{{izin_aciklama}}"),
    ("Yerine bakacak", "{{izin_yerineBakacak}}"),
    ("Is kaydi", "{{isKodu}}"),
]


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
        for key in ("items", "data", "results", "children"):
            value = payload.get(key)
            if isinstance(value, list):
                return value
        if payload.get("id") or payload.get("__dataId"):
            return [payload]
    return []


def text_of(value) -> str:
    if isinstance(value, str):
        return value
    if isinstance(value, dict):
        for key in ("id", "__dataId", "dataId"):
            if value.get(key):
                return str(value[key])
    return ""


def xml_escape(value: str) -> str:
    return (
        value.replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
        .replace('"', "&quot;")
    )


def paragraph(text: str, size: int, color: str, bold: bool, align: str, before: int, after: int) -> str:
    bold_xml = "<w:b/><w:bCs/>" if bold else ""
    return (
        "<w:p><w:pPr>"
        f'<w:spacing w:before="{before}" w:after="{after}"/>'
        f'<w:jc w:val="{align}"/></w:pPr><w:r><w:rPr>'
        '<w:rFonts w:ascii="Calibri" w:hAnsi="Calibri" w:cs="Calibri"/>'
        f"{bold_xml}<w:color w:val=\"{color}\"/>"
        f'<w:sz w:val="{size}"/><w:szCs w:val="{size}"/></w:rPr>'
        f'<w:t xml:space="preserve">{xml_escape(text)}</w:t></w:r></w:p>'
    )


def build_docx() -> bytes:
    rows = []
    for label, token in ROWS:
        rows.append(
            "<w:tr>"
            '<w:tc><w:tcPr><w:tcW w:w="2800" w:type="dxa"/>'
            '<w:shd w:val="clear" w:color="auto" w:fill="F3F4F6"/></w:tcPr>'
            + paragraph(label, 20, "4B5563", True, "left", 40, 40)
            + "</w:tc>"
            '<w:tc><w:tcPr><w:tcW w:w="6838" w:type="dxa"/></w:tcPr>'
            + paragraph(token, 22, "1F2937", False, "left", 40, 40)
            + "</w:tc></w:tr>"
        )
    table = (
        '<w:tbl><w:tblPr><w:tblW w:w="9638" w:type="dxa"/>'
        '<w:tblBorders>'
        '<w:top w:val="single" w:sz="4" w:space="0" w:color="D1D5DB"/>'
        '<w:left w:val="single" w:sz="4" w:space="0" w:color="D1D5DB"/>'
        '<w:bottom w:val="single" w:sz="4" w:space="0" w:color="D1D5DB"/>'
        '<w:right w:val="single" w:sz="4" w:space="0" w:color="D1D5DB"/>'
        '<w:insideH w:val="single" w:sz="4" w:space="0" w:color="D1D5DB"/>'
        '<w:insideV w:val="single" w:sz="4" w:space="0" w:color="D1D5DB"/>'
        "</w:tblBorders></w:tblPr>"
        '<w:tblGrid><w:gridCol w:w="2800"/><w:gridCol w:w="6838"/></w:tblGrid>'
        + "".join(rows)
        + "</w:tbl>"
    )
    signs = (
        "<w:tr>"
        "<w:tc>" + paragraph("Izni kullanan", 22, "1F2937", True, "left", 200, 80) + "</w:tc>"
        "<w:tc>" + paragraph("Onaylayan", 22, "1F2937", True, "left", 200, 80) + "</w:tc>"
        "</w:tr>"
    )
    sign_table = (
        '<w:tbl><w:tblPr><w:tblW w:w="9638" w:type="dxa"/></w:tblPr>'
        '<w:tblGrid><w:gridCol w:w="4819"/><w:gridCol w:w="4819"/></w:tblGrid>'
        + signs
        + "</w:tbl>"
    )
    body = (
        paragraph("Izin Formu", 40, "1F2937", True, "center", 0, 80)
        + paragraph("Bu form, onaylanan izin talebinin kaydidir.", 22, "4B5563", False, "center", 0, 280)
        + table
        + sign_table
        + paragraph("Uretim: {{generatedAt}}", 16, "6B7280", False, "right", 360, 0)
    )
    document = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>'
        + body
        + '<w:sectPr><w:pgSz w:w="11906" w:h="16838"/>'
        '<w:pgMar w:top="1134" w:right="1134" w:bottom="1134" w:left="1134" w:header="708" w:footer="708" w:gutter="0"/>'
        "</w:sectPr></w:body></w:document>"
    )
    content_types = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
        '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>'
        '<Default Extension="xml" ContentType="application/xml"/>'
        '<Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>'
        '<Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>'
        "</Types>"
    )
    rels = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
        '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>'
        "</Relationships>"
    )
    doc_rels = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
        '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>'
        "</Relationships>"
    )
    styles = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">'
        "<w:docDefaults><w:rPrDefault><w:rPr>"
        '<w:rFonts w:ascii="Calibri" w:hAnsi="Calibri" w:cs="Calibri"/>'
        '<w:sz w:val="22"/><w:szCs w:val="22"/>'
        "</w:rPr></w:rPrDefault></w:docDefaults></w:styles>"
    )
    blob = io.BytesIO()
    with zipfile.ZipFile(blob, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        archive.writestr("[Content_Types].xml", content_types)
        archive.writestr("_rels/.rels", rels)
        archive.writestr("word/document.xml", document)
        archive.writestr("word/_rels/document.xml.rels", doc_rels)
        archive.writestr("word/styles.xml", styles)
    return blob.getvalue()


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
    _, tree = request("GET", "/documents/api/v1/template-categories/tree", access)
    for node in items(tree) or (tree if isinstance(tree, list) else []):
        if isinstance(node, dict) and node.get("name") == "Izin":
            print(f"sablon kategorisi var: {node.get('id')}")
            return str(node["id"])
    _, created = request(
        "POST",
        "/documents/api/v1/template-categories",
        access,
        {"name": "Izin", "parentId": None},
    )
    category_id = text_of(created) or (created.get("id") if isinstance(created, dict) else "")
    if not category_id:
        raise RuntimeError("Izin sablon kategorisi olusmadi")
    print(f"sablon kategorisi olustu: {category_id}")
    return category_id


def find_template(access: str) -> str | None:
    _, listed = request("GET", "/documents/api/v1/templates", access)
    for row in items(listed):
        if row.get("code") == TEMPLATE_CODE:
            return str(row.get("id"))
    return None


def ensure_template(access: str, category_id: str) -> str:
    existing = find_template(access)
    if existing:
        print(f"sablon duruyor: {TEMPLATE_CODE} {existing}")
        return existing
    content = build_docx()
    _, created = request(
        "POST",
        "/documents/api/v1/templates/from-reference",
        access,
        {
            "categoryId": category_id,
            "name": "Izin Formu",
            "description": "Onaylanan izin talebinin formu. Govde Belge Zekasinda duzenlenir.",
            "content": base64.b64encode(content).decode("ascii"),
            "fileName": "IZN-FORM-template.docx",
            "size": len(content),
        },
    )
    template_id = created.get("id") if isinstance(created, dict) else None
    if not template_id:
        raise RuntimeError("Izin formu sablonu olusmadi")
    request(
        "PUT",
        f"/documents/api/v1/templates/{template_id}/metadata",
        access,
        {"name": "Izin Formu", "code": TEMPLATE_CODE},
    )
    request(
        "PUT",
        f"/documents/api/v1/templates/{template_id}/parameters",
        access,
        {
            "parameters": [
                {"key": key, "label": label, "dataType": "text", "valueSourceMode": "manual"}
                for key, label in PARAMETERS
            ]
        },
    )
    try:
        request("POST", f"/documents/api/v1/templates/{template_id}/publish", access, {})
    except RuntimeError as ex:
        print(f"yayin uyarisi: {ex}")
    print(f"sablon olustu: {TEMPLATE_CODE} {template_id}")
    return str(template_id)


def ensure_folder(access: str) -> str:
    _, listed = request("GET", "/documents/api/v1/resources/children?limit=200", access)
    for row in items(listed):
        kind = str(row.get("kind") or row.get("resourceKind") or "").lower()
        if row.get("name") == FOLDER_NAME and (not kind or "folder" in kind):
            print(f"klasor var: {row.get('id')}")
            return str(row["id"])
    _, created = request(
        "POST",
        "/documents/api/v1/resources/folder",
        access,
        {"name": FOLDER_NAME, "description": "Onaylanan izin formlari"},
    )
    folder_id = created.get("id") if isinstance(created, dict) else None
    if not folder_id:
        raise RuntimeError("Izin formlari klasoru olusmadi")
    print(f"klasor olustu: {folder_id}")
    return str(folder_id)


def bind_transition(access: str, folder_id: str) -> None:
    filt = urllib.parse.quote(f"name:eq:{FLOW_NAME}")
    _, found = request("GET", f"/data/api/v1/data/op_state_flows?limit=1&expand=false&filter={filt}", access)
    rows = items(found)
    if not rows:
        raise RuntimeError("IZN akisi bulunamadi")
    flow = rows[0]
    flow_id = flow.get("__dataId") or flow.get("dataId")
    action = {
        "templateCode": TEMPLATE_CODE,
        "itemsField": "izin_personelId",
        "sourceDataset": "izin_personel",
        "reportId": "",
        "outputFolderId": folder_id,
        "parentRefField": "isKodu",
        "labelField": "sicil",
        "attachToWorkItem": True,
        "displayMaps": {
            "izin_yarimGun": {
                "ogleden_once": "Ogleden once",
                "ogleden_sonra": "Ogleden sonra",
            },
            "izin_cogul": {"true": "Evet", "false": "Hayir"},
        },
    }
    transitions = []
    for step in flow.get("transitions") or []:
        copied = dict(step)
        if copied.get("transitionKey") == "onayla":
            copied["documentAction"] = action
        transitions.append(copied)
    body = {
        "name": flow.get("name"),
        "workspaceId": text_of(flow.get("workspaceId")) or flow.get("workspaceId"),
        "initialStateId": text_of(flow.get("initialStateId")) or flow.get("initialStateId"),
        "isDefault": flow.get("isDefault", True),
        "isActive": flow.get("isActive", True),
        "transitions": transitions,
    }
    request("PUT", f"/data/api/v1/data/op_state_flows/{flow_id}", access, body)
    print(f"onay gecisi forma baglandi: {flow_id}")


def main() -> int:
    access = token()
    print("token alindi")
    category_id = ensure_category(access)
    ensure_template(access, category_id)
    folder_id = ensure_folder(access)
    bind_transition(access, folder_id)
    print("izin formu hazir")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as ex:
        print(ex, file=sys.stderr)
        sys.exit(1)
