$ErrorActionPreference='Stop'
try {
    $version=(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'latest.txt') -Raw).Trim()
    if ($version -notmatch '^release-[0-9]+([.][0-9]+)*$') { throw 'Некорректная версия приложения' }
    $executable=Join-Path (Join-Path $PSScriptRoot $version) 'Questlio.exe'
    if (-not (Test-Path -LiteralPath $executable)) { throw 'Сборка Questlio не найдена' }
    Start-Process -FilePath $executable -WorkingDirectory (Split-Path -Parent $executable)
} catch {
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show($_.Exception.Message,'Questlio') | Out-Null
}

