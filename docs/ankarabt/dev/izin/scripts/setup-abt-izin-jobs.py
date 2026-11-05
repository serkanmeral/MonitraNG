"""Create the three leave scheduler jobs on the abt domain.

They call the same query actions as the settings buttons.
{{today}}, {{year}} and {{serviceToken}} are filled when the job runs.
Cron is 04:00 UTC, which is 07:00 in Istanbul and the same calendar day.
Re-running updates the stored job.
"""

from __future__ import annotations

import json
import urllib.error
import urllib.request

BASE_URL = "http://192.168.1.249:5040"
DOMAIN = "abt"
USERNAME = "abt_admin"
PASSWORD = "Admin123!"
GATEWAY = "http://mnggateway:5000/data/api/v1/data"
CRON = "0 0 4 * * ?"

JOBS = [
    {
        "jobId": "izin-donem-acilis",
        "name": "Izin donem acilisi",
        "description": "Yildonumu gelen personelin devir ve hakedisini yazar.",
        "cronExpression": CRON,
        "endpointUrl": f"{GATEWAY}/izin_personel/actions/donem_acilis",
        "httpMethod": "POST",
        "headers": {"Authorization": "Bearer {{serviceToken}}"},
        "payload": "{\"tarih\":\"{{today}}\"}",
        "isActive": True,
        "timeoutSeconds": 300,
    },
    {
        "jobId": "izin-bant-farki",
        "name": "Izin bant farki",
        "description": "Acik donemde yukselen hakedis farkini yazar.",
        "cronExpression": CRON,
        "endpointUrl": f"{GATEWAY}/izin_personel/actions/bant_farki",
        "httpMethod": "POST",
        "headers": {"Authorization": "Bearer {{serviceToken}}"},
        "payload": "{\"tarih\":\"{{today}}\"}",
        "isActive": True,
        "timeoutSeconds": 300,
    },
    {
        "jobId": "izin-tatil-yili",
        "name": "Izin tatil yili",
        "description": "Sabit milli tatil kaliplarini yilin gunlerine acar.",
        "cronExpression": CRON,
        "endpointUrl": f"{GATEWAY}/izin_tatil_kaliplari/actions/tatil_yili",
        "httpMethod": "POST",
        "headers": {"Authorization": "Bearer {{serviceToken}}"},
        "payload": "{\"yil\":{{year}}}",
        "isActive": True,
        "timeoutSeconds": 300,
    },
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
        if ex.code == 404:
            return 404, detail
        raise RuntimeError(f"{method} {path} -> {ex.code} {detail}") from ex


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


def main() -> int:
    access = token()
    print("token alindi")
    for job in JOBS:
        path = f"/scheduler/api/v1/user/jobs/{job['jobId']}"
        status, found = request("GET", path, access)
        found_id = found.get("jobId") if isinstance(found, dict) else None
        if status == 404 or found_id != job["jobId"]:
            _, created = request("POST", "/scheduler/api/v1/user/jobs", access, job)
            print(f"olustu: {job['jobId']} {created.get('jobId') if isinstance(created, dict) else created}")
            continue
        _, updated = request("PUT", path, access, job)
        print(f"guncellendi: {job['jobId']} {updated.get('cronExpression') if isinstance(updated, dict) else updated}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
