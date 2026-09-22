# `forge download` / `forge extract` / `forge fetch` against a local server.
scenario_32_download() {
  local root="$WORK/32-download"
  make_plain_project "$root" demo_dl

  # Fixtures: a flat file and archives with a top-level directory (the shape
  # --strip-components exists for).
  local srv="$root/srv"
  mkdir -p "$srv/repo-main"
  printf 'payload\n' >"$srv/repo-main/nested.txt"
  (cd "$srv" && tar -cf arch.tar repo-main && tar -czf arch.tar.gz repo-main)
  python3 - "$srv" <<'PY'
import sys, zipfile, os
srv = sys.argv[1]
with zipfile.ZipFile(os.path.join(srv, "arch.zip"), "w") as z:
    z.write(os.path.join(srv, "repo-main", "nested.txt"), "repo-main/nested.txt")
PY

  if ! start_http_server "$srv"; then
    skip "python3 unavailable for the local HTTP server"
    return
  fi
  local url="http://127.0.0.1:$HTTP_PORT"
  local sum
  sum="$(sha256sum "$srv/repo-main/nested.txt" | cut -d' ' -f1)"

  # --- download -------------------------------------------------------------
  assert_exit 0 "download" forge_in "$root" download "$url/repo-main/nested.txt" -o dl.txt
  assert_contains "$root/dl.txt" "payload" "download writes the body"
  assert_exit 0 "download verifies a matching hash" \
    forge_in "$root" download "$url/repo-main/nested.txt" -o dl-ok.txt --sha-256 "$sum"
  assert_exit 1 "download rejects a wrong hash" \
    forge_in "$root" download "$url/repo-main/nested.txt" -o dl-bad.txt --sha-256 deadbeef
  assert_missing "$root/dl-bad.txt" "a failed verification removes the file"
  assert_exit 1 "download reports a 404" \
    forge_in "$root" download "$url/nope.txt" -o dl-404.txt
  assert_exit 0 "download accepts --timeout and --show-progress" \
    forge_in "$root" download "$url/repo-main/nested.txt" -o dl-opts.txt --timeout 30 --show-progress
  assert_contains "$root/dl-opts.txt" "payload" "the options do not affect the body"

  # --- extract --------------------------------------------------------------
  assert_exit 0 "extract zip" forge_in "$root" extract "$srv/arch.zip" ex-zip
  assert_exists "$root/ex-zip/nested.txt" "zip strips one component by default"
  assert_exit 0 "extract zip (strip 0)" \
    forge_in "$root" extract "$srv/arch.zip" ex-zip0 --strip-components 0
  assert_exists "$root/ex-zip0/repo-main/nested.txt" "zip strip 0 keeps the layout"
  assert_exit 0 "extract tar (strip 1)" forge_in "$root" extract "$srv/arch.tar" ex-tar
  assert_exists "$root/ex-tar/nested.txt" "tar strip 1 removes the top directory"
  assert_exit 0 "extract tar (strip 0)" \
    forge_in "$root" extract "$srv/arch.tar" ex-tar0 --strip-components 0
  assert_exists "$root/ex-tar0/repo-main/nested.txt" "tar strip 0 keeps the layout"
  assert_exit 0 "extract tar.gz" forge_in "$root" extract "$srv/arch.tar.gz" ex-targz
  assert_exists "$root/ex-targz/nested.txt" "tar.gz contents extracted"
  assert_exit 1 "extract rejects an unknown format" \
    forge_in "$root" extract "$srv/repo-main/nested.txt" ex-bad

  # --- fetch ----------------------------------------------------------------
  assert_exit 0 "fetch tar" forge_in "$root" fetch "$url/arch.tar" ft-tar
  assert_exists "$root/ft-tar/nested.txt" "fetch extracts with strip 1 by default"
  assert_exit 0 "fetch tar.gz" forge_in "$root" fetch "$url/arch.tar.gz" ft-targz
  assert_exists "$root/ft-targz/nested.txt" "fetch handles gzip"
  assert_exit 0 "fetch zip" forge_in "$root" fetch "$url/arch.zip" ft-zip
  assert_exists "$root/ft-zip/nested.txt" "fetch strips one component for zip too"
  assert_exit 1 "fetch reports a 404" forge_in "$root" fetch "$url/nope.tar" ft-404
  assert_exit 0 "fetch verifies a matching hash" \
    forge_in "$root" fetch "$url/arch.tar" ft-hash --sha-256 \
    "$(sha256sum "$srv/arch.tar" | cut -d' ' -f1)"

  stop_http_server
}
