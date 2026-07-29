<#
.SYNOPSIS
  Temporary OpenSearch test dashboard for MngLogs / SIEM pilot data.

.DESCRIPTION
  Serves a local HTML UI that queries Odak OpenSearch (no Dashboards install).
  TEMPORARY — for verification only; stop with Ctrl+C.

.EXAMPLE
  .\start-os-test-dashboard.ps1
  .\start-os-test-dashboard.ps1 -OpenSearchUrl http://192.168.20.8:9200 -Domain odak -HostId TERMINAL-pilot
#>
[CmdletBinding()]
param(
    [string]$OpenSearchUrl = "http://192.168.20.8:9200",
    [string]$Domain = "odak",
    [string]$HostId = "",
    [int]$Port = 5099,
    [int]$Size = 50
)

$ErrorActionPreference = "Stop"
$osBase = $OpenSearchUrl.TrimEnd("/")
$indexPattern = "mng-$($Domain.ToLowerInvariant())-sec-events-*"

Write-Host "Checking OpenSearch $osBase ..." -ForegroundColor Cyan
try {
    $health = Invoke-RestMethod -Uri "$osBase/_cluster/health" -Method Get -TimeoutSec 8
    Write-Host "  cluster=$($health.cluster_name) status=$($health.status)" -ForegroundColor Green
}
catch {
    Write-Host "OpenSearch unreachable: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Fix VPN/network or -OpenSearchUrl, then retry." -ForegroundColor Yellow
    exit 1
}

function Get-OsRecent {
    param([string]$FilterHostId, [int]$Take)

    $must = @(
        @{ exists = @{ field = "@timestamp" } }
    )
    if (-not [string]::IsNullOrWhiteSpace($FilterHostId)) {
        $must += @{ term = @{ "host.id.keyword" = $FilterHostId } }
        # fallback if keyword mapping differs
        $must = @(
            @{ exists = @{ field = "@timestamp" } }
            @{ bool = @{
                    should = @(
                        @{ term = @{ "host.id.keyword" = $FilterHostId } }
                        @{ term = @{ "host.id" = $FilterHostId } }
                        @{ term = @{ "agent.id.keyword" = $FilterHostId } }
                        @{ match_phrase = @{ "host.id" = $FilterHostId } }
                    )
                    minimum_should_match = 1
                }
            }
        )
    }

    $body = @{
        size = $Take
        sort = @(@{ "@timestamp" = @{ order = "desc" } })
        query = @{ bool = @{ must = $must } }
    } | ConvertTo-Json -Depth 12 -Compress

    $uri = "$osBase/${indexPattern}/_search"
    return Invoke-RestMethod -Uri $uri -Method Post -ContentType "application/json" -Body $body -TimeoutSec 20
}

