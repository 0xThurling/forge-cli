#!/usr/bin/env python3
"""Check that every CLI option is mentioned in the CLI reference.

Used by the end-to-end suite (scenario 78): a new flag should fail the suite
until it is documented, rather than being discovered by a reader.
"""

import pathlib
import re
import subprocess
import sys


def clean(text: str) -> str:
    return re.sub(r"\x1b\[[0-9;]*m", "", text)


def help_of(args: list[str]) -> str:
    return clean(subprocess.run(["forge"] + args + ["--help"], capture_output=True, text=True).stdout)


def main() -> int:
    repo = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else ".")
    reference = (repo / "docs" / "cli-reference.md").read_text()

    commands = re.findall(r"^  ([a-z]+)\s{2,}", help_of([]), re.M)
    missing = []
    for command in commands:
        text = help_of([command])
        for option in sorted(set(re.findall(r"--([a-z0-9-]+)", text))):
            if option in ("help", "version"):
                continue
            if f"--{option}" not in reference:
                missing.append(f"{command} --{option}")

    print(len(missing))
    for item in missing:
        print(item)
    return 0


if __name__ == "__main__":
    sys.exit(main())
