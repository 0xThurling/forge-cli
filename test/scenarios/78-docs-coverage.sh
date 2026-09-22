# The documentation stays in step with the CLI: every command is in the CLI
# reference, every docs page is in the nav, and the features added along the way
# are described somewhere.
scenario_78_docs_coverage() {
  local docs="$REPO/docs"
  local reference="$docs/cli-reference.md"

  # --- every command the CLI exposes is documented --------------------------
  local command missing=""
  while read -r command; do
    [[ -z "$command" ]] && continue
    grep -qF "\`$command\`" "$reference" || missing="$missing $command"
  # Only the Commands block: the usage line mentions `forge` itself.
  done < <(forge --help 2>&1 | sed 's/\x1b\[[0-9;]*m//g' |
    awk '/^Commands:/{block=1; next} block && /^$/{exit} block {print $1}')

  if [[ -z "$missing" ]]; then
    pass "every CLI command appears in the CLI reference"
  else
    fail "every CLI command appears in the CLI reference (missing:$missing)"
  fi

  # --- subcommands too ------------------------------------------------------
  local parent sub submissing=""
  for parent in project new workspace config; do
    while read -r sub; do
      [[ -z "$sub" ]] && continue
      grep -qF "$parent $sub" "$reference" || submissing="$submissing $parent-$sub"
    done < <(forge "$parent" --help 2>&1 | sed 's/\x1b\[[0-9;]*m//g' |
      awk '/^Commands:/{block=1; next} block && /^$/{exit} block {print $1}')
  done

  if [[ -z "$submissing" ]]; then
    pass "every subcommand appears in the CLI reference"
  else
    fail "every subcommand appears in the CLI reference (missing:$submissing)"
  fi

  # --- the nav and the pages agree -----------------------------------------
  local page pagemissing=""
  for page in "$docs"/*.md; do
    grep -qF "$(basename "$page")" "$REPO/mkdocs.yml" || pagemissing="$pagemissing $(basename "$page")"
  done
  if [[ -z "$pagemissing" ]]; then
    pass "every documentation page is in the nav"
  else
    fail "every documentation page is in the nav (missing:$pagemissing)"
  fi

  local entry entrymissing=""
  while read -r entry; do
    [[ -f "$docs/$entry" ]] || entrymissing="$entrymissing $entry"
  done < <(grep -oE '[a-z-]+\.md' "$REPO/mkdocs.yml" | sort -u)
  if [[ -z "$entrymissing" ]]; then
    pass "every nav entry has a page"
  else
    fail "every nav entry has a page (missing:$entrymissing)"
  fi

  # --- the features of recent work are described somewhere ------------------
  local feature
  for feature in \
    "forge.add_section" \
    "FORGE_BUILD_DEPENDENCY_TESTS" \
    "vcpkg_triplet" \
    "cxx_compiler" \
    "compiler_launcher" \
    "cmake_prefix_path" \
    "unity" \
    "pch" \
    "modules" \
    "catch2" \
    "doctest" \
    "forge publish" \
    "forge ci" \
    "forge workspace" \
    "forge vendor" \
    "forge outdated" \
    "forge format" \
    "forge lint" \
    "forge bench" \
    "forge completions" \
    "forge doctor --fix" \
    "forge.lock" \
    "pkg-config" \
    "CPACK_PACKAGE_CONTACT" \
    "HeaderFilterRegex"; do
    if grep -rqF "$feature" "$docs" 2>/dev/null; then
      pass "documented: $feature"
    else
      fail "documented: $feature"
    fi
  done
}
