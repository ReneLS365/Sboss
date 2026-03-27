#!/usr/bin/env python3
import argparse
import re
import sys
from pathlib import Path

MASTER_TASK_RE = re.compile(r"^- \[(?P<done>[ xX])\]\s+(?P<step>\d+[A-Z])(?:\s+[—-])?\s+(?P<title>.+)$")
CURRENT_PHASE_RE = re.compile(r"^- Current phase:\s+\*\*Phase\s+(?P<phase>\d+)\s+[—-]\s+(?P<title>.+)\*\*$", re.MULTILINE)
CURRENT_TASK_RE = re.compile(r"^- Current task:\s+\*\*(?P<step>\d+[A-Z])\s+[—-]\s+(?P<title>.+)\*\*$", re.MULTILINE)
NEXT_TASK_RE = re.compile(r"^- Next task:\s+\*\*(?P<step>\d+[A-Z])\s+[—-]\s+(?P<title>.+)\*\*$", re.MULTILINE)
TASK_STEP_PHASE_RE = re.compile(r"^(?P<phase>\d+)[A-Z]$")


def extract_all_master_tasks(text: str):
    tasks = []
    for line in text.splitlines():
        match = MASTER_TASK_RE.match(line.strip())
        if match:
            tasks.append(
                {
                    "step": match.group("step").upper(),
                    "done": match.group("done").lower() == "x",
                    "title": match.group("title").strip(),
                }
            )
    return tasks


def fail(message: str):
    print(f"STATUS VALIDATION FAILED: {message}", file=sys.stderr)
    raise SystemExit(1)


def require(condition: bool, message: str):
    if not condition:
        fail(message)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--master-status-file", default="docs/MASTER_STATUS.md")
    args = parser.parse_args()

    master_text = Path(args.master_status_file).read_text(encoding="utf-8")

    current_phase_match = CURRENT_PHASE_RE.search(master_text)
    current_match = CURRENT_TASK_RE.search(master_text)
    next_match = NEXT_TASK_RE.search(master_text)
    require(current_phase_match is not None, "docs/MASTER_STATUS.md is missing a Current phase entry.")
    require(current_match is not None, "docs/MASTER_STATUS.md is missing a Current task entry.")
    require(next_match is not None, "docs/MASTER_STATUS.md is missing a Next task entry.")

    current_phase_number = int(current_phase_match.group("phase"))
    current_step = current_match.group("step").upper()
    next_step = next_match.group("step").upper()
    current_step_phase_match = TASK_STEP_PHASE_RE.match(current_step)
    require(current_step_phase_match is not None, f"Current task step '{current_step}' does not follow the expected phase/task format.")
    current_step_phase_number = int(current_step_phase_match.group("phase"))
    require(
        current_step_phase_number == current_phase_number,
        f"Current phase 'Phase {current_phase_number}' does not match current task step '{current_step}'.",
    )

    master_tasks = extract_all_master_tasks(master_text)
    roadmap_steps = [task["step"] for task in master_tasks]
    require(current_step in roadmap_steps, f"Current task step '{current_step}' is missing from docs/MASTER_STATUS.md.")

    current_index = roadmap_steps.index(current_step)
    expected_next = roadmap_steps[current_index + 1] if current_index + 1 < len(roadmap_steps) else current_step
    require(next_step == expected_next, "docs/MASTER_STATUS.md Next task skips ahead or reorders the roadmap.")

    print(f"Roadmap status validation passed for current task {current_step}.")


if __name__ == "__main__":
    main()
