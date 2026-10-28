param([ValidatePattern('^[0-9]+([.][0-9]+){1,3}$')][string]$Version='0.30.0')
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    if (-not (Test-Path (Join-Path $PSScriptRoot 'assets/questlio.ico'))) { throw 'Не найдена иконка Questlio' }
    $sdk = Get-Command dotnet -ErrorAction SilentlyContinue
    $dotnet = if ($sdk) { $sdk.Source } else { Join-Path $env:ProgramFiles 'dotnet/dotnet.exe' }
    & $dotnet publish Questlio.csproj -c Release -r win-x64 --self-contained true -o "release-$Version"
    if ($LASTEXITCODE -ne 0) { throw 'Questlio build failed' }
    if (-not (Test-Path -LiteralPath (Join-Path "release-$Version" 'ui/fonts/InterVariable.ttf'))) { throw 'Шрифт Inter не попал в публикацию' }
    $launcherCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    if(Test-Path -LiteralPath $launcherCompiler){
        $launcherTarget=Join-Path (Split-Path $PSScriptRoot -Parent) 'Questlio.exe'
        & $launcherCompiler /nologo /target:winexe /reference:System.Windows.Forms.dll ('/win32icon:'+(Join-Path $PSScriptRoot 'assets/questlio.ico')) ('/out:'+$launcherTarget) (Join-Path $PSScriptRoot 'Launcher.cs')
        if($LASTEXITCODE -ne 0){throw 'Could not build the Questlio launcher'}
    }
    Set-Content -LiteralPath latest.txt -Value "release-$Version" -Encoding ascii
} finally { Pop-Location }

















