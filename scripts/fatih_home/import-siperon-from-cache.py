#!/usr/bin/env python3
"""Import Odak keeper cache into mng_siperon, anonymize, bind Keycloak."""
import json
import random
import subprocess
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

DOMAIN_ID = "6a89c0999b3629cdd970dce6"
DOMAIN = "siperon"
KC_BASE = "http://127.0.0.1:8080/keycloak"
KC_ADMIN_USER = "admin"
KC_ADMIN_PASS = "admin123"
DEFAULT_PASSWORD = "Sm123!?"
SEED = 20260822

PRESERVE_USERNAMES = {
    "serkan.meral",
    "fkosger",
    "siperon_admin",
    "cnc",
    "montaj",
    "talasli",
    "temiz-oda",
    "k.kesim",
}
SKIP_USERNAMES = {"odak_admin"}
PRESERVE_EMAIL = {
    "serkan.meral": "sermeral@gmail.com",
    "fkosger": "fatih.kosker@siperon.local",
}
FATIH_NAME = ("Fatih", "Köşker")

HERE = Path("/tmp/siperon-import")
USERS_JSON = HERE / "users.json"
GROUPS_JSON = HERE / "groups.json"
NAMES_JSON = HERE / "turkish-names.json"


def sh(args, input_text=None):
    return subprocess.check_output(
        args, input=input_text, text=True, stderr=subprocess.STDOUT
    )


def mongosh(js: str) -> str:
    return sh(
        [
            "docker", "exec", "-i", "mongo", "mongosh",
            "-u", "admin", "-p", "admin123",
            "--authenticationDatabase", "admin", "--quiet",
        ],
        input_text=js,
    )


def ascii_part(text: str) -> str:
    table = str.maketrans({
        "ç": "c", "Ç": "c", "ğ": "g", "Ğ": "g", "ı": "i", "İ": "i",
        "ö": "o", "Ö": "o", "ş": "s", "Ş": "s", "ü": "u", "Ü": "u",
    })
    out = "".join(ch.lower() for ch in text.translate(table) if ch.isalnum())
    return out or "user"


def http(method, url, token=None, body=None, form=False):
    headers = {}
    data = None
    if token:
        headers["Authorization"] = f"Bearer {token}"
    if form:
        headers["Content-Type"] = "application/x-www-form-urlencoded"
        data = body
    elif body is not None:
        headers["Content-Type"] = "application/json"
        data = json.dumps(body).encode("utf-8")
    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    with urllib.request.urlopen(req) as resp:
        raw = resp.read()
        return resp.status, (json.loads(raw) if raw else None)


def kc_token():
    data = urllib.parse.urlencode({
        "client_id": "admin-cli",
        "username": KC_ADMIN_USER,
        "password": KC_ADMIN_PASS,
        "grant_type": "password",
    }).encode()
    _, payload = http("POST", f"{KC_BASE}/realms/master/protocol/openid-connect/token", form=True, body=data)
    return payload["access_token"]


def unique_identity(rng, firsts, lasts, used):
    for _ in range(500):
        fn = firsts[rng.randrange(len(firsts))]
        ln = lasts[rng.randrange(len(lasts))]
        base = f"{ascii_part(fn)}.{ascii_part(ln)}"
        cand = base
        n = 2
        while cand in used:
            cand = f"{base}{n}"
            n += 1
            if n > 99:
                break
        if cand not in used:
            used.add(cand)
            return fn, ln, cand, f"{cand}@example.local"
    raise RuntimeError("username uretilemedi")


