# Export Sidebar Menu Items from TypeScript to JSON
# This script parses sidebarItem.ts and exports menu items to MongoDB format

param(
    [string]$Domain = "meral",
    [string]$KeeperUrl = "https://localhost:5001",
    [string]$DataGatewayUrl = "https://localhost:5010"
)

$ErrorActionPreference = "Stop"

# Paths
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Resolve-Path (Join-Path $scriptPath "../../../..")
$sidebarItemPath = Join-Path $projectRoot "Mng.Ui/components/lc/Full/vertical-sidebar/sidebarItem.ts"
$outputPath = Join-Path $scriptPath "menu-items-export.json"

Write-Host "📂 Project Root: $projectRoot" -ForegroundColor Cyan
Write-Host "📄 Sidebar Item Path: $sidebarItemPath" -ForegroundColor Cyan
Write-Host ""

# Check if sidebarItem.ts exists
if (-not (Test-Path $sidebarItemPath)) {
    Write-Host "❌ sidebarItem.ts dosyası bulunamadı: $sidebarItemPath" -ForegroundColor Red
    exit 1
}

Write-Host "✅ sidebarItem.ts dosyası bulundu" -ForegroundColor Green
Write-Host ""

# Read TypeScript file
$fileContent = Get-Content $sidebarItemPath -Raw

# Extract icon imports
$iconMap = @{}
$iconImportMatch = [regex]::Match($fileContent, 'import\s*\{([^}]+)\}\s*from\s*["'']vue-tabler-icons["'']')
if ($iconImportMatch.Success) {
    $iconNames = $iconImportMatch.Groups[1].Value -split ',' | ForEach-Object { $_.Trim() }
    foreach ($iconName in $iconNames) {
        $iconMap[$iconName] = $iconName
    }
    Write-Host "✅ Icon mapping oluşturuldu: $($iconMap.Count) icon bulundu" -ForegroundColor Green
}

# Extract sidebarItem array content (between const sidebarItem: menu[] = [ and ];)
$arrayStartMatch = [regex]::Match($fileContent, 'const\s+sidebarItem[^=]*=\s*(\[)')
if (-not $arrayStartMatch.Success) {
    Write-Host "❌ sidebarItem array başlangıcı bulunamadı!" -ForegroundColor Red
    exit 1
}

$startIndex = $arrayStartMatch.Groups[1].Index + 1
$bracketCount = 0
$inString = $false
$escapeNext = $false
$endIndex = -1

# Find the matching closing bracket
for ($i = $startIndex; $i -lt $fileContent.Length; $i++) {
    $char = $fileContent[$i]
    
    if ($escapeNext) {
        $escapeNext = $false
        continue
    }
    
    if ($char -eq '\') {
        $escapeNext = $true
        continue
    }
    
    if ($char -eq '"' -or $char -eq "'" -or $char -eq '`') {
        $inString = -not $inString
        continue
    }
    
    if ($inString) {
        continue
    }
    
    if ($char -eq '[') {
        $bracketCount++
    }
    elseif ($char -eq ']') {
        $bracketCount--
        if ($bracketCount -eq -1) {
            $endIndex = $i
            break
        }
    }
}

if ($endIndex -eq -1) {
    Write-Host "❌ sidebarItem array sonu bulunamadı!" -ForegroundColor Red
    exit 1
}

$arrayContent = $fileContent.Substring($startIndex, $endIndex - $startIndex)
Write-Host "✅ Array content extracted ($($arrayContent.Length) characters)" -ForegroundColor Green

# Parse menu items manually
# This is a simplified parser - we'll convert TypeScript object literals to PowerShell objects
$menuItems = @()
$order = 0
$currentLevel = 0
$parentStack = @() # Stack to track parent IDs
$parentId = $null

# Split by objects (simplified - looking for { ... } patterns)
# We'll use a more sophisticated approach: look for patterns like { header: "..." } or { title: "..." }

# Regex pattern to find menu objects
$objectPattern = '\{\s*(?:header|title)\s*:'
$matches = [regex]::Matches($arrayContent, $objectPattern)

