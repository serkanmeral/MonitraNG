"""Create the leave workspace and the short approval chain on the abt domain.

States: Taslak, Yonetici onayi, Onaylandi, Reddedildi, Iptal.
Transitions: onaya gonder, onayla, reddet, iptal from the waiting state.
Cancel from Taslak writes no ledger row. There is no IK step.
Re-running updates the same records by name.
"""

from __future__ import annotations

import json
import sys
import urllib.error
import urllib.parse
import urllib.request

BASE_URL = "http://192.168.1.249:5040"
DOMAIN = "abt"
USERNAME = "abt_admin"
PASSWORD = "Admin123!"


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
    if not isinstance(row, dict):
        return None
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


def ensure(access: str, collection: str, name: str, body: dict) -> str:
    filt = urllib.parse.quote(f"name:eq:{name}")
    _, found = request("GET", f"/data/api/v1/data/{collection}?limit=1&filter={filt}", access)
    existing = items(found)
    if existing:
        row_id = data_id(existing[0])
        request("PUT", f"/data/api/v1/data/{collection}/{row_id}", access, body)
        print(f"guncellendi: {collection} {name}")
        return row_id
    _, created = request("POST", f"/data/api/v1/data/{collection}", access, body)
    row_id = data_id(created if isinstance(created, dict) else {})
    if not row_id:
        rows = items(created)
        row_id = data_id(rows[0]) if rows else None
    if not row_id:
        _, found = request("GET", f"/data/api/v1/data/{collection}?limit=1&filter={filt}", access)
        rows = items(found)
        row_id = data_id(rows[0]) if rows else None
    if not row_id:
        raise RuntimeError(f"{collection} {name} olusmadi")
    print(f"olustu: {collection} {name} {row_id}")
    return row_id


def relation(dataset: str, label: str, search: list[str], **extra) -> dict:
    lookup = {
        "source": "dataset",
        "presentation": "autocomplete",
        "valueField": "__dataId",
        "labelField": label,
        "searchFields": search,
        "pageSize": 50,
    }
    lookup.update(extra)
    return {"lookup": lookup}


def static_select(pairs: list[tuple[str, str]]) -> dict:
    return {
        "lookup": {
            "source": "static",
            "presentation": "dropdown",
            "staticItems": [{"value": value, "label": label} for value, label in pairs],
        }
    }


DOC_API = "/documents/api/v1/resources"
PERSONEL_FOLDER = "7fb89d06-69e7-4c90-9e40-d0be954fa2b2"


def ensure_belge_folder(access: str) -> str:
    _, current = request("GET", f"{DOC_API}/{PERSONEL_FOLDER}", access)
    parent = current.get("parentId") if isinstance(current, dict) else None
    path = f"{DOC_API}/children"
    if parent:
        path += "?parentId=" + urllib.parse.quote(parent)
    _, payload = request("GET", path, access)
    for row in items(payload):
        if row.get("type") == "folder" and row.get("name") == "İzin belgeleri":
            print(f"klasor duruyor: Izin belgeleri {row.get('id')}")
            return row["id"]
    body = {"name": "İzin belgeleri"}
    if parent:
        body["parentId"] = parent
    _, created = request("POST", f"{DOC_API}/folder", access, body)
    folder_id = created.get("id") if isinstance(created, dict) else None
    if not folder_id:
        raise RuntimeError(f"belge klasoru olusmadi: {created}")
    print(f"klasor olustu: Izin belgeleri {folder_id}")
    return folder_id


