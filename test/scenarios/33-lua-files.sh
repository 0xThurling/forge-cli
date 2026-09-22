# The Lua file API: forge.download (with options), forge.fetch, forge.extract.
scenario_33_lua_files() {
  local root="$WORK/33-lua-files"
  mkdir -p "$root/src" "$root/.config/forge/build"
  make_plain_project "$root" demo_luafiles

  local srv="$root/srv"
  mkdir -p "$srv/repo-main"
  printf 'lua payload\n' >"$srv/repo-main/nested.txt"
  (cd "$srv" && tar -cf arch.tar repo-main)

  if ! start_http_server "$srv"; then
    skip "python3 unavailable for the local HTTP server"
    return
  fi
  local url="http://127.0.0.1:$HTTP_PORT"
  local sum
  sum="$(sha256sum "$srv/repo-main/nested.txt" | cut -d' ' -f1)"

  cat >"$root/.config/forge/build/files.lua" <<EOF
forge.download("$url/repo-main/nested.txt", "lua-dl.txt")
forge.download("$url/repo-main/nested.txt", "lua-hashed.txt", { sha256 = "$sum" })
forge.fetch("$url/arch.tar", "lua-fetched")
forge.extract("$srv/arch.tar", "lua-extracted", 0)
return {}
EOF

  local out flat
  out="$(forge_in "$root" build 2>&1 || true)"
  flat="$(flatten <<<"$out")"

  assert_exists "$root/lua-dl.txt" "forge.download writes the file"
  assert_contains "$root/lua-dl.txt" "lua payload" "forge.download body"
  assert_exists "$root/lua-hashed.txt" "forge.download with a sha256 option"
  if grep -qF "SHA256 verification passed" <<<"$flat"; then
    pass "forge.download verifies the hash"
  else
    fail "forge.download verifies the hash"
  fi
  assert_exists "$root/lua-fetched/nested.txt" "forge.fetch extracts (strip 1)"
  assert_exists "$root/lua-extracted/repo-main/nested.txt" "forge.extract keeps the layout with strip 0"

  stop_http_server
}
