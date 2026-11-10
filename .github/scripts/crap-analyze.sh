#!/usr/bin/env bash
# Usage: crap-analyze.sh <checkout-dir> <test-results-dir> <output-json>
set -euo pipefail

checkout=$1
results=$2
output=$3

# The TRX logger copies each coverage file into an In/ folder; read only the originals.
mapfile -t coverage < <(find "$results" -name coverage.cobertura.xml -not -path '*/In/*')
if [ "${#coverage[@]}" -eq 0 ]; then
  echo "No coverage.cobertura.xml under $results" >&2
  exit 2
fi

# Exit code 1 only means some methods are over the threshold. The diff step decides
# whether that fails the build; exit code 2 is a real error.
exit_code=0
dotnet-crap analyze "$checkout/crap4dotnet.sln" --coverage "${coverage[@]}" --output "$output" || exit_code=$?
[ "$exit_code" -le 1 ] || exit "$exit_code"
