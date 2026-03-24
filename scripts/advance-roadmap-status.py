#!/usr/bin/env python3
import argparse
import re
import sys
from dataclasses import dataclass
from pathlib import Path

TASK_HEADER_RE = re.compile(r"^## Task Record — (?P<header>.+)$", re.MULTILINE)
FIELD_RE = re.compile(r"^- \*\*(?P<name>[^:]+):\*\*\s*(?P<value>.*)$", re.MULTILINE)
MASTER_TASK_RE = re.compile(r"^- \[(?P<done>[ xX])\]\s+(?P<step>\d+[A-Z])(?:\s+[—-])?\s+(?P<title>.+)$")
CURRENT_TASK_RE = re.compile(r"^- Current task:\s+\*\*(?P<step>\d+[A-Z])\s+[—-]\s+(?P<title>.+)\*\*$", re.MULTILINE)
NEXT_TASK_RE = re.compile(r"^- Next task:\s+\*\*(?P<step>\d+[A-Z])\s+[—-]\s+(?P<title>.+)\*\*$", re.MULTILINE)
CURRENT_PHASE_RE = re.compile(r"^- Current phase:\s+\*\*Phase\s+(?P<phase>\d+)\s+[—-]\s+(?P<title>.+)\*\*$", re.MULTILINE)
COMPLETED_PHASE_RE = re.compile(r"^- Completed phase:\s+\*\*Phase\s+(?P<phase>\d+)\s+[—-]\s+(?P<title>.+)\*\*$", re.MULTILINE)
PHASE_HEADER_RE = re.compile(r"^## Phase\s+(?P<phase>\d+)\s+[—-]\s+(?P<title>.+)$", re.MULTILINE)
PLANS_CURRENT_PHASE_RE = re.compile(r"^- \*\*Current_phase:\*\*\s*(?P<phase>\d+)\s*\((?P<title>.+)\)\s*$", re.MULTILINE)
READ_ME_CURRENT_RE = re.compile(r"^- \*\*Current task:\s*(?P<step>\d+[A-Z])\s+[—-]\s+(?P<title>.+)\*\*$", re.MULTILINE)
READ_ME_NEXT_RE = re.compile(r"^- \*\*Next task:\s*(?P<step>\d+[A-Z])\s+[—-]\s+(?P<title>.+)\*\*$", re.MULTILINE)
ACTIVE_TASK_ID_RE = re.compile(r'private const string ActiveTaskId = "(?P<task>[^"]+)";')


@dataclass
class PlanTask:
    task_id: str
    title: str
    status: str
    pr: str
    block_start: int
    block_end: int


def fail(message: str):
    print(f"ROADMAP ADVANCEMENT FAILED: {message}", file=sys.stderr)
    raise SystemExit(1)


def require(condition: bool, message: str):
    if not condition:
        fail(message)


def parse_plan_tasks(plans_text: str):
    matches = list(TASK_HEADER_RE.finditer(plans_text))
    tasks: list[PlanTask] = []
    for index, match in enumerate(matches):
        start = match.start()
        end = matches[index + 1].start() if index + 1 < len(matches) else len(plans_text)
        block = plans_text[start:end]
        fields = {field.group('name').strip(): field.group('value').strip() for field in FIELD_RE.finditer(block)}
        task_id = fields.get('Task ID', '')
        title = fields.get('Title', '')
        status = fields.get('Status', '')
        pr = fields.get('PR', '')
        if task_id and title and status:
            tasks.append(PlanTask(task_id=task_id, title=title, status=status, pr=pr, block_start=start, block_end=end))
    return tasks


def parse_master_tasks(master_text: str):
    tasks = []
    for line in master_text.splitlines():
        match = MASTER_TASK_RE.match(line.strip())
        if match:
            tasks.append({
                'step': match.group('step').upper(),
                'title': match.group('title').strip(),
            })
    return tasks


