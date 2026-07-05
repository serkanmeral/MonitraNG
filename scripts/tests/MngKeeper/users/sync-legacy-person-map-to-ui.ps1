# legacy-kalite-user-id-map.json -> Mng.Ui legacy person lookup modulu
#
# Kullanim:
#   .\scripts\tests\MngKeeper\users\sync-legacy-person-map-to-ui.ps1

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib/LegacyArchiveUserCommon.ps1")

$mapState = Load-LegacyKaliteUserIdMap
$keeperByLegacyId = @{}
$labelsByLegacyId = @{}

foreach ($key in $mapState.entries.Keys) {
    $e = $mapState.entries[$key]
    $keeperId = [string]$e.keeperUserId
    if (-not $keeperId -or $keeperId -eq "DRY-RUN") { continue }
    $legacyKey = [string]$key
    $keeperByLegacyId[$legacyKey] = $keeperId
    $label = [string]($e.legacyName ?? $e.legacyEmployeeName ?? "")
    if ($label) { $labelsByLegacyId[$legacyKey] = $label }
}

$outPath = Join-Path $LegacyArchiveRepoRoot "Mng.Ui/utils/odakSiparisLegacyPersonMap.ts"
$keeperJson = ($keeperByLegacyId.GetEnumerator() | Sort-Object Name | ForEach-Object { "  '$($_.Key)': '$($_.Value)'," }) -join "`n"
$labelsJson = ($labelsByLegacyId.GetEnumerator() | Sort-Object Name | ForEach-Object {
    $escaped = ($_.Value -replace '\\', '\\\\' -replace "'", "\'")
    "  '$($_.Key)': '$escaped',"
}) -join "`n"

$content = @"
/**
 * Legacy kalite users/employees -> Keeper userId eslemesi.
 * Uretim: scripts/tests/MngKeeper/users/sync-legacy-person-map-to-ui.ps1
 * Kaynak: docs/odak/eskiapp/reports/legacy-kalite-user-id-map.json
 */
export const LEGACY_PERSON_KEEPER_ID_BY_LEGACY_KEY: Record<string, string> = {
$keeperJson
};

export const LEGACY_PERSON_LABEL_BY_LEGACY_KEY: Record<string, string> = {
$labelsJson
};

export function legacyPersonLabel(
  legacyId: string | undefined,
  personLabels: Record<string, string> = {}
): string {
  const id = String(legacyId ?? '').trim();
  if (!id) return '';
  const keeperId = LEGACY_PERSON_KEEPER_ID_BY_LEGACY_KEY[id];
  if (keeperId && personLabels[keeperId]) return personLabels[keeperId];
  return LEGACY_PERSON_LABEL_BY_LEGACY_KEY[id] || '';
}

export function collectLegacyPersonKeeperIds(legacyIds: string[]): string[] {
  const out = new Set<string>();
  for (const raw of legacyIds) {
    const id = String(raw ?? '').trim();
    if (!id) continue;
    const keeperId = LEGACY_PERSON_KEEPER_ID_BY_LEGACY_KEY[id];
    if (keeperId) out.add(keeperId);
  }
  return [...out];
}
"@

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($outPath, $content, $utf8NoBom)
Write-Host "Yazildi: $outPath ($($keeperByLegacyId.Count) esleme)" -ForegroundColor Green
