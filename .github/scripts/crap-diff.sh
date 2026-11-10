#!/usr/bin/env bash
# Usage: crap-diff.sh <base-report> <pr-report> <diff-output>
set -euo pipefail

base=$1
pr=$2
output=$3

exit_code=0
dotnet-crap diff "$base" "$pr" --output "$output" || exit_code=$?

# diff writes no output on an error (exit code 2), and its own message is the one to keep.
if [ -f "$output" ] && [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  {
    echo '### CRAP diff against base'
    echo '```json'
    jq '.summary' "$output"
    echo '```'
  } >> "$GITHUB_STEP_SUMMARY"
fi

exit "$exit_code"
