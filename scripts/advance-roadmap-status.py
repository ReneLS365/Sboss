#!/usr/bin/env python3
import argparse
import re
import sys
from pathlib import Path

MASTER_TASK_RE = re.compile(r"^- \[(?P<done>[ xX])\]\s+(?P<step>\d+[A-Z])(?:\s+[—-])?\s+(?P<title>.+)$")
CURRENT_TASK_RE = re.compile(r"^- Current task:\s+\*\*(?P<step>\d+[A-Z])\s+[—-]\s+(?P<title>.+)\*\*$", re.MULTILINE)
NEXT_TASK_RE = re.compile(r"^- Next task:\s+\*\*(?P<step>\d+[A-Z])\s+[—-]\s+(?P<title>.+)\*\*$", re.MULTILINE)


def fail(message: str):
    print(f"ROADMAP ADVANCEMENT FAILED: {message}", file=sys.stderr)
    raise SystemExit(1)


def require(condition: bool, message: str):
    if not condition:
        fail(message)


def parse_master_tasks(master_text: str):
    tasks = []
    for line in master_text.splitlines():
        match = MASTER_TASK_RE.match(line.strip())
        if match:
            tasks.append(
                {
                    "step": match.group("step").upper(),
                    "title": match.group("title").strip(),
                }
            )
    return tasks


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--master-status-file", default="docs/MASTER_STATUS.md")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    master_path = Path(args.master_status_file)
    master_text = master_path.read_text(encoding="utf-8")

    current_match = CURRENT_TASK_RE.search(master_text)
    next_match = NEXT_TASK_RE.search(master_text)
    require(current_match is not None, "docs/MASTER_STATUS.md is missing Current task entry.")
    require(next_match is not None, "docs/MASTER_STATUS.md is missing Next task entry.")

    current_step = current_match.group("step").upper()
    next_step = next_match.group("step").upper()

    tasks = parse_master_tasks(master_text)
    steps = [task["step"] for task in tasks]

    require(current_step in steps, f"Current task step '{current_step}' is missing from docs/MASTER_STATUS.md.")
    current_index = steps.index(current_step)
    require(current_index + 1 < len(steps), f"Current task step '{current_step}' has no successor in docs/MASTER_STATUS.md.")

    expected_next_step = steps[current_index + 1]
    require(
        next_step == expected_next_step,
        "docs/MASTER_STATUS.md Next task skips ahead or reorders the roadmap.",
    )

    next_index = current_index + 1
    new_current = tasks[next_index]
    new_next = tasks[next_index + 1] if next_index + 1 < len(tasks) else tasks[next_index]

    updated = master_text
    updated, current_count = CURRENT_TASK_RE.subn(
        f"- Current task: **{new_current['step']} — {new_current['title']}**",
        updated,
        count=1,
    )
    require(current_count == 1, "Unable to update Current task in docs/MASTER_STATUS.md.")

    updated, next_count = NEXT_TASK_RE.subn(
        f"- Next task: **{new_next['step']} — {new_next['title']}**",
        updated,
        count=1,
    )
    require(next_count == 1, "Unable to update Next task in docs/MASTER_STATUS.md.")

    updated, mark_count = re.subn(
        rf"^- \[ \]\s+{re.escape(current_step)}\s+(.+)$",
        rf"- [x] {current_step} \1",
        updated,
        count=1,
        flags=re.MULTILINE,
    )
    require(mark_count == 1, f"Unable to mark step '{current_step}' complete in docs/MASTER_STATUS.md.")

    if args.dry_run:
        print(f"Roadmap advancement dry-run passed: {current_step} -> {new_current['step']}.")
        return

    master_path.write_text(updated, encoding="utf-8")
    print(f"Roadmap advanced in docs/MASTER_STATUS.md: {current_step} -> {new_current['step']}.")


if __name__ == "__main__":
    main()
