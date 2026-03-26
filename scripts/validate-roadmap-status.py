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
STEP_FROM_TITLE_RE = re.compile(r"Phase\s+(?P<step>\d+[A-Z])\b", re.IGNORECASE)
STEP_FROM_ID_RE = re.compile(r"P(?P<step>\d+[A-Z])\b", re.IGNORECASE)


@dataclass
class PlanTask:
    task_id: str
    title: str
    status: str
    pr: str
    step: str


def extract_plan_tasks(text: str):
    matches = list(TASK_HEADER_RE.finditer(text))
    tasks = []
    for index, match in enumerate(matches):
        start = match.start()
        end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
        block = text[start:end]
        fields = {field.group('name').strip(): field.group('value').strip() for field in FIELD_RE.finditer(block)}
        task_id = fields.get('Task ID')
        title = fields.get('Title')
        status = fields.get('Status')
        pr = fields.get('PR', '')
        if not task_id or not title or not status:
            continue
        step = derive_step(task_id, title)
        if step is None:
            continue
        tasks.append(PlanTask(task_id=task_id, title=title, status=status, pr=pr, step=step))
    return tasks


def derive_step(task_id: str, title: str):
    for pattern, value in ((STEP_FROM_TITLE_RE, title), (STEP_FROM_ID_RE, task_id)):
        match = pattern.search(value)
        if match:
            return match.group('step').upper()
    return None


def derive_step_from_task_id(task_id: str):
    match = STEP_FROM_ID_RE.search(task_id or "")
    if match:
        return match.group('step').upper()
    return None


def extract_master_phase_tasks(text: str):
    current_phase_match = CURRENT_PHASE_RE.search(text)
    if not current_phase_match:
        raise ValueError("Unable to locate the current phase entry in docs/MASTER_STATUS.md.")

    phase_number = current_phase_match.group('phase')
    phase_pattern = rf"## Phase {phase_number} — [^\n]+\n(?P<body>.*?)(?:\n## Phase \d+|\Z)"
    phase_match = re.search(phase_pattern, text, re.S)
    if not phase_match:
        raise ValueError(f"Unable to locate Phase {phase_number} task list in docs/MASTER_STATUS.md.")
    phase_body = phase_match.group('body')
    tasks = []
    for line in phase_body.splitlines():
        match = MASTER_TASK_RE.match(line.strip())
        if match:
            tasks.append({
                'step': match.group('step').upper(),
                'done': match.group('done').lower() == 'x',
                'title': match.group('title').strip(),
            })
    return tasks


def extract_all_master_tasks(text: str):
    tasks = []
    for line in text.splitlines():
        match = MASTER_TASK_RE.match(line.strip())
        if match:
            tasks.append({
                'step': match.group('step').upper(),
                'done': match.group('done').lower() == 'x',
                'title': match.group('title').strip(),
            })
    return tasks


def fail(message: str):
    print(f"STATUS VALIDATION FAILED: {message}", file=sys.stderr)
    raise SystemExit(1)


def require(condition: bool, message: str):
    if not condition:
        fail(message)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--plans-file', default='PLANS.md')
    parser.add_argument('--master-status-file', default='docs/MASTER_STATUS.md')
    parser.add_argument('--task-id')
    args = parser.parse_args()

    plans_path = Path(args.plans_file)
    master_text = Path(args.master_status_file).read_text(encoding='utf-8')

    current_match = CURRENT_TASK_RE.search(master_text)
    next_match = NEXT_TASK_RE.search(master_text)
    require(current_match is not None, 'docs/MASTER_STATUS.md is missing a Current task entry.')
    require(next_match is not None, 'docs/MASTER_STATUS.md is missing a Next task entry.')

    current_step = current_match.group('step').upper()
    next_step = next_match.group('step').upper()

    plan_tasks = []
    active_task = None
    if plans_path.exists():
        plans_text = plans_path.read_text(encoding='utf-8')
        plan_tasks = extract_plan_tasks(plans_text)
        require(plan_tasks, 'No task records were found in PLANS.md.')

        in_progress = [task for task in plan_tasks if task.status == 'IN_PROGRESS']
        require(len(in_progress) <= 1, 'More than one task is marked IN_PROGRESS in PLANS.md.')

        draft_done = [task for task in plan_tasks if task.status == 'DONE' and 'draft pr' in task.pr.lower()]

        if args.task_id:
            active_task = next((task for task in plan_tasks if task.task_id == args.task_id), None)
            if active_task is not None and active_task.step != current_step:
                active_task = None
        elif in_progress and in_progress[0].step == current_step:
            active_task = in_progress[0]
        elif len(draft_done) == 1:
            active_task = draft_done[0]

    if active_task is None and args.task_id:
        step = derive_step_from_task_id(args.task_id)
        if step == current_step:
            active_task = PlanTask(task_id=args.task_id, title=args.task_id, status='IN_PROGRESS', pr='', step=step)

    if active_task is None:
        active_task = PlanTask(
            task_id=f"P{current_step}-INFERRED-FROM-MASTER-STATUS",
            title='Inferred from docs/MASTER_STATUS.md',
            status='IN_PROGRESS',
            pr='',
            step=current_step,
        )

    master_tasks = extract_master_phase_tasks(master_text)
    roadmap_steps = [task['step'] for task in master_tasks]
    require(current_step in roadmap_steps, f"Current task step '{current_step}' is missing from docs/MASTER_STATUS.md.")

    active_index = roadmap_steps.index(current_step)
    prior_steps = set(roadmap_steps[:active_index])

    if plan_tasks:
        for task in plan_tasks:
            if task.status == 'IN_PROGRESS' and task.step in prior_steps:
                fail(f"Merged prior task '{task.step}' is still marked IN_PROGRESS in PLANS.md.")

        for task in plan_tasks:
            if task.status == 'DONE':
                has_merged_pr = bool(re.search(r"#\d+", task.pr))
                has_draft_pr = task.task_id == active_task.task_id and 'draft pr' in task.pr.lower()
                require(has_merged_pr or has_draft_pr, f"Task '{task.task_id}' is marked DONE without a required PR reference.")

    if active_index + 1 < len(roadmap_steps):
        expected_next = roadmap_steps[active_index + 1]
    else:
        all_master_tasks = extract_all_master_tasks(master_text)
        all_steps = [task['step'] for task in all_master_tasks]
        all_index = all_steps.index(active_task.step)
        expected_next = all_steps[all_index + 1] if all_index + 1 < len(all_steps) else active_task.step
    require(next_step == expected_next, 'docs/MASTER_STATUS.md Next task skips ahead or reorders the roadmap.')

    if plan_tasks:
        for task in master_tasks:
            matching_plan = next((plan_task for plan_task in plan_tasks if plan_task.step == task['step']), None)
            if matching_plan is None:
                continue
            if matching_plan.status == 'DONE':
                require(task['done'], f"docs/MASTER_STATUS.md does not mark '{task['step']}' complete even though PLANS.md says DONE.")
            if task['step'] in prior_steps:
                require(matching_plan.status == 'DONE', f"Prior roadmap step '{task['step']}' is not recorded DONE in PLANS.md.")

    print(f"Roadmap status validation passed for active task {active_task.task_id} ({active_task.step}).")

if __name__ == '__main__':
    main()
