"""Publish reviewed TA notes to the existing GitHub main, without staging unrelated files."""

import argparse
from datetime import datetime, timedelta, timezone
import os
from pathlib import Path
import re
import subprocess
import sys


SKILL = Path(__file__).resolve().parents[1]
REPO = SKILL.parents[2]
PREFIX = "嗯之宝库/skill/n的TA糕手/"
ORIGIN = "https://github.com/Neilyodoge/Gate-of-Babylon.git"
REMOTE_REF = "refs/heads/main"
SETUP_SUBJECT = "docs(ta): set up continuous learning"
SETUP_PATHS = {
    "嗯之宝库/skill/n的TA绝学/SKILL.md",
    *(PREFIX + path for path in (
        "SKILL.md",
        "references/paopu-crystal.md",
        "references/learning-workflow.md",
        "references/research-queue.md",
        "references/cases/texture-alpha-import.md",
        "scripts/publish-learning.py",
    )),
}
DAILY_SUBJECT = re.compile(r"docs\(ta\): publish research \d{4}-\d{2}-\d{2}")
DAILY_PATH = re.compile(r"references/(?:research-queue\.md|cases/[a-z0-9]+(?:-[a-z0-9]+)*\.md)")
GIT_ENV = {
    **{key: value for key, value in os.environ.items()
       if key.upper() not in {"GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "GIT_COMMON_DIR"}},
    "GIT_TERMINAL_PROMPT": "0",
    "GCM_INTERACTIVE": "never",
}


def git(*args):
    result = subprocess.run(
        ["git", "--literal-pathspecs", "-C", str(REPO), *args],
        capture_output=True, env=GIT_ENV, timeout=45,
    )
    if result.returncode:
        detail = result.stderr.decode("utf-8", errors="replace").strip()
        raise RuntimeError(f"git {args[0]}: {detail or f'exit {result.returncode}'}")
    return result.stdout.decode("utf-8", errors="strict")


def remote_head():
    line = git("ls-remote", "--exit-code", "origin", REMOTE_REF).strip()
    sha, tab, ref = line.partition("\t")
    if tab != "\t" or ref != REMOTE_REF or not re.fullmatch(r"[a-f0-9]{40}", sha):
        raise RuntimeError("Unexpected origin/main response.")
    return sha


def verify_outgoing(remote, head):
    # A pinned commit is pushed only if it contains the observed remote tip.
    try:
        git("merge-base", "--is-ancestor", remote, head)
    except RuntimeError as exc:
        raise RuntimeError("origin/main moved or is not in local history; kept local notes.") from exc
    commits = git("rev-list", "--reverse", f"{remote}..{head}").splitlines()
    for sha in commits:
        parents = git("show", "-s", "--format=%P", sha).split()
        subject = git("show", "-s", "--format=%s", sha).strip()
        paths = set(git("diff-tree", "--no-commit-id", "--name-only", "--no-renames", "-r", "-z", sha).strip("\0").split("\0"))
        daily = DAILY_SUBJECT.fullmatch(subject) and all(
            p.startswith(PREFIX) and DAILY_PATH.fullmatch(p[len(PREFIX):]) for p in paths
        )
        setup = subject == SETUP_SUBJECT and paths == SETUP_PATHS
        if len(parents) != 1 or not (daily or setup):
            raise RuntimeError(f"Unrelated outgoing commit {sha[:12]}; did not push.")
    return commits


def reviewed_paths(paths):
    files = []
    for path in dict.fromkeys(paths):
        if not DAILY_PATH.fullmatch(path):
            raise RuntimeError(f"Out of research scope: {path}")
        file = SKILL / path
        if not file.is_file() or file.is_symlink() or file.resolve() != file:
            raise RuntimeError(f"Expected an ordinary existing note: {path}")
        files.append(PREFIX + path)
    return files


def run(apply, paths):
    if SKILL.relative_to(REPO).as_posix() + "/" != PREFIX:
        raise RuntimeError("Run from the canonical TA skill in Gate-of-Babylon.")
    if git("symbolic-ref", "-q", "HEAD").strip() != REMOTE_REF:
        raise RuntimeError("The checkout must be on main.")
    for mode in ([], ["--push"]):
        if git("remote", "get-url", *mode, "--all", "origin").splitlines() != [ORIGIN]:
            raise RuntimeError("Unexpected origin URL; did not push.")
    local = git("rev-parse", "HEAD").strip()
    remote = remote_head()
    queued = verify_outgoing(remote, local)
    files = reviewed_paths(paths)
    if files:
        if git("diff", "--cached", "--name-only", "-z", "--", *files):
            raise RuntimeError("A reviewed file already has staged work; did not commit.")
        changed = git("status", "--porcelain=v1", "--no-renames", "-z", "--untracked-files=all", "--", *files)
        if apply and changed:
            # Intent-to-add lets commit --only include new notes while preserving other staged work.
            git("add", "-N", "--", *files)
            git("diff", "--check", "HEAD", "--", *files)
            if git("rev-parse", "HEAD").strip() != local:
                raise RuntimeError("HEAD changed during review; retry after examining the worktree.")
            date = datetime.now(timezone(timedelta(hours=8))).date().isoformat()
            git("commit", "--only", "-m", f"docs(ta): publish research {date}", "--", *files)
            local = git("rev-parse", "HEAD").strip()
            queued = verify_outgoing(remote, local)
        elif changed:
            print("reviewed_paths_with_pending_changes:", len(changed.strip("\0").split("\0")))
    if not queued:
        print("nothing_to_push")
        return
    flags = [] if apply else ["--dry-run"]
    git("push", "--porcelain", *flags, "origin", f"{local}:{REMOTE_REF}")
    if apply and remote_head() != local:
        raise RuntimeError("Push returned, but origin/main did not match the intended commit.")
    print(f"{'pushed' if apply else 'dry_run_ok'} {local} ({len(queued)} TA commits)")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true", help="commit reviewed files and push")
    parser.add_argument("paths", nargs="*", help="reviewed paths relative to the skill root")
    options = parser.parse_args()
    try:
        run(options.apply, options.paths)
    except (RuntimeError, subprocess.TimeoutExpired, OSError) as exc:
        print(f"TA sync stopped: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
