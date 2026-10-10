#!/usr/bin/env sh
# Downloads the benchmark photo (the 2725×2225 image sharp uses for its benchmarks) and builds the derived inputs.
set -eu
dir="${1:-${IMG_DIR:-images}}"
mkdir -p "$dir"
curl -sSfL -o "$dir/photo-6mp.jpg" https://raw.githubusercontent.com/lovell/sharp/main/test/fixtures/2569067123_aca715a2ee_o.jpg
dotnet run -c Release --project "$(dirname "$0")/../bench/WebpResizer.Benchmarks" -- prepare "$dir"
