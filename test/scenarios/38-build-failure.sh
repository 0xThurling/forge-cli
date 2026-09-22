# A compilation error fails the build, and fixing it lets the next build pass.
scenario_38_build_failure() {
  local root="$WORK/38-build-failure"
  make_plain_project "$root" demo_broken
  printf 'int main() { this is not valid C++ }\n' >"$root/src/main.cpp"

  local out flat
  out="$(forge_in "$root" build 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  assert_exit 1 "compile error fails the build" forge_in "$root" build
  if grep -qiE "error:|Error 1" <<<"$flat"; then
    pass "the compiler error is surfaced"
  else
    fail "the compiler error is surfaced"
  fi
  assert_missing "$root/build/demo_broken" "no binary is produced"

  printf '#include <cstdio>\nint main() { std::printf("fixed\\n"); return 0; }\n' \
    >"$root/src/main.cpp"
  assert_exit 0 "fixing the source builds" forge_in "$root" build
  assert_runs "$root/build/demo_broken" "fixed" "the fixed binary runs"
}
