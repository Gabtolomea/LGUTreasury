#!/usr/bin/env bash
#
# merge-branch.sh — safely merge one branch into another for LGUTreasury.
#
# Run this from Git Bash or WSL (on Windows). It never pushes automatically.
#
# Usage:
#   ./scripts/merge-branch.sh <source-branch> <target-branch> [options]
#
# Examples:
#   ./scripts/merge-branch.sh customfix main
#   ./scripts/merge-branch.sh customfix gab --test
#
# Options:
#   --test       Run the xUnit test suite on the target before and after merging.
#   --no-pull    Skip `git pull` on the target branch.
#   --dry-run    Show what would happen without merging.
#   -h|--help    Show this help.
#
set -euo pipefail

# ── helpers ────────────────────────────────────────────────────────────────
info()  { printf '\033[36m%s\033[0m\n' "$*"; }
ok()    { printf '\033[32m%s\033[0m\n' "$*"; }
warn()  { printf '\033[33m%s\033[0m\n' "$*"; }
err()   { printf '\033[31m%s\033[0m\n' "$*" >&2; }

usage() { sed -n '3,18p' "$0" | sed 's/^# \{0,1\}//'; exit "${1:-0}"; }

SOURCE=""; TARGET=""; RUN_TESTS=0; DO_PULL=1; DRY_RUN=0

while [ $# -gt 0 ]; do
  case "$1" in
    --test)     RUN_TESTS=1 ;;
    --no-pull)  DO_PULL=0 ;;
    --dry-run)  DRY_RUN=1 ;;
    -h|--help)  usage 0 ;;
    --*)        err "Unknown option: $1"; usage 1 ;;
    *)          if [ -z "$SOURCE" ]; then SOURCE="$1"; elif [ -z "$TARGET" ]; then TARGET="$1"; else err "Too many arguments: $1"; usage 1; fi ;;
  esac
  shift
done

[ -n "$SOURCE" ] && [ -n "$TARGET" ] || { err "Both <source-branch> and <target-branch> are required."; usage 1; }
[ "$SOURCE" != "$TARGET" ] || { err "Source and target branches must differ."; exit 1; }

# ── make sure we are inside the repo ──────────────────────────────────────
git rev-parse --is-inside-work-tree >/dev/null 2>&1 || { err "Not a git repository."; exit 1; }
REPO_ROOT="$(git rev-parse --show-toplevel)"
cd "$REPO_ROOT"
info "Repository: $REPO_ROOT"

run_tests() {
  local project="LGUTreasury.Tests/LGUTreasury.Tests.csproj"
  if [ ! -f "$project" ]; then
    warn "Test project not found on this branch ($project) — skipping tests."
    return 0
  fi
  info "Running tests (output redirected to bin2/ to avoid file locks)..."
  dotnet test "$project" -p:BaseOutputPath=bin2/ --nologo -v q
}

# ── 1. working tree must be clean ─────────────────────────────────────────
if [ "$DRY_RUN" -eq 0 ] && [ -n "$(git status --porcelain)" ]; then
  err "Working tree is not clean. Commit or stash your changes on '$SOURCE' first."
  git status --short
  exit 1
fi

# ── 2. .env must exist (credentials live there, not in appsettings) ───────
if [ ! -f ".env" ]; then
  if [ -f ".env.example" ]; then
    warn ".env not found — copying .env.example to .env. Fill in real credentials!"
    if [ "$DRY_RUN" -eq 0 ]; then cp ".env.example" ".env"; fi
  else
    warn ".env and .env.example both missing — the app will fail without a connection string."
  fi
fi

# ── 3. build artifacts must be untracked ──────────────────────────────────
if [ -n "$(git ls-files bin obj publish 2>/dev/null | head -n 1)" ]; then
  warn "Build artifacts are still tracked. Untracking them (keeps files on disk)..."
  if [ "$DRY_RUN" -eq 0 ]; then
    git rm -r --cached bin obj publish >/dev/null
    git add .gitignore 2>/dev/null || true
  fi
  warn "Review and commit the staged deletions before merging."
fi

# ── 4. verify both branches exist ─────────────────────────────────────────
git show-ref --verify --quiet "refs/heads/$SOURCE" || { err "Source branch '$SOURCE' does not exist locally."; exit 1; }
git show-ref --verify --quiet "refs/heads/$TARGET" || { err "Target branch '$TARGET' does not exist locally."; exit 1; }

# ── 5. fetch + switch to target ───────────────────────────────────────────
info "Fetching origin..."
git fetch --all --prune

CURRENT="$(git branch --show-current)"
info "Switching: $CURRENT -> $TARGET"
if [ "$DRY_RUN" -eq 0 ]; then git checkout "$TARGET"; fi

if [ "$DO_PULL" -eq 1 ] && [ "$DRY_RUN" -eq 0 ]; then
  info "Pulling latest '$TARGET' (fast-forward only)..."
  if ! git pull --ff-only; then
    err "Could not fast-forward '$TARGET'. Resolve divergence manually, then re-run."
    exit 1
  fi
fi

# ── 6. optional pre-merge tests ───────────────────────────────────────────
if [ "$RUN_TESTS" -eq 1 ] && [ "$DRY_RUN" -eq 0 ]; then
  info "Pre-merge tests on '$TARGET':"
  run_tests
fi

# ── 7. merge ──────────────────────────────────────────────────────────────
if [ "$DRY_RUN" -eq 1 ]; then
  info "[dry-run] git merge --no-ff $SOURCE"
  info "[dry-run] Would merge '$SOURCE' into '$TARGET'. No changes made."
  exit 0
fi

info "Merging '$SOURCE' into '$TARGET' (--no-ff)..."
if ! git merge --no-ff "$SOURCE" -m "Merge branch '$SOURCE' into '$TARGET'"; then
  err "Merge stopped due to conflicts. Conflicted files:"
  git diff --name-only --diff-filter=U | sed 's/^/  /'
  echo
  warn "If conflicts are only build artifacts (bin/obj/publish), accept the deletion:"
  warn "  git rm -r bin obj publish"
  warn "Then:  git add -A && git commit"
  warn "To back out completely:  git merge --abort"
  exit 1
fi

ok "Merge completed successfully."

# ── 8. optional post-merge tests ──────────────────────────────────────────
if [ "$RUN_TESTS" -eq 1 ]; then
  info "Post-merge tests on '$TARGET':"
  run_tests
fi

# ── 9. next steps ─────────────────────────────────────────────────────────
echo
ok "Done. Next steps:"
echo "  1. Review:   git log --oneline -5 && git status"
echo "  2. Push:     git push origin $TARGET"
echo "  3. Repeat for other branches:  ./scripts/merge-branch.sh $SOURCE <other-branch>"
echo
warn "Reminder: the DB password exists in older history — rotate it if it was ever shared."
