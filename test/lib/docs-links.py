#!/usr/bin/env python3
"""Check that documentation links and anchors resolve.

Used by the end-to-end suite (scenario 78): a renamed heading or a moved page
should fail the suite, not a reader's click. Fenced code blocks are ignored, so
a C++ lambda or a shell snippet is not mistaken for a link.
"""

import pathlib
import re
import sys

fence = re.compile(r"```.*?```", re.S)
link = re.compile(r"\[[^\]]*\]\(([^)]+)\)")


def slug(heading: str) -> str:
    """GitHub's heading anchor: lower-case, punctuation dropped, spaces hyphens."""
    text = heading.lstrip("#").strip().lower()
    text = re.sub(r"[^a-z0-9 _-]", "", text)
    return text.replace(" ", "-")


def main() -> int:
    docs = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else ".") / "docs"
    if not docs.is_dir():
        print("1")
        print(f"no docs directory at {docs}")
        return 0

    anchors = {
        page.name: {slug(line) for line in page.read_text().splitlines() if line.startswith("#")}
        for page in docs.glob("*.md")
    }

    problems = []
    for page in sorted(docs.glob("*.md")):
        text = fence.sub("", page.read_text())
        for target in link.findall(text):
            if target.startswith(("http", "mailto")):
                continue

            path, _, anchor = target.partition("#")
            if not path:  # same-page anchor
                if anchor and anchor not in anchors[page.name]:
                    problems.append(f"{page.name} -> #{anchor}")
                continue

            if not (docs / path).exists():
                problems.append(f"{page.name} -> {path}")
            elif anchor and anchor not in anchors.get(path, set()):
                problems.append(f"{page.name} -> {path}#{anchor}")

    print(len(problems))
    for problem in problems:
        print(problem)
    return 0


if __name__ == "__main__":
    sys.exit(main())
