# Update all plugin csproj files to target net8.0-windows

Get-ChildItem -Path "." -Filter "*.csproj" -Recurse | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    $updated = $content -replace '<TargetFramework>net8.0</TargetFramework>', '<TargetFramework>net8.0-windows</TargetFramework>'

    if ($content -ne $updated) {
        Set-Content -Path $_.FullName -Value $updated -NoNewline
        Write-Host "Updated: $($_.FullName)"
    }
}

Write-Host "`nAll plugin target frameworks updated to net8.0-windows"