def parse_phase_title(master_text: str, phase_number: int):
    phase_match = PHASE_HEADER_RE.search(master_text, pos=0)
    while phase_match is not None:
        if int(phase_match.group('phase')) == phase_number:
            return phase_match.group('title').strip()
        phase_match = PHASE_HEADER_RE.search(master_text, phase_match.end())
    return None


def normalize_task_id(step: str, title: str):
    cleaned = title.upper().replace('+', ' AND ').replace('/', ' ')
    cleaned = re.sub(r'[^A-Z0-9]+', '-', cleaned)
    cleaned = re.sub(r'-+', '-', cleaned).strip('-')
    return f"P{step}-{cleaned}"


def split_step(step: str):
    match = re.fullmatch(r'(?P<phase>\d+)(?P<letter>[A-Z])', step)
    if not match:
        fail(f"Invalid roadmap step format '{step}'.")
    return int(match.group('phase')), match.group('letter')


def update_plan_block(block: str, status: str | None = None, pr: str | None = None):
    updated = block
    if status is not None:
        updated, count = re.subn(r"^- \*\*Status:\*\*\s*.*$", f"- **Status:** {status}", updated, count=1, flags=re.MULTILINE)
        require(count == 1, "Unable to update Status in a PLANS task record.")
    if pr is not None:
        updated, count = re.subn(r"^- \*\*PR:\*\*\s*.*$", f"- **PR:** {pr}", updated, count=1, flags=re.MULTILINE)
        require(count == 1, "Unable to update PR in a PLANS task record.")
    return updated


