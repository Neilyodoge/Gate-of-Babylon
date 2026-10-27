param([switch]$SelfContained)
$ErrorActionPreference = 'Stop'
Push-Location -LiteralPath $PSScriptRoot
try {
    $glyphText = (Get-ChildItem -Filter '*.cs' | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8 }) -join ''
    $glyphChars = ($glyphText.ToCharArray() | Where-Object { [int]$_ -ge 128 } | Sort-Object -Unique) -join ''
    Set-Content -LiteralPath 'glyphs.txt' -Value $glyphChars -Encoding utf8
    if ($SelfContained) {
        dotnet publish '.\Stormfield.csproj' -c Release -r win-x64 --self-contained true -o '.\build'
    } else {
        dotnet publish '.\Stormfield.csproj' -c Release -o '.\build'
    }
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    Write-Host 'Ready: build\Stormfield.exe'
} finally { Pop-Location }
