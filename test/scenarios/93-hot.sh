# `forge hot`: builds with the engine wired in, runs the app, and signals a
# reload on save. The suite has no network, so the engine is a local stub here;
# set FORGE_HOT_E2E=1 to run the real reload against the pinned jet-live.
scenario_93_hot() {
  local root="$WORK/93-hot"
  local stub="$root/stub"
  mkdir -p "$root/src" "$stub/src/jet/live" "$stub/cmake"

  # --- a stub engine: the same surface the glue compiles against -----------
  cat >"$stub/CMakeLists.txt" <<'CMAKE'
cmake_minimum_required(VERSION 3.23)
project(jetlive_stub CXX)
add_library(jet-live STATIC src/stub.cpp)
target_include_directories(jet-live PUBLIC ${CMAKE_CURRENT_SOURCE_DIR}/src)
CMAKE
  cat >"$stub/cmake/jet_live_setup.cmake" <<'CMAKE'
# The real engine sets its compile/link flags here; the stub needs none.
set(JET_LIVE_CONFIGURED ON)
CMAKE
  cat >"$stub/src/jet/live/Live.hpp" <<'HPP'
#pragma once
#include <csignal>
#include <cstddef>
#include <memory>
#include <string>
namespace jet
{
  enum class LogSeverity { kInfo, kWarning, kError };
  class ILiveListener
  {
  public:
    virtual ~ILiveListener() = default;
    virtual void onLog(LogSeverity, const std::string&) {}
    virtual void onCodePreLoad() {}
    virtual void onCodePostLoad() {}
  };
  struct LiveConfig
  {
    std::size_t workerThreadsCount = 4;
    bool reloadOnSignal = true;
  };
  class Live
  {
  public:
    // The stub cannot patch code; it only ignores the reload signal so the
    // app survives a save and the plumbing around it can be asserted.
    Live(std::unique_ptr<ILiveListener>&&, const LiveConfig& = {}) { std::signal(SIGUSR1, SIG_IGN); }
    void update() {}
    bool isInitialized() const { return true; }
    void tryReload() {}
  };
}
HPP
  printf 'namespace jet { void forge_stub_anchor() {} }\n' >"$stub/src/stub.cpp"

  # --- the project ---------------------------------------------------------
  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "hotdemo", type = "executable", standard = "20" },
  dependencies = {
    direct = { jetlive = { path = "stub", target = "jet-live" } },
    conan = {},
  },
  resources = { files = {} },
  scripts = {},
  features = {}
}
LUA
  cat >"$root/src/main.cpp" <<'CPP'
#include "forge_hot.h"
#include <chrono>
#include <cstdio>
#include <string>
#include <thread>
std::string message();
int tick();
int main()
{
  forge_hot_init();
  std::printf("READY\n");
  std::fflush(stdout);
  for (int i = 0; i < 3000; ++i)
  {
    forge_hot_update();
    std::printf("tick %d | %s | counter %d\n", i, message().c_str(), tick());
    std::fflush(stdout);
    std::this_thread::sleep_for(std::chrono::milliseconds(200));
  }
  return 0;
}
CPP
  cat >"$root/src/message.cpp" <<'CPP'
