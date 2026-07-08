# Shared helper: seed JSON template.parameters -> MngDocument PUT /parameters body entries (G1/G2).

function ConvertTo-DiTemplateParameterEntries {
    param([object[]]$Parameters)

    $params = @()
    foreach ($p in @($Parameters)) {
        $entry = @{
            key             = [string]$p.key
            label           = [string]$p.label
            dataType        = [string]$p.dataType
            valueSourceMode = [string]$p.valueSourceMode
        }
        if ($p.kind) { $entry.kind = [string]$p.kind }
        if ($p.defaultValue) { $entry.defaultValue = [string]$p.defaultValue }
        if ($p.format) { $entry.format = [string]$p.format }
        if ($p.contextBinding) {
            $cb = @{ path = [string]$p.contextBinding.path }
            if ($p.contextBinding.fallbackPath) { $cb.fallbackPath = [string]$p.contextBinding.fallbackPath }
            if ($p.contextBinding.format) { $cb.format = [string]$p.contextBinding.format }
            $entry.contextBinding = $cb
        }
        if ($p.incremental) {
            $entry.incremental = @{
                format        = [string]$p.incremental.format
                startValue    = [int]$p.incremental.startValue
                incrementStep = [int]$p.incremental.incrementStep
                scopeKey      = [string]$p.incremental.scopeKey
                resetPolicy   = [string]$p.incremental.resetPolicy
            }
        }
        if ($p.valueSource) {
            $vs = @{ mode = [string]$p.valueSource.mode }
            if ($p.valueSource.provider) { $vs.provider = [string]$p.valueSource.provider }
            if ($p.valueSource.dataset) { $vs.dataset = [string]$p.valueSource.dataset }
            if ($p.valueSource.queryName) { $vs.queryName = [string]$p.valueSource.queryName }
            if ($p.valueSource.idFrom) { $vs.idFrom = [string]$p.valueSource.idFrom }
            if ($p.valueSource.query) { $vs.query = [string]$p.valueSource.query }
            if ($p.valueSource.path) { $vs.path = [string]$p.valueSource.path }
            if ($p.valueSource.fallbackPath) { $vs.fallbackPath = [string]$p.valueSource.fallbackPath }
            if ($p.valueSource.match) { $vs.match = $p.valueSource.match }
            if ($p.valueSource.parameters) { $vs.parameters = $p.valueSource.parameters }
            if ($p.valueSource.columns) { $vs.columns = @($p.valueSource.columns) }
            $entry.valueSource = $vs
        }
        if ($p.docBinding) {
            $db = @{ regionKind = [string]$p.docBinding.regionKind }
            if ($null -ne $p.docBinding.paragraphIndex) { $db.paragraphIndex = [int]$p.docBinding.paragraphIndex }
            if ($p.docBinding.originalText) { $db.originalText = [string]$p.docBinding.originalText }
            if ($null -ne $p.docBinding.charStart) { $db.charStart = [int]$p.docBinding.charStart }
            if ($null -ne $p.docBinding.charEnd) { $db.charEnd = [int]$p.docBinding.charEnd }
            if ($null -ne $p.docBinding.tableIndex) { $db.tableIndex = [int]$p.docBinding.tableIndex }
            if ($null -ne $p.docBinding.headerRowIndex) { $db.headerRowIndex = [int]$p.docBinding.headerRowIndex }
            if ($null -ne $p.docBinding.templateRowIndex) { $db.templateRowIndex = [int]$p.docBinding.templateRowIndex }
            $entry.docBinding = $db
        }
        if ($p.dataSourceRef) { $entry.dataSourceRef = [string]$p.dataSourceRef }
        $params += $entry
    }
    return $params
}