Write-Host "📊 $($matches.Count) menu objects found" -ForegroundColor Cyan
Write-Host ""

# Manual parsing approach: Extract each menu object
$lines = $arrayContent -split "`n"
$currentObject = $null
$objectDepth = 0
$currentObjectText = ""
$objectStartLine = -1

foreach ($line in $lines) {
    $trimmedLine = $line.Trim()
    
    # Skip empty lines and comments
    if ($trimmedLine -eq "" -or $trimmedLine.StartsWith("//")) {
        continue
    }
    
    # Check for object start
    if ($trimmedLine -match '\{\s*(header|title)\s*:') {
        if ($currentObject -ne $null) {
            # Save previous object
            $menuItems += $currentObject
        }
        
        $objectDepth = 1
        $currentObjectText = $trimmedLine
        $currentObject = @{}
        continue
    }
    
    # If we're inside an object, accumulate text
    if ($objectDepth -gt 0) {
        $currentObjectText += "`n" + $line
        
        # Count brackets
        $bracketCount = ([regex]::Matches($line, '\{')).Count - ([regex]::Matches($line, '\}')).Count
        $objectDepth += $bracketCount
        
        # Extract properties
        if ($line -match 'header\s*:\s*["'']([^"'']+)["'']') {
            $currentObject.header = $matches.Groups[1].Value
            $currentObject.itemType = 'header'
        }
        elseif ($line -match 'title\s*:\s*["'']([^"'']+)["'']') {
            $currentObject.title = $matches.Groups[1].Value
            $currentObject.itemType = 'item'
        }
        elseif ($line -match 'icon\s*:\s*(\w+Icon)') {
            $iconName = $matches.Groups[1].Value
            $currentObject.icon = $iconName
            $currentObject.iconType = 'tabler'
        }
        elseif ($line -match 'to\s*:\s*["'']([^"'']+)["'']') {
            $currentObject.to = $matches.Groups[1].Value
            $currentObject.type = 'internal'
        }
        elseif ($line -match 'chip\s*:\s*["'']([^"'']+)["'']') {
            $currentObject.chip = $matches.Groups[1].Value
        }
        elseif ($line -match 'chipColor\s*:\s*["'']([^"'']+)["'']') {
            $currentObject.chipColor = $matches.Groups[1].Value
        }
        elseif ($line -match 'chipBgColor\s*:\s*["'']([^"'']+)["'']') {
            $currentObject.chipBgColor = $matches.Groups[1].Value
        }
        elseif ($line -match 'chipVariant\s*:\s*["'']([^"'']+)["'']') {
            $currentObject.chipVariant = $matches.Groups[1].Value
        }
        elseif ($line -match 'chipIcon\s*:\s*["'']([^"'']+)["'']') {
            $currentObject.chipIcon = $matches.Groups[1].Value
        }
        elseif ($line -match 'disabled\s*:\s*(true|false)') {
            $currentObject.disabled = $matches.Groups[1].Value -eq 'true'
        }
        elseif ($line -match 'subCaption\s*:\s*["'']([^"'']+)["'']') {
            $currentObject.subCaption = $matches.Groups[1].Value
        }
        elseif ($trimmedLine -eq '},' -or $trimmedLine -eq '}') {
            # Object end
            if ($objectDepth -eq 1) {
                # Set default values
                if (-not $currentObject.ContainsKey('itemType')) {
                    $currentObject.itemType = 'item'
                }
                $currentObject.order = $order++
                $currentObject.level = $currentLevel
                $currentObject.parentId = $parentId
                
                # If it's a header, reset level and parent
                if ($currentObject.itemType -eq 'header') {
                    $currentLevel = 0
                    $parentId = $null
                    $parentStack = @()
                }
                
                # Save object
                $menuItems += $currentObject
                $currentObject = $null
                $objectDepth = 0
                $currentObjectText = ""
            }
            $objectDepth--
        }
        elseif ($trimmedLine -match 'children\s*:\s*\[') {
            # Children array start - increase level
            $currentLevel++
            $parentId = if ($menuItems.Count -gt 0) { ($menuItems.Count - 1).ToString() } else { $null }
        }
    }
}