def main():
    users = json.loads(USERS_JSON.read_text(encoding="utf-8"))["users"]
    groups = json.loads(GROUPS_JSON.read_text(encoding="utf-8"))["groups"]
    names = json.loads(NAMES_JSON.read_text(encoding="utf-8"))
    firsts, lasts = names["firstNames"], names["lastNames"]
    rng = random.Random(SEED)

    print(f"cache users={len(users)} groups={len(groups)}")

    group_docs = []
    for g in groups:
        name = g.get("name") or ""
        if not name:
            continue
        group_docs.append({
            "name": name,
            "description": g.get("description") or "",
            "permissions": g.get("permissions") or [],
            "isActive": bool(g.get("isActive", True)),
            "includeInApplication": True,
            "domainId": DOMAIN_ID,
            "keycloakGroupId": "",
            "provisioningSource": 0,
        })

    used = {u.get("username", "").lower() for u in users}
    used.update(x.lower() for x in PRESERVE_USERNAMES)
    used.add("siperon_admin")

    user_docs = []
    for u in users:
        un = (u.get("username") or "").strip()
        if not un or un.lower() in SKIP_USERNAMES or un.lower() == "siperon_admin":
            print("SKIP", un or "(empty)")
            continue
        uid = u.get("userId")
        if not uid:
            continue
        first, last, email = u.get("firstName") or "", u.get("lastName") or "", u.get("email") or ""
        if un.lower() in PRESERVE_USERNAMES:
            if un.lower() == "fkosger":
                first, last = FATIH_NAME
            email = PRESERVE_EMAIL.get(un.lower(), email)
        else:
            first, last, un, email = unique_identity(rng, firsts, lasts, used)
        user_docs.append({
            "userId": uid,
            "username": un,
            "email": email,
            "firstName": first,
            "lastName": last,
            "title": None,
            "department": None,
            "gender": 0,
            "phoneNumber": None,
            "photoUrl": None,
            "isActive": bool(u.get("isActive", True)),
            "includeInApplication": True,
            "groups": u.get("groups") or [],
            "domainId": DOMAIN_ID,
            "keycloakUserId": "",
            "provisioningSource": 0,
            "preserved": un.lower() in PRESERVE_USERNAMES or u.get("username", "").lower() in PRESERVE_USERNAMES,
            "oldUsername": u.get("username"),
        })

    js_payload = json.dumps({"groups": group_docs, "users": user_docs}, ensure_ascii=False)
    js = f"""
const payload = {js_payload};
const d = db.getSiblingDB('mng_siperon');
const ug = d.getCollection('@groups');
const uu = d.getCollection('@users');
const existingNames = new Set();
ug.find({{}}, {{name:1}}).forEach(x => existingNames.add(x.name));
let gIns = 0, gSkip = 0;
payload.groups.forEach(g => {{
  if (existingNames.has(g.name)) {{ gSkip++; return; }}
  const oid = new ObjectId();
  g._id = oid;
  g.__dataId = oid.str;
  g.createdAt = new Date();
  g.createdBy = 'cache-import';
  ug.insertOne(g);
  existingNames.add(g.name);
  gIns++;
}});
let uIns = 0, uSkip = 0, uUpd = 0;
payload.users.forEach(u => {{
  const id = u.userId;
  const doc = Object.assign({{}}, u);
  delete doc.userId;
  delete doc.preserved;
  delete doc.oldUsername;
  doc.__dataId = id;
  doc.createdAt = new Date();
  doc.createdBy = 'cache-import';
  let oid = id;
  try {{ oid = ObjectId(id); }} catch (e) {{}}
  const existing = uu.findOne({{ $or: [{{ _id: oid }}, {{ username: u.username }}] }});
  if (existing && existing.username === 'siperon_admin') {{ uSkip++; return; }}
  if (existing) {{
    delete doc.keycloakUserId;
    uu.updateOne({{ _id: existing._id }}, {{ $set: doc }});
    uUpd++;
    return;
  }}
  doc._id = oid;
  uu.insertOne(doc);
  uIns++;
}});
print('GROUPS_INS=' + gIns + ' SKIP=' + gSkip + ' TOTAL=' + ug.countDocuments());
print('USERS_INS=' + uIns + ' UPD=' + uUpd + ' SKIP=' + uSkip + ' TOTAL=' + uu.countDocuments());
"""
    print(mongosh(js))

    tok = kc_token()
    admin = f"{KC_BASE}/admin/realms/{DOMAIN}"
    try:
        st, realm = http("GET", admin, tok)
        realm["editUsernameAllowed"] = True
        http("PUT", admin, tok, realm)
        print("editUsernameAllowed=true")
    except urllib.error.HTTPError as e:
        print("realm_put_warn", e.code)

    kc_groups = {}
    _, existing_g = http("GET", f"{admin}/groups?briefRepresentation=true&max=200", tok)
    for g in existing_g or []:
        kc_groups[g["name"]] = g["id"]
    for g in group_docs:
        name = g["name"]
        if name in kc_groups:
            continue
        try:
            http("POST", f"{admin}/groups", tok, {"name": name})
        except urllib.error.HTTPError as e:
            print("KC_GROUP_FAIL", name, e.code)
    _, existing_g = http("GET", f"{admin}/groups?briefRepresentation=true&max=200", tok)
    for g in existing_g or []:
        kc_groups[g["name"]] = g["id"]
    print("kc_groups", len(kc_groups))

    mongo_users = []
    raw = mongosh("""
db.getSiblingDB('mng_siperon').getCollection('@users').find({}).forEach(d => {
  print(JSON.stringify({
    id: String(d._id),
    username: d.username,
    email: d.email||'',
    firstName: d.firstName||'',
    lastName: d.lastName||'',
    groups: d.groups||[],
    enabled: d.isActive !== false,
    keycloakUserId: d.keycloakUserId||''
  }));
});
""")
    for line in raw.splitlines():
        line = line.strip()
        if line.startswith("{"):
            mongo_users.append(json.loads(line))

    ok = fail = 0
    tok = kc_token()
    for i, u in enumerate(mongo_users, 1):
        if i % 20 == 0:
            tok = kc_token()
        un = u["username"]
        kid = u.get("keycloakUserId")
        skip_password = un.lower() == "siperon_admin"
        body = {
            "username": un,
            "firstName": u["firstName"],
            "lastName": u["lastName"],
            "email": u["email"],
            "enabled": bool(u.get("enabled", True)),
            "emailVerified": True,
        }
        if not skip_password:
            body["credentials"] = [{
                "type": "password",
                "value": DEFAULT_PASSWORD,
                "temporary": False,
            }]
        try:
            if kid:
                upd = dict(body)
                upd.pop("credentials", None)
                http("PUT", f"{admin}/users/{kid}", tok, upd)
            else:
                try:
                    http("POST", f"{admin}/users", tok, body)
                except urllib.error.HTTPError as e:
                    if e.code != 409:
                        raise
                _, found = http("GET", f"{admin}/users?username={urllib.parse.quote(un)}&exact=true", tok)
                if not found:
                    print("KC_NO_ID", un)
                    fail += 1
                    continue
                kid = found[0]["id"]
                mongosh(
                    f"db.getSiblingDB('mng_siperon').getCollection('@users').updateOne("
                    f"{{username:'{un}'}}, {{$set:{{keycloakUserId:'{kid}'}}}});"
                )
                http("PUT", f"{admin}/users/{kid}", tok, {
                    "username": un,
                    "firstName": u["firstName"],
                    "lastName": u["lastName"],
                    "email": u["email"],
                    "enabled": bool(u.get("enabled", True)),
                    "emailVerified": True,
                })
            for gname in u.get("groups") or []:
                gid = kc_groups.get(gname)
                if not gid:
                    continue
                try:
                    http("PUT", f"{admin}/users/{kid}/groups/{gid}", tok)
                except urllib.error.HTTPError:
                    pass
            print("KC_OK", un)
            ok += 1
        except urllib.error.HTTPError as e:
            if e.code == 401:
                tok = kc_token()
                try:
                    http("POST", f"{admin}/users", tok, body)
                    _, found = http("GET", f"{admin}/users?username={urllib.parse.quote(un)}&exact=true", tok)
                    if found:
                        kid = found[0]["id"]
                        mongosh(
                            f"db.getSiblingDB('mng_siperon').getCollection('@users').updateOne("
                            f"{{username:'{un}'}}, {{$set:{{keycloakUserId:'{kid}'}}}});"
                        )
                        print("KC_OK", un, "retry")
                        ok += 1
                        continue
                except urllib.error.HTTPError as e2:
                    print("KC_FAIL", un, e2.code, e2.read()[:180].decode("utf-8", "replace"))
                    fail += 1
                    continue
            print("KC_FAIL", un, e.code, e.read()[:180].decode("utf-8", "replace"))
            fail += 1
    print(f"keycloak ok={ok} fail={fail}")


if __name__ == "__main__":
    main()
