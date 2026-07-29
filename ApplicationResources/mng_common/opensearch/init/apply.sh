#!/bin/sh
# Apply SIEM index templates + ISM policies to OpenSearch (idempotent).
set -eu

OS_URL="${OPENSEARCH_URL:-http://opensearch:9200}"
INIT_DIR="$(dirname "$0")"

echo "Waiting for OpenSearch at ${OS_URL} ..."
i=0
while [ "$i" -lt 60 ]; do
  if curl -sf "${OS_URL}/_cluster/health" >/dev/null 2>&1; then
    break
  fi
  i=$((i + 1))
  sleep 2
done

if ! curl -sf "${OS_URL}/_cluster/health" >/dev/null 2>&1; then
  echo "OpenSearch did not become ready in time." >&2
  exit 1
fi

echo "Cluster health:"
curl -sf "${OS_URL}/_cluster/health?pretty" || true

# 200/201 created/updated; 409 already exists (ISM policy create) — treat as OK
put_json() {
  path="$1"
  file="$2"
  echo "PUT ${path} <- $(basename "$file")"
  code=$(curl -s -o /tmp/os_resp.json -w "%{http_code}" -X PUT "${OS_URL}${path}" \
    -H "Content-Type: application/json" \
    --data-binary @"${file}")
  echo "  HTTP ${code}"
  cat /tmp/os_resp.json
  echo
  case "$code" in
    200|201|409) ;;
    *) echo "Failed: ${path}" >&2; exit 1 ;;
  esac
}

# Update existing ISM policy (needs seq_no / primary_term) — ignore if missing
put_ism_policy() {
  name="$1"
  file="$2"
  meta=$(curl -sf "${OS_URL}/_plugins/_ism/policies/${name}" || true)
  if [ -n "$meta" ] && echo "$meta" | grep -q '"_seq_no"'; then
    seq=$(echo "$meta" | sed -n 's/.*"_seq_no"[[:space:]]*:[[:space:]]*\([0-9]*\).*/\1/p' | head -1)
    term=$(echo "$meta" | sed -n 's/.*"_primary_term"[[:space:]]*:[[:space:]]*\([0-9]*\).*/\1/p' | head -1)
    if [ -n "$seq" ] && [ -n "$term" ]; then
      echo "PUT ISM update ${name} (seq=${seq} term=${term})"
      code=$(curl -s -o /tmp/os_resp.json -w "%{http_code}" \
        -X PUT "${OS_URL}/_plugins/_ism/policies/${name}?if_seq_no=${seq}&if_primary_term=${term}" \
        -H "Content-Type: application/json" \
        --data-binary @"${file}")
      echo "  HTTP ${code}"
      cat /tmp/os_resp.json
      echo
      case "$code" in
        200|201) return 0 ;;
      esac
    fi
  fi
  put_json "/_plugins/_ism/policies/${name}" "${file}"
}

put_ism_policy "mng-sec-events-90d" "${INIT_DIR}/ism-sec-events-90d.json"
put_ism_policy "mng-metrics-30d" "${INIT_DIR}/ism-metrics-30d.json"
put_json "/_index_template/mng-sec-events" "${INIT_DIR}/template-sec-events.json"
put_json "/_index_template/mng-metrics" "${INIT_DIR}/template-metrics.json"

echo "OpenSearch SIEM init complete."
