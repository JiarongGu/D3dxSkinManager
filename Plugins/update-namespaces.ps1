# Update all plugin files to use the correct namespace for models
# Changes: using D3dxSkinManager.Models; -> using D3dxSkinManager.Modules.Core.Models;
# Also fixes: using D3dxSkinManager.Services; -> using D3dxSkinManager.Modules.Tools.Services;

Get-ChildItem -Path "." -Filter "*Plugin.cs" -Recurse | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    $updated = $content

    # Fix Models namespace
    $updated = $updated -replace 'using D3dxSkinManager\.Models;', 'using D3dxSkinManager.Modules.Core.Models;'

    # Fix Services namespace (for plugins that use CacheService, etc.)
    $updated = $updated -replace 'using D3dxSkinManager\.Services;', 'using D3dxSkinManager.Modules.Tools.Services;'

    if ($content -ne $updated) {
        Set-Content -Path $_.FullName -Value $updated -NoNewline
        Write-Host "Updated: $($_.FullName)"
    }
}

Write-Host "`nAll plugin namespaces updated"
