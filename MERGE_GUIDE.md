# Merge Guide — LGUTreasury

How to merge a feature branch (e.g. `customfix`) into other branches (`main`, `gab`,
`demo`, `alleksu`, ...) without breaking the build or re-committing files that should
never be tracked.

There are two ways: the **script** (`scripts/merge-branch.sh`) or the **manual steps**.
Use Git Bash or WSL on Windows to run the script.

---

## 0. One-time setup (already done in this change set)

- Credentials live in `.env` (git-ignored). `.env.example` is committed for others.
- `appsettings.json` no longer contains the DB password.
- `.gitignore` ignores `bin/`, `obj/`, `publish/`, `bin2/`, test output and `.env`.
- Build artifacts were untracked with `git rm -r --cached bin obj publish`.

**These only take effect on a branch once the commits are on that branch.** The merge
carries them over; see conflict handling below if a target branch also touched those files.

> ⚠️ The old DB password is still in **git history**. Rotate it. To purge it from
> history (destructive, rewrites SHAs, needs force-push):
> ```bash
> git filter-repo --path bin --path obj --path publish --invert-paths
> # or BFG; then: git push --force origin <branch>
> ```
> Only do this if you can coordinate with everyone who cloned the repo.

---

## 1. Using the script (recommended)

```bash
# from the repo root, in Git Bash / WSL
chmod +x scripts/merge-branch.sh

# merge customfix into main, running tests before and after
./scripts/merge-branch.sh customfix main --test

# merge customfix into another branch without pulling first
./scripts/merge-branch.sh customfix gab --no-pull

# see what it would do, without changing anything
./scripts/merge-branch.sh customfix main --dry-run
```

What the script does:

1. Requires a clean working tree.
2. Creates `.env` from `.env.example` if missing (then you fill in credentials).
3. Untracks `bin/obj/publish` if they are somehow still tracked.
4. Fetches and switches to the target branch, then `git pull --ff-only`.
5. Optionally runs the test suite on the target.
6. Merges the source with `--no-ff`.
7. On conflicts: lists them and stops (nothing is lost) with fix instructions.
8. Optionally runs tests again and prints push/next-step commands.

It **never pushes** — you push when you are satisfied.

---

## 2. Manual steps (PowerShell or Bash)

```bash
# 1) Make sure your work is committed on the feature branch
git checkout customfix
git add -A
git commit -m "feat(report): custom report, remove weekly, xlsx export, env/gitignore"

# 2) Verify tests pass
dotnet test LGUTreasury.Tests/LGUTreasury.Tests.csproj -p:BaseOutputPath=bin2/ --nologo

# 3) Update the target and merge
git checkout main
git pull --ff-only
git merge --no-ff customfix -m "Merge branch 'customfix' into 'main'"

# 4) Confirm and push
git log --oneline -5
git push origin main
```

PowerShell equivalents work the same; only `cp` becomes `Copy-Item`.

---

## 3. Resolving common conflicts

### a. Build artifacts (`bin/`, `obj/`, `publish/`)
If the target branch also changed these (very likely, since they used to be tracked),
Git reports conflicts/modify-delete. **Accept the deletion** — they are generated files now
covered by `.gitignore`:

```bash
git rm -r bin obj publish
git add -A
git commit            # completes the merge
```

### b. `.env` / credentials
Never commit `.env`. If a conflict involves `appsettings.json`, keep the version **without**
the password (`"DefaultConnection": ""`); the real value comes from `.env`.

### c. `.gitignore`
If both branches edited `.gitignore`, keep both sets of rules (union).

### d. Aborting
```bash
git merge --abort     # return to the pre-merge state on the target branch
```

---

## 4. After each merge — checklist

- [ ] `.env` exists locally with valid credentials.
- [ ] `dotnet restore` succeeds.
- [ ] `dotnet build` succeeds (stop the running app first; it locks `bin/…/LGUTreasury.dll`).
- [ ] `dotnet test ... -p:BaseOutputPath=bin2/` is green.
- [ ] `git status` shows no build artifacts staged.
- [ ] Only push after the above look correct.

---

## 5. Running tests while the app is running

The running app locks `bin/Debug/net10.0/LGUTreasury.dll` and `.exe`, which breaks a
normal build. Two options:

```bash
# Option A: build/test into a separate folder (no need to stop the app)
dotnet test LGUTreasury.Tests/LGUTreasury.Tests.csproj -p:BaseOutputPath=bin2/

# Option B: stop the app, then build/test normally
dotnet test LGUTreasury.Tests/LGUTreasury.Tests.csproj
```

`bin2/` is git-ignored.

---

## 6. If you ever see "Unable to copy … file is locked"

The app is running. Stop it (close the console/Visual Studio debug session), or use the
`-p:BaseOutputPath=bin2/` workaround above.
