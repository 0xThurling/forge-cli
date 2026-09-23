# Every Spectre markup string in the CLI is balanced. An unclosed tag throws
# while the console refreshes — inside a progress display that can abort a
# download and leave a truncated file — so it is worth checking statically.
scenario_89_markup_balance() {
  if ! command -v python3 >/dev/null 2>&1; then
    skip "python3 is not installed"
    return
  fi

  local out
  out="$(python3 - "$REPO" <<'PY'
import pathlib, re, sys

root = pathlib.Path(sys.argv[1])
# Only style-ish tags count: `[main]`, `[requires]`, `[A-Za-z_]` and array
# literals in generated YAML/JSON are not markup.
styles = {
    "bold", "dim", "italic", "underline", "strikethrough", "invert",
    "slowblink", "rapidblink", "conceal", "default", "black", "red", "green",
    "yellow", "blue", "magenta", "cyan", "white", "grey", "gray",
    "link", "slow", "rapid", "on", "not", "no",
}
tag = re.compile(r"\[(/?)([a-zA-Z#][a-zA-Z0-9 =_#-]*)?\]")

def is_style(content):
    words = content.replace("#", " ").split()
    return bool(words) and all(word.lower() in styles for word in words)

offenders = []
for path in sorted(root.glob("Commands/**/*.cs")):
    statement = ""
    start = 0
    for number, line in enumerate(path.read_text().splitlines(), 1):
        code = line.split("//", 1)[0]
        if not statement:
            start = number
        statement += " " + code

        # A markup string may be concatenated across lines: keep collecting
        # until the expression is complete, then check it as a whole.
        if code.rstrip().endswith("+"):
            continue

        depth = 0
        for close, content in tag.findall(statement):
            if close:
                depth -= 1
                continue
            # `[]` is an empty array, not a tag; `[main]` is not a style.
            if not content or not is_style(content):
                continue
            depth += 1

        if depth != 0:
            offenders.append(f"{path.relative_to(root)}:{start} (depth {depth})")
        statement = ""

print(len(offenders))
for offender in offenders:
    print(offender)
PY
)" || true

  local count
  count="$(head -1 <<<"$out")"
  if [[ "$count" == "0" ]]; then
    pass "every markup string is balanced"
  else
    fail "unbalanced markup strings: $(flatten <<<"$(tail -n +2 <<<"$out")")"
  fi
}
