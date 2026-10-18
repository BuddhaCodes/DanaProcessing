#!/usr/bin/env bash
# Which version a build-release.yml run packs, and whether it's a pre-release.
#
#   ci-version.sh <csproj Version> <run number> <cut_release true|false>
#
# Prints two lines: "version=<x>" and "prerelease=<true|false>" (ready for
# $GITHUB_OUTPUT). Velopack refuses a version that isn't strictly greater than
# the release it downloads for the delta, so every CI version must sort ABOVE
# everything published before it and BELOW the official release it leads to:
#
#   csproj 1.0.0-rc.1 -> 1.0.0-rc.1.<run>     (as before)
#   csproj 1.0.0, v1.0.0 not released yet
#                     -> 1.0.0-rtm.<run>      ("rtm" sorts after "rc": newer than
#                                              every 1.0.0-rc.1.N, older than 1.0.0)
#   csproj 1.0.0, v1.0.0 already released
#                     -> 1.0.1-ci.<run>       (builds toward the next patch)
#   cut_release=true  -> 1.0.0, a stable release
#
# Every CI build is a GitHub *pre-release*: the IDE's updater only offers
# stable releases, and the website's download button reads the latest stable
# one, so day-to-day pushes reach neither. RELEASED=true|false overrides the
# "is v<Version> already a tag?" check (tests; offline).
set -euo pipefail
base="$1"; run="$2"; cut="${3:-false}"

if [ "$cut" = "true" ]; then
  echo "version=$base"
  echo "prerelease=false"
  exit 0
fi

if [[ "$base" == *-* ]]; then
  version="${base}.${run}"
else
  released="${RELEASED:-}"
  if [ -z "$released" ]; then
    if git ls-remote --exit-code --tags origin "refs/tags/v${base}" >/dev/null 2>&1; then released=true; else released=false; fi
  fi
  if [ "$released" = "true" ]; then
    IFS=. read -r major minor patch <<< "$base"
    version="${major}.${minor}.$((patch + 1))-ci.${run}"
  else
    version="${base}-rtm.${run}"
  fi
fi
echo "version=$version"
echo "prerelease=true"
