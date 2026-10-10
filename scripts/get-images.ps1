# Downloads the benchmark photo (the 2725×2225 image sharp uses for its benchmarks) and builds the derived inputs.
param([string]$Dir = $(if ($env:IMG_DIR) { $env:IMG_DIR } else { "images" }))
$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $Dir | Out-Null
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/lovell/sharp/main/test/fixtures/2569067123_aca715a2ee_o.jpg" -OutFile (Join-Path $Dir "photo-6mp.jpg")
dotnet run -c Release --project (Join-Path $PSScriptRoot "../bench/WebpResizer.Benchmarks") -- prepare $Dir