def field(access: str, key: str, label: str, field_type: str, **extra) -> str:
    body = {
        "key": key,
        "label": label,
        "fieldType": field_type,
        "scope": "pool",
        "category": extra.pop("category", "leave"),
    }
    body.update(extra)
    filt = urllib.parse.quote(f"key:eq:{key}")
    _, found = request("GET", f"/data/api/v1/data/op_fields?limit=1&filter={filt}", access)
    existing = items(found)
    if existing:
        row_id = data_id(existing[0])
        request("PUT", f"/data/api/v1/data/op_fields/{row_id}", access, body)
        print(f"alan guncellendi: {key}")
        return row_id
    _, created = request("POST", "/data/api/v1/data/op_fields", access, body)
    row_id = data_id(created if isinstance(created, dict) else {})
    if not row_id:
        rows = items(created)
        row_id = data_id(rows[0]) if rows else None
    if not row_id:
        _, found = request("GET", f"/data/api/v1/data/op_fields?limit=1&filter={filt}", access)
        rows = items(found)
        row_id = data_id(rows[0]) if rows else None
    if not row_id:
        raise RuntimeError(f"alan olusmadi: {key}")
    print(f"alan olustu: {key} {row_id}")
    return row_id


def transition(key, source, target, label, order, required=None, groups=None, document_action=None):
    body = {
        "transitionKey": key,
        "fromStateId": source,
        "toStateId": target,
        "label": label,
        "order": order,
    }
    if required:
        body["requiredFields"] = required
    if groups:
        body["permissions"] = {"groups": groups}
    if document_action:
        body["documentAction"] = document_action
    return body


def existing_document_actions(access: str, flow_name: str) -> dict:
    filt = urllib.parse.quote(f"name:eq:{flow_name}")
    _, found = request("GET", f"/data/api/v1/data/op_state_flows?limit=1&filter={filt}", access)
    rows = items(found)
    if not rows:
        return {}
    kept = {}
    for step in rows[0].get("transitions") or []:
        action = step.get("documentAction")
        key = step.get("transitionKey")
        if key and action:
            kept[key] = action
    return kept


def query_step(dataset, query, parameters, mappings):
    return {
        "dataset": dataset,
        "query": query,
        "parameters": [{"name": name, "from": source} for name, source in parameters],
        "mappings": [{"from": source, "to": target, "onEmpty": empty} for source, target, empty in mappings],
    }


def ledger_mappings(hareket: str, gun_field: str, gecis: str, anahtar_field: str):
    return [
        {"target": "personelId", "source": "field", "path": "fields.izin_personelKartId"},
        {"target": "turId", "source": "field", "path": "fields.izin_turId"},
        {"target": "nedenId", "source": "field", "path": "fields.izin_nedenId"},
        {"target": "hareket", "source": "static", "value": hareket},
        {"target": "gun", "source": "field", "path": f"fields.{gun_field}"},
        {"target": "donemBaslangic", "source": "field", "path": "fields.izin_donemBaslangic"},
        {"target": "baslangic", "source": "field", "path": "fields.izin_baslangic"},
        {"target": "bitis", "source": "field", "path": "fields.izin_bitis"},
        {"target": "yarimGun", "source": "field", "path": "fields.izin_yarimGun"},
        {
            "target": "bakiyeyeEtki",
            "source": "lookup",
            "dataset": "izin_turleri",
            "from": "fields.izin_turId",
            "field": "bakiyedenDuser",
        },
        {"target": "kaynak", "source": "static", "value": "talep"},
        {"target": "isKodu", "source": "field", "path": "key"},
        {"target": "gecis", "source": "static", "value": gecis},
        {"target": "izKodu", "source": "field", "path": f"fields.{anahtar_field}"},
    ]


def ledger_rule(name, type_id, transition_key, hareket, gun_field, anahtar_field):
    return {
        "name": name,
        "workspaceId": None,
        "ruleType": "automation",
        "trigger": "WorkItemTransitioned",
        "isActive": True,
        "priority": 100,
        "typeId": type_id,
        "transitionKey": transition_key,
        "actions": [
            {
                "type": "createDatasetRows",
                "dataset": "izin_hareketleri",
                "onError": "failTransition",
                "idempotency": {
                    "mode": "one_per_source",
                    "lookupField": "izKodu",
                    "lookupFrom": f"fields.{anahtar_field}",
                },
                "fieldMappings": ledger_mappings(hareket, gun_field, transition_key, anahtar_field),
            }
        ],
        "querySteps": [],
    }


