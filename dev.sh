#!/usr/bin/env bash
# Convenience entry point: the end-to-end suite lives in test/.
# See test/README.md for the scenario list and conventions.
exec "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/test/run.sh" "$@"
