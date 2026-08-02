$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $projectRoot 'src\Kerosene.cs'
$manifest = Join-Path $projectRoot 'Kerosene.manifest'
$icon = Join-Path $projectRoot 'assets\Kerosene.ico'
$outputDirectory = Join-Path $projectRoot 'dist'
$output = Join-Path $outputDirectory 'Kerosene.exe'

$compilerCandidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)

$compiler = $compilerCandidates |
    Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1

if (-not $compiler) {
    throw 'Compilador C# do .NET Framework não encontrado.'
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

& $compiler `
    /nologo `
    /target:exe `
    /platform:anycpu `
    /optimize+ `
    "/win32icon:$icon" `
    "/win32manifest:$manifest" `
    "/out:$output" `
    $source

if ($LASTEXITCODE -ne 0) {
    throw "A compilação falhou com o código $LASTEXITCODE."
}

$file = Get-Item -LiteralPath $output
$hash = Get-FileHash -LiteralPath $output -Algorithm SHA256

Write-Host ''
Write-Host 'Kerosene compilado com sucesso.' -ForegroundColor Magenta
Write-Host "Arquivo: $($file.FullName)"
Write-Host "Tamanho: $($file.Length) bytes"
Write-Host "SHA-256: $($hash.Hash)"

