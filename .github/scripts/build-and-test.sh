#!/usr/bin/env bash
# Usage: build-and-test.sh <checkout-dir> <test-results-dir>
#
# CI builds both the PR and its base with this script, because Coverlet measures branches
# in the IL: the two sides must compile the same way for their coverage to compare.
set -euo pipefail

# Through a symlinked path (macOS /var -> /private/var), MSBuild sees project references as
# projects outside the solution and also builds them in Debug.
checkout=$(cd "$1" && pwd -P)
results=$2

dotnet build "$checkout/crap4dotnet.sln" -c Release
dotnet test "$checkout/crap4dotnet.sln" -c Release --no-build \
  --collect:"XPlat Code Coverage" \
  --logger trx \
  --results-directory "$results"
