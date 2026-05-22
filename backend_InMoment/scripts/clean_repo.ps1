$paths = @(
    ".vs",
    "TestResults",
    "coverage",
    "coverage-report",
    "artifacts",
    "publish"
)

foreach ($path in $paths) {
    if (Test-Path $path) {
        Remove-Item $path -Recurse -Force
        Write-Host "Removed $path"
    }
}

Get-ChildItem -Path . -Recurse -Directory -Filter bin | ForEach-Object {
    Remove-Item $_.FullName -Recurse -Force
    Write-Host "Removed $($_.FullName)"
}

Get-ChildItem -Path . -Recurse -Directory -Filter obj | ForEach-Object {
    Remove-Item $_.FullName -Recurse -Force
    Write-Host "Removed $($_.FullName)"
}