$html = @'
<!DOCTYPE html>
<html lang="tr">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>OS Test · MngLogs (geçici)</title>
  <style>
    :root {
      --bg: #0f1419;
      --panel: #1a2332;
      --border: #2d3a4d;
      --text: #e7ecf3;
      --muted: #8b9bb4;
      --accent: #3b82f6;
      --ok: #22c55e;
      --warn: #f59e0b;
    }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      font-family: "Segoe UI", system-ui, sans-serif;
      background: var(--bg);
      color: var(--text);
      min-height: 100vh;
    }
    header {
      padding: 1rem 1.25rem;
      border-bottom: 1px solid var(--border);
      background: var(--panel);
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem 1.25rem;
      align-items: center;
      justify-content: space-between;
    }
    h1 { margin: 0; font-size: 1.1rem; font-weight: 600; }
    .badge {
      display: inline-block;
      font-size: 0.7rem;
      padding: 0.15rem 0.45rem;
      border-radius: 999px;
      background: #7c2d12;
      color: #fed7aa;
      margin-left: 0.5rem;
      vertical-align: middle;
    }
    .meta { color: var(--muted); font-size: 0.85rem; }
    .controls { display: flex; flex-wrap: wrap; gap: 0.5rem; align-items: center; }
    input, select, button {
      background: var(--bg);
      border: 1px solid var(--border);
      color: var(--text);
      border-radius: 6px;
      padding: 0.4rem 0.65rem;
      font-size: 0.85rem;
    }
    button {
      background: var(--accent);
      border-color: var(--accent);
      cursor: pointer;
      font-weight: 600;
    }
    button:hover { filter: brightness(1.1); }
    main { padding: 1rem 1.25rem 2rem; max-width: 1400px; margin: 0 auto; }
    .stats {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
      gap: 0.75rem;
      margin-bottom: 1rem;
    }
    .stat {
      background: var(--panel);
      border: 1px solid var(--border);
      border-radius: 8px;
      padding: 0.75rem 1rem;
    }
    .stat .label { color: var(--muted); font-size: 0.75rem; }
    .stat .value { font-size: 1.25rem; font-weight: 650; margin-top: 0.2rem; }
    .err {
      background: #450a0a;
      border: 1px solid #991b1b;
      color: #fecaca;
      padding: 0.75rem 1rem;
      border-radius: 8px;
      margin-bottom: 1rem;
      white-space: pre-wrap;
    }
    table {
      width: 100%;
      border-collapse: collapse;
      background: var(--panel);
      border: 1px solid var(--border);
      border-radius: 8px;
      overflow: hidden;
      font-size: 0.82rem;
    }
    th, td {
      text-align: left;
      padding: 0.55rem 0.65rem;
      border-bottom: 1px solid var(--border);
      vertical-align: top;
    }
    th {
      background: #121a24;
      color: var(--muted);
      font-weight: 600;
      font-size: 0.72rem;
      text-transform: uppercase;
      letter-spacing: 0.03em;
    }
    tr:hover td { background: #1f2a3a; }
    tr.top-process td { border-left: 3px solid var(--warn); }
    .mono { font-family: ui-monospace, Consolas, monospace; font-size: 0.78rem; }
    .pill {
      display: inline-block;
      padding: 0.1rem 0.4rem;
      border-radius: 4px;
      background: #1e3a5f;
      color: #93c5fd;
      font-size: 0.72rem;
    }
    .pill.metric { background: #14532d; color: #86efac; }
    .pill.process { background: #78350f; color: #fde68a; }
    details { margin-top: 0.25rem; }
    summary { cursor: pointer; color: var(--muted); font-size: 0.75rem; }
    pre {
      margin: 0.35rem 0 0;
      max-height: 180px;
      overflow: auto;
      background: var(--bg);
      padding: 0.5rem;
      border-radius: 4px;
      font-size: 0.72rem;
    }
    .empty { color: var(--muted); padding: 2rem; text-align: center; }
  </style>
</head>
<body>
  <header>
    <div>
      <h1>OpenSearch test arayüzü <span class="badge">GEÇİCİ</span></h1>
      <div class="meta" id="subtitle">MngLogs → collector → OS doğrulama</div>
    </div>
    <div class="controls">
      <label class="meta">HostId
        <input id="hostId" placeholder="örn. TERMINAL-pilot" style="width:11rem" />
      </label>
      <label class="meta">Boyut
        <select id="size">
          <option>25</option>
          <option selected>50</option>
          <option>100</option>
          <option>200</option>
        </select>
      </label>
      <label class="meta"><input type="checkbox" id="auto" checked /> Otomatik (5 sn)</label>
      <button type="button" id="refresh">Yenile</button>
    </div>
  </header>
  <main>
    <div id="error" class="err" hidden></div>
    <div class="stats">
      <div class="stat"><div class="label">Kayıt</div><div class="value" id="sCount">—</div></div>
      <div class="stat"><div class="label">Metrik</div><div class="value" id="sMetric">—</div></div>
      <div class="stat"><div class="label">process.top_*</div><div class="value" id="sTop">—</div></div>
      <div class="stat"><div class="label">Son yenileme</div><div class="value" id="sAt" style="font-size:0.95rem">—</div></div>
    </div>
    <div id="tableWrap"><div class="empty">Yükleniyor…</div></div>
  </main>
  <script>
    const qs = new URLSearchParams(location.search);
    const hostInput = document.getElementById('hostId');
    const sizeSel = document.getElementById('size');
    const autoCb = document.getElementById('auto');
    const errEl = document.getElementById('error');
    const wrap = document.getElementById('tableWrap');
    if (qs.get('hostId')) hostInput.value = qs.get('hostId');

    function pillClass(source, action) {
      const a = (action || '').toLowerCase();
      if (a.includes('process.top')) return 'pill process';
      if ((source || '').toLowerCase() === 'metric') return 'pill metric';
      return 'pill';
    }

    function isTopProcess(action, fields) {
      const a = (action || '');
      const m = fields && (fields.metric || fields['metric']);
      return a.includes('process.top') || (typeof m === 'string' && m.startsWith('process.top'));
    }

    function fmt(ts) {
      if (!ts) return '—';
      try { return new Date(ts).toLocaleString('tr-TR'); } catch { return ts; }
    }

    function fieldMetric(f) {
      if (!f) return '';
      if (f.metric != null) return String(f.metric);
      return '';
    }

    function summarize(hit) {
      const s = hit._source || {};
      const fields = s.fields || {};
      const action = (s.event && s.event.action) || '';
      const metric = fieldMetric(fields);
      const processes = fields.processes;
      let detail = '';
      if (Array.isArray(processes) && processes.length) {
        detail = processes.slice(0, 5).map(p => {
          if (p.cpuPercent != null) return `${p.name}(${p.pid}) %${p.cpuPercent}`;
          if (p.workingSetBytes != null) {
            const mb = (Number(p.workingSetBytes) / (1024*1024)).toFixed(0);
            return `${p.name}(${p.pid}) ${mb}MB`;
          }
          return `${p.name}(${p.pid})`;
        }).join(', ');
      } else if (metric) {
        detail = `${metric}=${fields.value ?? ''}`;
        if (fields.volume) detail += ` · ${fields.volume}`;
      } else if (s.rawPreview) {
        detail = String(s.rawPreview).slice(0, 120);
      }
      return { s, fields, action, metric, detail };
    }

    async function load() {
      errEl.hidden = true;
      const hostId = hostInput.value.trim();
      const size = sizeSel.value;
      const url = `/api/recent?size=${encodeURIComponent(size)}` +
        (hostId ? `&hostId=${encodeURIComponent(hostId)}` : '');
      try {
        const res = await fetch(url);
        const data = await res.json();
        if (!res.ok) throw new Error(data.error || res.statusText);
        document.getElementById('subtitle').textContent =
          `${data.indexPattern} · hits=${data.total ?? (data.hits||[]).length}`;
        const hits = data.hits || [];
        let metric = 0, top = 0;
        hits.forEach(h => {
          const x = summarize(h);
          const src = (x.s.source && x.s.source.type) || '';
          if (String(src).toLowerCase() === 'metric' || x.metric) metric++;
          if (isTopProcess(x.action, x.fields)) top++;
        });
        document.getElementById('sCount').textContent = String(hits.length);
        document.getElementById('sMetric').textContent = String(metric);
        document.getElementById('sTop').textContent = String(top);
        document.getElementById('sAt').textContent = new Date().toLocaleTimeString('tr-TR');

        if (!hits.length) {
          wrap.innerHTML = '<div class="empty">Kayıt yok — ajan ship ediyor mu / domain-hostId doğru mu?</div>';
          return;
        }

        const rows = hits.map(h => {
          const x = summarize(h);
          const srcType = (x.s.source && x.s.source.type) || '—';
          const product = (x.s.source && x.s.source.product) || '';
          const host = (x.s.host && (x.s.host.id || x.s.host.name)) || '—';
          const cls = isTopProcess(x.action, x.fields) ? 'top-process' : '';
          const json = JSON.stringify(x.s, null, 2)
            .replace(/&/g,'&amp;').replace(/</g,'&lt;');
          return `<tr class="${cls}">
            <td class="mono">${fmt(x.s['@timestamp'])}</td>
            <td class="mono">${host}</td>
            <td><span class="${pillClass(srcType, x.action)}">${srcType}</span>
              ${product ? `<div class="meta">${product}</div>` : ''}</td>
            <td>${(x.action || '—').replace(/</g,'&lt;')}
              ${x.metric ? `<div class="mono meta">${x.metric}</div>` : ''}</td>
            <td>${(x.detail || '—').replace(/</g,'&lt;')}
              <details><summary>JSON</summary><pre>${json}</pre></details></td>
          </tr>`;
        }).join('');

        wrap.innerHTML = `<table>
          <thead><tr>
            <th>Zaman</th><th>Host</th><th>Kaynak</th><th>Action / metrik</th><th>Özet</th>
          </tr></thead>
          <tbody>${rows}</tbody>
        </table>`;
      } catch (e) {
        errEl.hidden = false;
        errEl.textContent = String(e.message || e);
      }
    }

    document.getElementById('refresh').onclick = load;
    hostInput.addEventListener('change', load);
    sizeSel.addEventListener('change', load);
    load();
    setInterval(() => { if (autoCb.checked) load(); }, 5000);
  </script>
</body>
</html>
'@

$listener = [System.Net.HttpListener]::new()
$prefix = "http://127.0.0.1:$Port/"
$listener.Prefixes.Add($prefix)
$listener.Start()

Write-Host ""
Write-Host "Geçici OS test dashboard: $prefix" -ForegroundColor Green
Write-Host "  index: $indexPattern" -ForegroundColor DarkGray
if ($HostId) { Write-Host "  default hostId: $HostId" -ForegroundColor DarkGray }
Write-Host "Durdurmak için Ctrl+C" -ForegroundColor Yellow
Write-Host ""

try {
    Start-Process "$prefix$(if ($HostId) { "?hostId=$([uri]::EscapeDataString($HostId))" } else { '' })"
}
catch { }

try {
    while ($listener.IsListening) {
        $ctx = $listener.GetContext()
        $req = $ctx.Request
        $res = $ctx.Response
        try {
            $path = $req.Url.AbsolutePath.TrimEnd('/').ToLowerInvariant()
            if ([string]::IsNullOrEmpty($path)) { $path = "/" }

            if ($path -eq "/" -or $path -eq "/index.html") {
                $bytes = [Text.Encoding]::UTF8.GetBytes($html)
                $res.ContentType = "text/html; charset=utf-8"
                $res.StatusCode = 200
                $res.OutputStream.Write($bytes, 0, $bytes.Length)
            }
            elseif ($path -eq "/api/recent") {
                $qHost = $req.QueryString["hostId"]
                if ([string]::IsNullOrWhiteSpace($qHost)) { $qHost = $HostId }
                $qSize = 50
                [int]::TryParse($req.QueryString["size"], [ref]$qSize) | Out-Null
                if ($qSize -lt 1) { $qSize = 50 }
                if ($qSize -gt 200) { $qSize = 200 }

                try {
                    $os = Get-OsRecent -FilterHostId $qHost -Take $qSize
                    $hits = @($os.hits.hits)
                    $total = $os.hits.total
                    if ($total -is [psobject] -and $null -ne $total.value) { $total = $total.value }
                    $payload = @{
                        ok           = $true
                        indexPattern = $indexPattern
                        openSearch   = $osBase
                        hostId       = $qHost
                        total        = $total
                        hits         = $hits
                    } | ConvertTo-Json -Depth 30 -Compress
                    $bytes = [Text.Encoding]::UTF8.GetBytes($payload)
                    $res.ContentType = "application/json; charset=utf-8"
                    $res.StatusCode = 200
                    $res.OutputStream.Write($bytes, 0, $bytes.Length)
                }
                catch {
                    $err = @{ ok = $false; error = $_.Exception.Message } | ConvertTo-Json -Compress
                    $bytes = [Text.Encoding]::UTF8.GetBytes($err)
                    $res.ContentType = "application/json; charset=utf-8"
                    $res.StatusCode = 502
                    $res.OutputStream.Write($bytes, 0, $bytes.Length)
                }
            }
            else {
                $res.StatusCode = 404
                $msg = [Text.Encoding]::UTF8.GetBytes("not found")
                $res.OutputStream.Write($msg, 0, $msg.Length)
            }
        }
        finally {
            $res.OutputStream.Close()
        }
    }
}
finally {
    $listener.Stop()
    $listener.Close()
}