def main() -> int:
    access = token()
    print("token alindi")

    priority_id = ensure(
        access,
        "op_priorities",
        "IZN - Normal",
        {"name": "IZN - Normal", "color": "primary", "sortOrder": 10, "isDefault": True},
    )

    personel = field(
        access, "izin_personelId", "Personel", "persons",
        cardinality="single",
        relationDatasetName="",
        options={
            "cardResolve": {
                "dataset": "izin_ayarlar",
                "query": "kart_bul",
                "parameter": "keeperId",
                "copies": [{"from": "cinsiyet", "to": "izin_cinsiyet"}],
            }
        },
    )
    cinsiyet = field(access, "izin_cinsiyet", "Cinsiyet", "text")
    tur = field(
        access, "izin_turId", "Izin turu", "relation",
        relationDatasetName="izin_turleri",
        options=relation(
            "izin_turleri", "ad", ["ad", "kod"],
            presentation="dropdown",
            filter="durum:eq:acik",
            dependsOn={"fieldKey": "izin_cinsiyet", "filterTemplate": "kimAcar:in:herkes,{{parentValue|herkes}}"},
        ),
    )
    neden = field(
        access, "izin_nedenId", "Mazeret nedeni", "relation",
        relationDatasetName="izin_mazeret_nedenleri",
        options=relation("izin_mazeret_nedenleri", "ad", ["ad", "kod"]),
    )
    baslangic = field(access, "izin_baslangic", "Baslangic", "datetime")
    bitis = field(access, "izin_bitis", "Bitis", "datetime")
    yarim = field(
        access, "izin_yarimGun", "Yarim gun", "select",
        options=static_select([("ogleden_once", "Ogleden once"), ("ogleden_sonra", "Ogleden sonra")]),
    )
    cogul = field(access, "izin_cogul", "Cogul dogum", "bool")
    dusen = field(
        access, "izin_dusenGun", "Dusen gun", "number",
        options={
            "compute": {
                "dataset": "izin_ayarlar",
                "query": "dusen_gun",
                "parameters": [
                    {"name": "baslangic", "from": "izin_baslangic", "required": True},
                    {"name": "bitis", "from": "izin_bitis", "required": True},
                    {"name": "yarimGun", "from": "izin_yarimGun"},
                    {"name": "turId", "from": "izin_turId", "required": True},
                    {"name": "cogul", "from": "izin_cogul"},
                ],
                "copies": [
                    {"from": "gun", "to": "izin_dusenGun"},
                    {"from": "eksi", "to": "izin_dusenEksi"},
                ],
            }
        },
    )
    eksi = field(access, "izin_dusenEksi", "Dusen gun eksi", "number")
    aciklama = field(access, "izin_aciklama", "Aciklama", "text")
    belge_folder = ensure_belge_folder(access)
    belge = field(
        access, "izin_belgeId", "Belge", "file",
        relationDatasetName="",
        options={"folderId": belge_folder, "maxSizeBytes": 10 * 1024 * 1024},
    )
    yerine = field(
        access, "izin_yerineBakacak", "Yerine bakacak", "persons",
        cardinality="single",
        relationDatasetName="",
        options={},
    )
    personel_kart = field(access, "izin_personelKartId", "Personel karti", "text")
    yerine_kart = field(access, "izin_yerineKartId", "Yerine bakacak karti", "text")
    engel = field(access, "izin_engel", "Engel", "text")
    hak = field(access, "izin_hakVar", "Talep hakki", "bool")
    donem = field(access, "izin_donemBaslangic", "Donem baslangici", "datetime")
    cakisma = field(access, "izin_cakismaAdet", "Cakisma adedi", "number")
    tavan = field(access, "izin_tavanAsildi", "Tavan asildi", "bool")
    donem_asildi = field(access, "izin_donemAsildi", "Donem limiti asildi", "bool")
    bloke_key = field(access, "izin_blokeAnahtar", "Bloke anahtari", "text")
    iade_key = field(access, "izin_iadeAnahtar", "Iade anahtari", "text")
    kullanim_key = field(access, "izin_kullanimAnahtar", "Kullanim anahtari", "text")
    field_ids = [
        personel, cinsiyet, tur, neden, baslangic, bitis, yarim, cogul, dusen, eksi, aciklama, belge, yerine,
        personel_kart, yerine_kart,
        engel, hak, donem, cakisma, tavan, donem_asildi, bloke_key, iade_key, kullanim_key,
    ]

    type_id = ensure(
        access,
        "op_work_item_types",
        "Izin talebi",
        {"name": "Izin talebi", "category": "operational", "color": "info", "icon": "CalendarIcon", "sortOrder": 80},
    )

    taslak = ensure(access, "op_states", "IZN - Taslak", {
        "name": "IZN - Taslak", "category": "open", "isInitial": True, "isStart": True, "color": "secondary", "sortOrder": 10,
    })
    onay = ensure(access, "op_states", "IZN - Yonetici onayi", {
        "name": "IZN - Yonetici onayi", "category": "on_hold", "color": "warning", "sortOrder": 20,
    })
    onaylandi = ensure(access, "op_states", "IZN - Onaylandi", {
        "name": "IZN - Onaylandi", "category": "closed", "isClosed": True, "isTerminal": True, "color": "success", "sortOrder": 30,
    })
    reddedildi = ensure(access, "op_states", "IZN - Reddedildi", {
        "name": "IZN - Reddedildi", "category": "closed", "isClosed": True, "isTerminal": True, "color": "error", "sortOrder": 40,
    })
    iptal = ensure(access, "op_states", "IZN - Iptal", {
        "name": "IZN - Iptal", "category": "closed", "isClosed": True, "isTerminal": True, "color": "secondary", "sortOrder": 50,
    })
    state_ids = [taslak, onay, onaylandi, reddedildi, iptal]

    workspace = {
        "name": "Personel izin",
        "workspaceType": "operational",
        "description": "Personel izin talebi. Ilk zincir yonetici onayi, red ve iptaldir.",
        "workItemKeyPrefix": "IZN",
        "workItemKeyFormat": "{prefix}-{seq:D4}",
        "workItemSequenceStart": 1,
        "enabledTypeIds": [type_id],
        "enabledStateIds": state_ids,
        "enabledPriorityIds": [priority_id],
        "enabledFieldIds": field_ids,
    }
    workspace_id = ensure(access, "op_workspaces", "Personel izin", workspace)

    kept_documents = existing_document_actions(access, "IZN - Ana akis")
    flow = {
        "name": "IZN - Ana akis",
        "workspaceId": workspace_id,
        "initialStateId": taslak,
        "isDefault": True,
        "isActive": True,
        "transitions": [
            transition("gonder", taslak, onay, "Onaya gonder", 0, ["izin_personelId", "izin_turId", "izin_baslangic", "izin_bitis"], document_action=kept_documents.get("gonder")),
            transition("onayla", onay, onaylandi, "Onayla", 1, groups=["Yonetim"], document_action=kept_documents.get("onayla")),
            transition("reddet", onay, reddedildi, "Reddet", 2, groups=["Yonetim"], document_action=kept_documents.get("reddet")),
            transition("iptal", onay, iptal, "Iptal", 3, document_action=kept_documents.get("iptal")),
            transition("iptal_taslak", taslak, iptal, "Iptal", 4, document_action=kept_documents.get("iptal_taslak")),
        ],
    }
    flow_id = ensure(access, "op_state_flows", "IZN - Ana akis", flow)

    visible = [
        "title", "description", "typeId", "priorityId",
        "izin_personelId", "izin_turId",
        "izin_baslangic", "izin_bitis", "izin_yarimGun", "izin_cogul",
        "izin_dusenGun", "izin_aciklama", "izin_belgeId", "izin_yerineBakacak",
    ]
    behaviors = {key: {"visible": True} for key in visible}
    behaviors["title"]["required"] = True
    behaviors["izin_personelId"]["required"] = True
    behaviors["izin_turId"]["required"] = True
    behaviors["izin_baslangic"]["required"] = True
    behaviors["izin_bitis"]["required"] = True
    behaviors["izin_dusenGun"]["readonly"] = True
    form = {
        "name": "IZN - Talep formu",
        "workspaceId": workspace_id,
        "defaultTypeId": type_id,
        "defaultStateFlowId": flow_id,
        "defaultStateId": taslak,
        "isDefault": True,
        "layout": {"sections": [{"key": "main", "title": "Izin talebi", "fields": visible}]},
        "fieldBehaviors": behaviors,
    }
    form_id = ensure(access, "op_forms", "IZN - Talep formu", form)

    board = {
        "name": "IZN - Onay kuyrugu",
        "workspaceId": workspace_id,
        "viewType": "list",
        "defaultStateFlowId": flow_id,
        "defaultFormId": form_id,
        "defaultTypeId": type_id,
        "defaultPriorityId": priority_id,
        "defaultStateId": taslak,
        "visibleFields": ["key", "title", "stateId", "priorityId", "assignee", "izin_personelId", "izin_dusenGun"],
        "config": {
            "columns": [
                {"stateId": taslak, "title": "Taslak", "queryKey": "wi_board_column"},
                {"stateId": onay, "title": "Yonetici onayi", "queryKey": "wi_board_column"},
                {"stateId": onaylandi, "title": "Onaylandi", "queryKey": "wi_board_column"},
                {"stateId": reddedildi, "title": "Reddedildi", "queryKey": "wi_board_column"},
                {"stateId": iptal, "title": "Iptal", "queryKey": "wi_board_column"},
            ]
        },
    }
    ensure(access, "op_boards", "IZN - Onay kuyrugu", board)

    profile = {
        "name": "IZN - Talep profili",
        "workspaceId": workspace_id,
        "defaultTypeId": type_id,
        "isDefault": True,
        "layout": {"sections": [{"key": "main", "title": "Izin talebi", "fields": visible}]},
        "fieldBehaviors": behaviors,
        "actions": [
            {"transitionKey": "gonder", "order": 0, "label": "Onaya gonder"},
            {"transitionKey": "onayla", "order": 1, "label": "Onayla"},
            {"transitionKey": "reddet", "order": 2, "label": "Reddet"},
            {"transitionKey": "iptal", "order": 3, "label": "Iptal"},
            {"transitionKey": "iptal_taslak", "order": 4, "label": "Iptal"},
        ],
    }
    ensure(access, "op_profiles", "IZN - Talep profili", profile)

    hesap = {
        "name": "IZN - Hesap",
        "workspaceId": workspace_id,
        "ruleType": "automation",
        "trigger": "WorkItemTransition",
        "isActive": True,
        "priority": 100,
        "typeId": type_id,
        "transitionKey": "gonder",
        "description": "Once Keeper kisisi sicille karta baglanir. Sonra talep kurali, dusen gun, donem, yazim anahtari, cakisma, tavan ve donem limiti.",
        "actions": [],
        "querySteps": [
            query_step(
                "izin_ayarlar", "kart_bul",
                [("keeperId", "izin_personelId"), ("yerineKeeperId", "izin_yerineBakacak")],
                [
                    ("personelKartId", "izin_personelKartId", "fail"),
                    ("cinsiyet", "izin_cinsiyet", "skip"),
                    ("yerineKartId", "izin_yerineKartId", "skip"),
                ],
            ),
            query_step(
                "izin_turleri", "talep_uygun",
                [
                    ("turId", "izin_turId"),
                    ("personelId", "izin_personelKartId"),
                    ("nedenId", "izin_nedenId"),
                    ("belgeId", "izin_belgeId"),
                    ("yarimGun", "izin_yarimGun"),
                    ("yerineId", "izin_yerineKartId"),
                    ("baslangic", "izin_baslangic"),
                    ("cogul", "izin_cogul"),
                ],
                [("engel", "izin_engel", "fail"), ("bitis", "izin_bitis", "skip")],
            ),
            query_step(
                "izin_personel", "talep_hakki",
                [("personelId", "izin_personelKartId"), ("actorId", "actor.personId"), ("actorGroups", "actor.groups")],
                [("hakVar", "izin_hakVar", "fail")],
            ),
            query_step(
                "izin_ayarlar", "dusen_gun",
                [("baslangic", "izin_baslangic"), ("bitis", "izin_bitis"), ("yarimGun", "izin_yarimGun"), ("turId", "izin_turId"), ("cogul", "izin_cogul")],
                [("gun", "izin_dusenGun", "fail"), ("eksi", "izin_dusenEksi", "fail")],
            ),
            query_step(
                "izin_personel", "acik_donem",
                [("personelId", "izin_personelKartId"), ("tarih", "izin_baslangic")],
                [("donemBaslangic", "izin_donemBaslangic", "fail")],
            ),
            query_step(
                "izin_ayarlar", "is_anahtar",
                [("isKodu", "key")],
                [("blokeAnahtar", "izin_blokeAnahtar", "fail"), ("iadeAnahtar", "izin_iadeAnahtar", "fail"), ("kullanimAnahtar", "izin_kullanimAnahtar", "fail")],
            ),
            query_step(
                "izin_hareketleri", "cakisma",
                [("personelId", "izin_personelKartId"), ("baslangic", "izin_baslangic"), ("bitis", "izin_bitis"), ("yarimGun", "izin_yarimGun"), ("isKodu", "key")],
                [("adet", "izin_cakismaAdet", "zero")],
            ),
            query_step(
                "izin_turleri", "tavan",
                [("turId", "izin_turId"), ("nedenId", "izin_nedenId"), ("gun", "izin_dusenGun")],
                [("asildi", "izin_tavanAsildi", "false")],
            ),
            query_step(
                "izin_turleri", "donem_limit",
                [
                    ("turId", "izin_turId"),
                    ("personelId", "izin_personelKartId"),
                    ("donemBaslangic", "izin_donemBaslangic"),
                    ("gun", "izin_dusenGun"),
                    ("isKodu", "key"),
                ],
                [("asildi", "izin_donemAsildi", "false")],
            ),
        ],
    }
    ensure(access, "op_rules", "IZN - Hesap", hesap)

    ensure(access, "op_rules", "IZN - Cakisma kes", {
        "name": "IZN - Cakisma kes",
        "workspaceId": workspace_id,
        "ruleType": "validation",
        "trigger": "WorkItemTransition",
        "applyMode": "pre",
        "isActive": True,
        "priority": 90,
        "typeId": type_id,
        "transitionKey": "gonder",
        "errorMessage": "Bu tarihlerde acik veya onayli bir izin var.",
        "conditions": {"op": "and", "items": [{"field": "izin_cakismaAdet", "cmp": "gt", "value": 0}]},
        "querySteps": [],
    })
    ensure(access, "op_rules", "IZN - Tavan kes", {
        "name": "IZN - Tavan kes",
        "workspaceId": workspace_id,
        "ruleType": "validation",
        "trigger": "WorkItemTransition",
        "applyMode": "pre",
        "isActive": True,
        "priority": 80,
        "typeId": type_id,
        "transitionKey": "gonder",
        "errorMessage": "Istenen gun, turun tek talep tavanini asiyor.",
        "conditions": {"op": "and", "items": [{"field": "izin_tavanAsildi", "cmp": "eq", "value": True}]},
        "querySteps": [],
    })
    ensure(access, "op_rules", "IZN - Donem kes", {
        "name": "IZN - Donem kes",
        "workspaceId": workspace_id,
        "ruleType": "validation",
        "trigger": "WorkItemTransition",
        "applyMode": "pre",
        "isActive": True,
        "priority": 79,
        "typeId": type_id,
        "transitionKey": "gonder",
        "errorMessage": "Bu tur icin donem limiti dolu.",
        "conditions": {"op": "and", "items": [{"field": "izin_donemAsildi", "cmp": "eq", "value": True}]},
        "querySteps": [],
    })
    for name, code, message, priority, *rest in (
        ("IZN - Cikis kes", "cikis", "Cikis tarihi dolu personel yeni talep acamaz.", 75),
        ("IZN - Kapali tur kes", "kapali", "Kapali izin turu ile talep acilamaz.", 74),
        ("IZN - Cinsiyet kes", "cinsiyet", "Bu izin turu personelin cinsiyetine acik degil.", 73),
        ("IZN - Neden kes", "neden", "Mazeret izninde neden zorunlu.", 72, False),
        ("IZN - Belge kes", "belge", "Bu izin turunde belge zorunlu.", 71),
        ("IZN - Yarim gun kes", "yarim", "Yarim gun yalniz yillik ve ucretsiz izinde acik.", 70),
        ("IZN - Yerine kes", "yerine", "Yerine bakacak kisi talep sahibi ya da cikisi dolu biri olamaz.", 69),
    ):
        ensure(access, "op_rules", name, {
            "name": name,
            "workspaceId": workspace_id,
            "ruleType": "validation",
            "trigger": "WorkItemTransition",
            "applyMode": "pre",
            "isActive": rest[0] if rest else True,
            "priority": priority,
            "typeId": type_id,
            "transitionKey": "gonder",
            "errorMessage": message,
            "conditions": {"op": "and", "items": [{"field": "izin_engel", "cmp": "eq", "value": code}]},
            "querySteps": [],
        })
    ensure(access, "op_rules", "IZN - Hak kes", {
        "name": "IZN - Hak kes",
        "workspaceId": workspace_id,
        "ruleType": "validation",
        "trigger": "WorkItemTransition",
        "applyMode": "pre",
        "isActive": True,
        "priority": 76,
        "typeId": type_id,
        "transitionKey": "gonder",
        "errorMessage": "Bu personel icin talep acma hakkin yok.",
        "conditions": {"op": "and", "items": [{"field": "izin_hakVar", "cmp": "eq", "value": False}]},
        "querySteps": [],
    })
    ensure(access, "op_rules", "IZN - Yonetim ata", {
        "name": "IZN - Yonetim ata",
        "workspaceId": workspace_id,
        "ruleType": "default",
        "trigger": "WorkItemTransitioned",
        "isActive": True,
        "priority": 50,
        "typeId": type_id,
        "transitionKey": "gonder",
        "description": "Ilk zincirde onay Yonetim grubuna duser. Yonetici kisi atamasi sonra eklenebilir.",
        "actions": [{"type": "setAssignmentGroups", "groups": ["Yonetim"]}],
        "querySteps": [],
    })

    for name, transition_key, hareket, gun_field, anahtar in (
        ("IZN - Bloke yaz", "gonder", "bloke", "izin_dusenEksi", "izin_blokeAnahtar"),
        ("IZN - Onay iade", "onayla", "iade", "izin_dusenGun", "izin_iadeAnahtar"),
        ("IZN - Onay kullanim", "onayla", "kullanim", "izin_dusenEksi", "izin_kullanimAnahtar"),
        ("IZN - Red iade", "reddet", "iade", "izin_dusenGun", "izin_iadeAnahtar"),
        ("IZN - Iptal iade", "iptal", "iade", "izin_dusenGun", "izin_iadeAnahtar"),
    ):
        body = ledger_rule(name, type_id, transition_key, hareket, gun_field, anahtar)
        body["workspaceId"] = workspace_id
        ensure(access, "op_rules", name, body)

    _, reload = request(
        "POST",
        f"/operations/api/v1/workspaces/{workspace_id}/metadata-cache/reload",
        access,
        {},
    )
    print(f"onbellek yenilendi: {workspace_id} {reload}")
    print("izin calisma alani hazir")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as ex:
        print(ex, file=sys.stderr)
        sys.exit(1)
