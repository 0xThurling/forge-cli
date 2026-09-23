# The project hooks that used to be empty scaffolding: .config/forge/templates
# for `forge new`, and .config/forge/commands for `forge run`.
scenario_66_project_hooks() {
  local root="$WORK/66-project-hooks"
  mkdir -p "$root/src" "$root/.config/forge/templates" "$root/.config/forge/commands"
  make_plain_project "$root" demo_hooks

  # --- templates -------------------------------------------------------------
  cat >"$root/.config/forge/templates/class.hpp" <<'TMPL'
#pragma once
// project template for {{NAME}}
namespace demo {
class {{name}} {
 public:
  int value() const;
};
}  // namespace demo
TMPL
  cat >"$root/.config/forge/templates/class.cpp" <<'TMPL'
#include "{{name}}.hpp"
// implemented from the project template
TMPL

  forge_in "$root" new class Widget >/dev/null 2>&1 || true
  assert_contains "$root/src/Widget.h" "// project template for WIDGET" "template rendered (upper)"
  assert_contains "$root/src/Widget.h" "class Widget {" "template rendered (name)"
  assert_contains "$root/src/Widget.cpp" "// implemented from the project template" \
    "template used for the source too"
  assert_lacks "$root/src/Widget.h" "#ifndef WIDGET_H" "the built-in scaffolding is replaced"

  # A kind without a template falls back to the built-in scaffolding.
  forge_in "$root" new header Plain >/dev/null 2>&1 || true
  assert_contains "$root/src/Plain.h" "#ifndef PLAIN_H" "kinds without a template use the default"

  # --- commands --------------------------------------------------------------
  cat >"$root/.config/forge/commands/hello.sh" <<'SH'
echo hello-from-project-command
SH
  cat >"$root/.config/forge/commands/check.sh" <<'SH'
exit 4
SH
  chmod +x "$root/.config/forge/commands"/*.sh

  local out flat
  out="$(forge_in "$root" project scripts 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "hello" <<<"$flat" && grep -qF ".config/forge/commands" <<<"$flat"; then
    pass "project commands are listed"
  else
    fail "project commands are listed"
  fi

  out="$(forge_in "$root" run hello 2>&1 || true)"
  if [[ "$out" == *hello-from-project-command* ]]; then
    pass "a project command runs"
  else
    fail "a project command runs"
  fi
  assert_exit 4 "a failing project command propagates its exit code" \
    forge_in "$root" run check

  # A forge.lua script of the same name wins.
  cat >"$root/forge.lua" <<'LUA'
return {
  project = { name = "demo_hooks", type = "executable", standard = "20" },
  dependencies = { direct = {}, conan = {} },
  resources = { files = {} },
  scripts = { hello = "echo from-forge-lua" },
  features = {}
}
LUA
  out="$(forge_in "$root" run hello 2>&1 || true)"
  if [[ "$out" == *from-forge-lua* && "$out" != *hello-from-project-command* ]]; then
    pass "forge.lua scripts take precedence"
  else
    fail "forge.lua scripts take precedence"
  fi

  # An unknown name reports both locations.
  out="$(forge_in "$root" run nope 2>&1 || true)"
  flat="$(flatten <<<"$out")"
  if grep -qF "not found in forge.lua or .config/forge/commands/" <<<"$flat"; then
    pass "the unknown name reports both locations"
  else
    fail "the unknown name reports both locations"
  fi
}
