# `forge new class/struct/header/source`: the generated files' contents.
scenario_51_new_contents() {
  local root="$WORK/51-new-contents"
  mkdir -p "$root/src"
  make_plain_project "$root" demo_new

  forge_in "$root" new class Widget >/dev/null 2>&1 || true
  assert_contains "$root/src/Widget.h" "#ifndef WIDGET_H" "class header guard"
  assert_contains "$root/src/Widget.h" "class Widget {" "class declared"
  assert_contains "$root/src/Widget.h" "Widget();" "constructor declared"
  assert_contains "$root/src/Widget.h" "~Widget();" "destructor declared"
  assert_contains "$root/src/Widget.cpp" '#include "Widget.h"' "class source includes its header"
  assert_contains "$root/src/Widget.cpp" "Widget::Widget()" "constructor defined"

  forge_in "$root" new struct Point >/dev/null 2>&1 || true
  assert_contains "$root/src/Point.h" "#ifndef POINT_H" "struct header guard"
  assert_contains "$root/src/Point.h" "struct Point {" "struct declared"

  forge_in "$root" new header Only >/dev/null 2>&1 || true
  assert_contains "$root/src/Only.h" "#ifndef ONLY_H" "header guard"
  assert_contains "$root/src/Only.h" "// Your code here" "header placeholder"

  forge_in "$root" new source helper >/dev/null 2>&1 || true
  assert_contains "$root/src/helper.h" "#ifndef HELPER_H" "source pair header guard"
  assert_contains "$root/src/helper.cpp" '#include "helper.h"' "source pair includes its header"

  # Everything the generator wrote still compiles.
  if forge_in "$root" build >/dev/null 2>&1; then
    pass "the generated files build"
  else
    fail "the generated files build"
  fi
}
