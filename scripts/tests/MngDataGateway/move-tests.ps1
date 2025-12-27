# Move test files to organized structure
$source = "C:\Serkan\iSIM\MonitraNG\MngDataGateway\tests"
$base = "C:\Serkan\iSIM\MonitraNG\scripts\tests\MngDataGateway"

# Dataset files
$datasetFiles = @('check-datasets.ps1','setup-books-datasets.ps1','setup-test-datasets.ps1','test-datasets.ps1','check-books-dataset.ps1','update-tasks-dataset.ps1')
foreach ($f in $datasetFiles) {
    $src = Join-Path $source $f
    $dst = Join-Path "$base\dataset" $f
    if (Test-Path $src) { Move-Item $src $dst -Force; Write-Host "Moved: $f" }
}

# Data files
$dataFiles = @('test-data-crud.ps1','test-bulk-insert.ps1','insert-books-test-data.ps1','insert-books-bulk-test.ps1','load-test-data.ps1')
foreach ($f in $dataFiles) {
    $src = Join-Path $source $f
    $dst = Join-Path "$base\data" $f
    if (Test-Path $src) { Move-Item $src $dst -Force; Write-Host "Moved: $f" }
}

# Validation files
$validationFiles = @('test-validations.ps1','test-single-validation.ps1','test-single-validation-detailed.ps1','test-edge-cases-debug.ps1','test-expression-ratio.ps1','tst_books_dataset_with_validations.json')
foreach ($f in $validationFiles) {
    $src = Join-Path $source $f
    $dst = Join-Path "$base\validation" $f
    if (Test-Path $src) { Move-Item $src $dst -Force; Write-Host "Moved: $f" }
}

# Query files
$queryFiles = @('test-predefined-query.ps1','test-query-simple-sort.ps1','test-query-update.ps1','test-query-with-parameters.ps1','test-all-query-examples.ps1')
foreach ($f in $queryFiles) {
    $src = Join-Path $source $f
    $dst = Join-Path "$base\query" $f
    if (Test-Path $src) { Move-Item $src $dst -Force; Write-Host "Moved: $f" }
}

# Search files
$searchFiles = @('test-search-basic.ps1','test-search-relations.ps1')
foreach ($f in $searchFiles) {
    $src = Join-Path $source $f
    $dst = Join-Path "$base\search" $f
    if (Test-Path $src) { Move-Item $src $dst -Force; Write-Host "Moved: $f" }
}

# Filter files
$filterFiles = @('test-price-filter.ps1')
foreach ($f in $filterFiles) {
    $src = Join-Path $source $f
    $dst = Join-Path "$base\filter" $f
    if (Test-Path $src) { Move-Item $src $dst -Force; Write-Host "Moved: $f" }
}

# Aggregate files
$aggregateFiles = @('test-aggregate.ps1')
foreach ($f in $aggregateFiles) {
    $src = Join-Path $source $f
    $dst = Join-Path "$base\aggregate" $f
    if (Test-Path $src) { Move-Item $src $dst -Force; Write-Host "Moved: $f" }
}

# Export files
$exportFiles = @('test-csv-export.ps1')
foreach ($f in $exportFiles) {
    $src = Join-Path $source $f
    $dst = Join-Path "$base\export" $f
    if (Test-Path $src) { Move-Item $src $dst -Force; Write-Host "Moved: $f" }
}

# Events files
$eventsFiles = @('test-rabbitmq-events.ps1')
foreach ($f in $eventsFiles) {
    $src = Join-Path $source $f
    $dst = Join-Path "$base\events" $f
    if (Test-Path $src) { Move-Item $src $dst -Force; Write-Host "Moved: $f" }
}

# Index files
$indexFiles = @('test-index-definitions.ps1')
foreach ($f in $indexFiles) {
    $src = Join-Path $source $f
    $dst = Join-Path "$base\index" $f
    if (Test-Path $src) { Move-Item $src $dst -Force; Write-Host "Moved: $f" }
}

# Persons files
$personsFiles = @('test-person-expansion.ps1','test-persons-field-type.ps1')
foreach ($f in $personsFiles) {
    $src = Join-Path $source $f
    $dst = Join-Path "$base\persons" $f
    if (Test-Path $src) { Move-Item $src $dst -Force; Write-Host "Moved: $f" }
}

# Dataset-categories files
$catFiles = @('test-dataset-categories.ps1')
foreach ($f in $catFiles) {
    $src = Join-Path $source $f
    $dst = Join-Path "$base\dataset-categories" $f
    if (Test-Path $src) { Move-Item $src $dst -Force; Write-Host "Moved: $f" }
}

# Docs files
$docsFiles = @('TEST_GUIDE.md','test-get-operations.ps1','test-mongo-context-service.ps1')
foreach ($f in $docsFiles) {
    $src = Join-Path $source $f
    $dst = Join-Path "$base\docs" $f
    if (Test-Path $src) { Move-Item $src $dst -Force; Write-Host "Moved: $f" }
}

# Config files
$configFiles = @('update-all-scripts-to-use-common-token.ps1')
foreach ($f in $configFiles) {
    $src = Join-Path $source $f
    $dst = Join-Path "$base\config" $f
    if (Test-Path $src) { Move-Item $src $dst -Force; Write-Host "Moved: $f" }
}

Write-Host "`nAll files moved!" -ForegroundColor Green

