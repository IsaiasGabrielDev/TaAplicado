# Builds dist\TaAplicado.zip to hand to someone else: .NET is bundled (self-contained), so the other
# computer needs only Windows 10/11. Chrome and Claude Code are installed from the app's "Antes de começar" steps.
$ErrorActionPreference = 'Stop'
$out = Join-Path $PSScriptRoot 'dist\TaAplicado'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish "$PSScriptRoot\LinkedInAutoApply.csproj" -c Release -r win-x64 --self-contained -o $out
Compress-Archive -Path $out -DestinationPath "$out.zip" -Force
Write-Host "Pronto: $out.zip"