def build_task_record(step: str, title: str, phase_title: str):
    task_id = normalize_task_id(step, title)
    today = "2026-03-24"
    return f"""
## Task Record — {task_id}
- **Task ID:** {task_id}
- **Title:** Phase {step} {title.lower()}
- **Phase:** Phase {split_step(step)[0]} — {phase_title}
- **Status:** IN_PROGRESS
- **Branch:** work
- **PR:** Draft PR pending
- **Scope:**
  - Implement roadmap-scoped changes for {step} — {title}.
- **Allowed files:**
  - Scoped by the active task prompt and roadmap governance.
- **Non-goals:**
  - No work outside scoped roadmap boundaries.
- **Acceptance criteria:**
  - Task deliverables satisfy roadmap definition for {step}.
- **Blockers:** None recorded.
- **Last updated:** {today}
""".strip("\n") + "\n\n---\n\n"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--plans-file', default='PLANS.md')
    parser.add_argument('--master-status-file', default='docs/MASTER_STATUS.md')
    parser.add_argument('--readme-file', default='README.md')
    parser.add_argument('--guardrail-test-file', default='src/backend/tests/Sboss.Api.Tests/RoadmapStatusGuardrailTests.cs')
    parser.add_argument('--expected-current-task', required=True)
    parser.add_argument('--next-task', required=True)
    parser.add_argument('--merged-pr', required=True)
    parser.add_argument('--dry-run', action='store_true')
    args = parser.parse_args()

    require(re.fullmatch(r'#\d+', args.merged_pr) is not None, "Merged PR reference must be provided as '#<number>'.")

    plans_path = Path(args.plans_file)
    master_path = Path(args.master_status_file)
    readme_path = Path(args.readme_file)
    guardrail_path = Path(args.guardrail_test_file)

    plans_text = plans_path.read_text(encoding='utf-8')
    master_text = master_path.read_text(encoding='utf-8')
    readme_text = readme_path.read_text(encoding='utf-8')
    guardrail_text = guardrail_path.read_text(encoding='utf-8')

    plan_tasks = parse_plan_tasks(plans_text)
    require(plan_tasks, 'No task records found in PLANS.md.')
    in_progress = [t for t in plan_tasks if t.status == 'IN_PROGRESS']
    require(len(in_progress) == 1, 'Exactly one task must be marked IN_PROGRESS in PLANS.md.')
    active_task = in_progress[0]
    require(active_task.task_id == args.expected_current_task,
            f"Expected current task '{args.expected_current_task}', but PLANS.md has '{active_task.task_id}' IN_PROGRESS.")

    current_match = CURRENT_TASK_RE.search(master_text)
    next_match = NEXT_TASK_RE.search(master_text)
    require(current_match is not None, 'docs/MASTER_STATUS.md is missing Current task entry.')
    require(next_match is not None, 'docs/MASTER_STATUS.md is missing Next task entry.')

    current_step = current_match.group('step').upper()
    current_title = current_match.group('title').strip()
    require(active_task.task_id == normalize_task_id(current_step, current_title),
            'IN_PROGRESS task in PLANS.md does not match docs/MASTER_STATUS.md current task.')

    all_master_tasks = parse_master_tasks(master_text)
    steps = [t['step'] for t in all_master_tasks]
    require(current_step in steps, f"Current step '{current_step}' was not found in docs/MASTER_STATUS.md task checklists.")
    current_index = steps.index(current_step)
    require(current_index + 1 < len(steps), f"Current step '{current_step}' has no successor in roadmap.")

    expected_next_step = steps[current_index + 1]
    expected_next_title = all_master_tasks[current_index + 1]['title']
    require(args.next_task.upper() == expected_next_step,
            f"Requested next task '{args.next_task}' is not direct roadmap successor '{expected_next_step}'.")
    require(next_match.group('step').upper() == expected_next_step,
            'docs/MASTER_STATUS.md Next task does not match direct roadmap successor.')

    # Update PLANS: close active task
    updated_plans = plans_text
    active_block = plans_text[active_task.block_start:active_task.block_end]
    closed_block = update_plan_block(active_block, status='DONE', pr=f"{args.merged_pr} (merged)")
    updated_plans = updated_plans[:active_task.block_start] + closed_block + updated_plans[active_task.block_end:]

    # Re-parse after replacement
    updated_tasks = parse_plan_tasks(updated_plans)
    next_task_id = normalize_task_id(expected_next_step, expected_next_title)
    next_task = next((t for t in updated_tasks if t.task_id == next_task_id), None)

    if next_task is not None:
        next_block = updated_plans[next_task.block_start:next_task.block_end]
        next_updated = update_plan_block(next_block, status='IN_PROGRESS', pr='Draft PR pending')
        updated_plans = updated_plans[:next_task.block_start] + next_updated + updated_plans[next_task.block_end:]
    else:
        phase_number, _ = split_step(expected_next_step)
        phase_title_match = re.search(rf"^## Phase {phase_number} [—-] (?P<title>.+)$", master_text, re.MULTILINE)
        require(phase_title_match is not None, f"Unable to find title for Phase {phase_number} in docs/MASTER_STATUS.md.")
        new_record = build_task_record(expected_next_step, expected_next_title, phase_title_match.group('title').strip())
        anchor = re.search(r"\n---\n\s*\n", updated_plans)
        if anchor:
            insert_at = anchor.end()
            updated_plans = updated_plans[:insert_at] + "\n" + new_record + updated_plans[insert_at:]
        else:
            updated_plans = updated_plans.rstrip() + "\n\n---\n\n" + new_record

    # Update MASTER_STATUS header
    updated_master = master_text
    updated_master, c1 = CURRENT_TASK_RE.subn(f"- Current task: **{expected_next_step} — {expected_next_title}**", updated_master, count=1)
    require(c1 == 1, 'Unable to update Current task in docs/MASTER_STATUS.md.')

    if current_index + 2 < len(steps):
        new_next_step = steps[current_index + 2]
        new_next_title = all_master_tasks[current_index + 2]['title']
    else:
        new_next_step = expected_next_step
        new_next_title = expected_next_title

    updated_master, c2 = NEXT_TASK_RE.subn(f"- Next task: **{new_next_step} — {new_next_title}**", updated_master, count=1)
    require(c2 == 1, 'Unable to update Next task in docs/MASTER_STATUS.md.')

    # Mark checklist completion/in-progress states
    updated_master, c3 = re.subn(rf"^- \[ \]\s+{re.escape(current_step)}\s+(.+)$", rf"- [x] {current_step} \1", updated_master, count=1, flags=re.MULTILINE)
    require(c3 == 1, f"Unable to mark {current_step} as complete in docs/MASTER_STATUS.md.")
    updated_master, c4 = re.subn(rf"^- \[(?:x|X)\]\s+{re.escape(expected_next_step)}\s+(.+)$", rf"- [ ] {expected_next_step} \1", updated_master, count=1, flags=re.MULTILINE)
    if c4 == 0:
        updated_master, c4b = re.subn(rf"^- \[ \]\s+{re.escape(expected_next_step)}\s+(.+)$", rf"- [ ] {expected_next_step} \1", updated_master, count=1, flags=re.MULTILINE)
        require(c4b == 1, f"Unable to keep {expected_next_step} as active in docs/MASTER_STATUS.md.")

    current_phase_num = int(CURRENT_PHASE_RE.search(master_text).group('phase'))
    next_phase_num, _ = split_step(expected_next_step)
    if next_phase_num != current_phase_num:
        next_phase_title = parse_phase_title(updated_master, next_phase_num)
        require(next_phase_title is not None, f"Unable to locate next phase header for Phase {next_phase_num}.")
        updated_master, _ = CURRENT_PHASE_RE.subn(
            f"- Current phase: **Phase {next_phase_num} — {next_phase_title}**",
            updated_master,
            count=1,
        )

        current_phase_title = parse_phase_title(updated_master, current_phase_num)
        require(current_phase_title is not None, f"Unable to locate completed phase header for Phase {current_phase_num}.")
        updated_master, _ = COMPLETED_PHASE_RE.subn(
            f"- Completed phase: **Phase {current_phase_num} — {current_phase_title}**",
            updated_master,
            count=1,
        )

        updated_master, _ = re.subn(rf"^- \[ \]\s+Phase {current_phase_num}(\s+[—-]\s+.+)$", rf"- [x] Phase {current_phase_num}\1", updated_master, count=1, flags=re.MULTILINE)

        plans_current_phase = PLANS_CURRENT_PHASE_RE.search(updated_plans)
        require(plans_current_phase is not None, "PLANS.md is missing '**Current_phase:**' metadata.")
        updated_plans, p1 = PLANS_CURRENT_PHASE_RE.subn(
            f"- **Current_phase:** {next_phase_num} ({next_phase_title})",
            updated_plans,
            count=1,
        )
        require(p1 == 1, "Unable to update PLANS.md current-phase metadata for cross-phase advancement.")

    # Update README snapshot lines
    updated_readme = readme_text
    updated_readme, r1 = READ_ME_CURRENT_RE.subn(f"- **Current task: {expected_next_step} — {expected_next_title}**", updated_readme, count=1)
    require(r1 == 1, 'Unable to update Current task in README.md.')
    updated_readme, r2 = READ_ME_NEXT_RE.subn(f"- **Next task: {new_next_step} — {new_next_title}**", updated_readme, count=1)
    require(r2 == 1, 'Unable to update Next task in README.md.')

    # Update guardrail fixture active id
    updated_guardrail = guardrail_text
    updated_guardrail, g1 = ACTIVE_TASK_ID_RE.subn(f'private const string ActiveTaskId = "{next_task_id}";', updated_guardrail, count=1)
    require(g1 == 1, 'Unable to update ActiveTaskId in RoadmapStatusGuardrailTests.cs.')

    if args.dry_run:
        print(f"Roadmap advancement check passed: {active_task.task_id} -> {next_task_id} ({args.merged_pr}).")
        return

    plans_path.write_text(updated_plans, encoding='utf-8')
    master_path.write_text(updated_master, encoding='utf-8')
    readme_path.write_text(updated_readme, encoding='utf-8')
    guardrail_path.write_text(updated_guardrail, encoding='utf-8')

    print(f"Roadmap advanced: {active_task.task_id} -> {next_task_id} using merged PR {args.merged_pr}.")


if __name__ == '__main__':
    main()