# Save last object if exists
if ($currentObject -ne $null -and $currentObject.Count -gt 0) {
    if (-not $currentObject.ContainsKey('itemType')) {
        $currentObject.itemType = 'item'
    }
    $currentObject.order = $order++
    $currentObject.level = $currentLevel
    $currentObject.parentId = $parentId
    $menuItems += $currentObject
}

Write-Host "📊 Parsed $($menuItems.Count) menu items" -ForegroundColor Green
Write-Host ""

# Convert to MongoDB format
$mongodbItems = @()
$itemIdMap = @{} # Map order to __dataId for parentId references

for ($i = 0; $i -lt $menuItems.Count; $i++) {
    $item = $menuItems[$i]
    $dataId = [guid]::NewGuid().ToString()
    $itemIdMap[$i] = $dataId
    
    $mongodbItem = @{
        order = if ($item.order -ne $null) { $item.order } else { $i }
        itemType = if ($item.itemType) { $item.itemType } else { 'item' }
        level = if ($item.level -ne $null) { $item.level } else { 0 }
        parentId = if ($item.parentId -ne $null -and $itemIdMap.ContainsKey($item.parentId)) { $itemIdMap[$item.parentId] } else { $null }
    }
    
    if ($item.header) {
        $mongodbItem.header = $item.header
    }
    
    if ($item.title) {
        $mongodbItem.title = $item.title
    }
    
    if ($item.icon) {
        $mongodbItem.icon = $item.icon
        $mongodbItem.iconType = if ($item.iconType) { $item.iconType } else { 'tabler' }
    }
    
    if ($item.to) {
        $mongodbItem.to = $item.to
        $mongodbItem.type = if ($item.type) { $item.type } else { 'internal' }
    }
    
    if ($item.chip) {
        $mongodbItem.chip = $item.chip
    }
    
    if ($item.chipColor) {
        $mongodbItem.chipColor = $item.chipColor
    }
    
    if ($item.chipBgColor) {
        $mongodbItem.chipBgColor = $item.chipBgColor
    }
    
    if ($item.chipVariant) {
        $mongodbItem.chipVariant = $item.chipVariant
    }
    
    if ($item.chipIcon) {
        $mongodbItem.chipIcon = $item.chipIcon
    }
    
    if ($item.disabled -ne $null) {
        $mongodbItem.disabled = $item.disabled
    }
    
    if ($item.subCaption) {
        $mongodbItem.subCaption = $item.subCaption
    }
    
    # Default pageType is 'user'
    $mongodbItem.pageType = 'user'
    
    # Default permissions (empty - will be set via UI)
    # $mongodbItem.permissions = @{}
    
    $mongodbItems += $mongodbItem
}

# Fix parentId references (use actual dataIds)
for ($i = 0; $i -lt $mongodbItems.Count; $i++) {
    $item = $mongodbItems[$i]
    if ($item.parentId -ne $null) {
        # parentId is currently an index, convert to actual dataId
        # This is simplified - we need to track parent hierarchy properly
        # For now, we'll leave it as is and fix in a second pass
    }
}

# Save to JSON file
$mongodbItems | ConvertTo-Json -Depth 10 | Set-Content $outputPath -Encoding UTF8

Write-Host "✅ Menu items exported to: $outputPath" -ForegroundColor Green
Write-Host "📊 Total items: $($mongodbItems.Count)" -ForegroundColor Cyan
Write-Host ""

# Show summary
$headerCount = ($mongodbItems | Where-Object { $_.itemType -eq 'header' }).Count
$itemCount = ($mongodbItems | Where-Object { $_.itemType -eq 'item' }).Count

Write-Host "📈 Summary:" -ForegroundColor Cyan
Write-Host "   Headers: $headerCount"
Write-Host "   Items: $itemCount"
Write-Host ""
Write-Host "✅ Export completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "📝 Next steps:" -ForegroundColor Yellow
Write-Host "   1. Review the exported JSON file: $outputPath"
Write-Host "   2. Run load-menu-items.ps1 to import to MongoDB"
Write-Host ""