#include <string>
std::string message() { return "v1"; }
int tick()
{
  static int counter = 0;
  return ++counter;
}
CPP

  # Sets (or clears) the single-line `build = { ... }` section of forge.lua.
  set_build() {
    python3 - "$root/forge.lua" "$1" <<'PY'
import pathlib, re, sys
path, body = pathlib.Path(sys.argv[1]), sys.argv[2]
text = path.read_text()
text = re.sub(r"  build = \{[^\n]*\},\n", "", text)
if body:
    text = text.replace("  resources", "  build = { " + body + " },\n  resources")
path.write_text(text)
PY
  }

  # --- build, run, reload on save -----------------------------------------
  local log="$root/hot.log"
  (cd "$root" && exec "${FORGE_CMD[@]}" hot) >"$log" 2>&1 &
  local hot_pid=$!

  local started=0
  for _ in $(seq 1 900); do
    grep -q READY "$log" 2>/dev/null && { started=1; break; }
    sleep 0.2
  done

  if [[ "$started" -eq 1 ]]; then
    pass "forge hot builds and starts the app"
  else
    fail "forge hot builds and starts the app ($(flatten <"$log" | tail -c 200))"
  fi

  assert_contains "$log" "Hot reload running:" "the run reports the executable and its pid"
  assert_contains "$root/.config/cmake/CMakeLists.txt" "jet_live_setup.cmake" \
    "the generated CMake includes the engine setup"
  assert_contains "$root/.config/cmake/CMakeLists.txt" "forge_hot.cpp" \
    "the generated CMake compiles the glue"
  assert_contains "$root/.config/forge/hot/forge_hot.h" "forge_hot_update" \
    "the glue exposes init/update"

  # A save is picked up and signalled; the app must keep running.
  sed_in_place 's/return "v1";/return "v1 edited";/' "$root/src/message.cpp"
  local detected=0
  for _ in $(seq 1 300); do
    grep -q "── change" "$log" 2>/dev/null && { detected=1; break; }
    sleep 0.1
  done
  if [[ "$detected" -eq 1 ]]; then
    pass "a save is detected and signalled"
  else
    fail "a save is detected and signalled"
  fi
  if pgrep -x hotdemo >/dev/null; then
    pass "the app keeps running after the signal"
  else
    fail "the app keeps running after the signal"
  fi

  kill "$hot_pid" 2>/dev/null || true
  pkill -x hotdemo 2>/dev/null || true
  wait "$hot_pid" 2>/dev/null || true

  # --- the mode refuses what it cannot patch -------------------------------
  set_build 'unity = true'
  assert_exit 1 "unity is rejected" forge_in "$root" hot
  set_build 'presets = { "asan" }'
  assert_exit 1 "a sanitizer preset is rejected" forge_in "$root" hot
  set_build 'presets = { "lto" }'
  assert_exit 1 "lto is rejected" forge_in "$root" hot
  set_build 'hot = true'
  assert_exit 1 "release is rejected when hot is on" forge_in "$root" build --release

  # --- Ninja deletes depfiles; a hot build must keep them ------------------
  set_build 'hot = true, generator = "Ninja"'
  rm -rf "$root/build"
  forge_in_timeout 300 "$root" build --debug >"$root/ninja.log" 2>&1
  local depfiles
  depfiles="$(find "$root/build/CMakeFiles" -name '*.o.d' 2>/dev/null | wc -l || true)"
  if [[ "$depfiles" -ge 2 ]]; then
    pass "a Ninja hot build keeps the depfiles the engine reads"
  else
    fail "a Ninja hot build keeps the depfiles the engine reads (found $depfiles)"
  fi
  set_build ''

  # --- the real engine, when asked (needs network for the pinned jet-live) --
  if [[ "${FORGE_HOT_E2E:-0}" != "1" ]]; then
    skip "the real engine reload (set FORGE_HOT_E2E=1)"
    return
  fi

  # The stub part left its own edit in the source; start from a clean slate.
  sed_in_place 's/return "v1 edited";/return "v1";/' "$root/src/message.cpp"

  # Without the stub override, Forge injects the pinned dependency.
  python3 - "$root/forge.lua" <<'PY'
import pathlib, re, sys
path = pathlib.Path(sys.argv[1])
path.write_text(re.sub(r"    direct = \{[^\n]*\},\n", "    direct = {},\n", path.read_text()))
PY
  rm -rf "$root/build"
  local real_log="$root/real.log"
  (cd "$root" && exec "${FORGE_CMD[@]}" hot) >"$real_log" 2>&1 &
  hot_pid=$!
  started=0
  for _ in $(seq 1 2400); do
    grep -q READY "$real_log" 2>/dev/null && { started=1; break; }
    sleep 0.2
  done
  if [[ "$started" -eq 1 ]]; then
    pass "the pinned engine builds and starts the app"
  else
    fail "the pinned engine builds and starts the app ($(flatten <"$real_log" | tail -c 200))"
  fi

  sed_in_place 's/return "v1";/return "v2";/' "$root/src/message.cpp"
  local reloaded=0
  for _ in $(seq 1 900); do
    grep -q '| v2 |' "$real_log" 2>/dev/null && { reloaded=1; break; }
    sleep 0.1
  done
  if [[ "$reloaded" -eq 1 ]]; then
    pass "the real engine reloads a save"
  else
    fail "the real engine reloads a save"
  fi
  if [[ "$(grep -c READY "$real_log")" -eq 1 ]]; then
    pass "the reload keeps the process (no restart)"
  else
    fail "the reload keeps the process (no restart)"
  fi

  kill "$hot_pid" 2>/dev/null || true
  pkill -x hotdemo 2>/dev/null || true
  wait "$hot_pid" 2>/dev/null || true
}
