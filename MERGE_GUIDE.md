# Merge Guide — LGUTreasury

How to merge a feature branch (e.g. `customfix`) into other branches (`main`, `gab`,
`demo`, `alleksu`, ...) without breaking the build or re-committing files that should
never be tracked.

These are manual steps. There is **no** merge script in this repo anymore — it was
removed. Commands below work in PowerShell or Git Bash.

> The tricky part is that older branches (`main`, `demo`, `alleksu`, ...) still have
> `bin/`, `obj/` and `publish/` **committed in their history**. The feature branches
> stopped tracking them and added a `.gitignore`, so a straight merge hits
> modify/delete conflicts. See **section 2**.

---

## 0. One-time setup (already done on the feature branches)

- Credentials live in `.env` (git-ignored). `.env.example` is committed for others.
- `appsettings.json` no longer contains the DB password.
- `.gitignore` ignores `bin/`, `obj/`, `publish/`, `bin2/`, test output and `.env`.
- Build artifacts were untracked with `git rm -r --cached bin obj publish`.

**These only take effect on a branch once the commits are on that branch.** The merge
carries them over; see section 2 if a target branch also touched those files.

> ⚠️ The old DB password is still in **git history**. Rotate it. To purge it from
> history (destructive, rewrites SHAs, needs force-push):
> ```bash
> git filter-repo --path bin --path obj --path publish --invert-paths
> # or BFG; then: git push --force origin <branch>
> ```
> Only do this if you can coordinate with everyone who cloned the repo.

---

## 1. Basic merge steps

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

If step 3 stops with conflicts in `bin/`, `obj/` or `publish/`, go to section 2.

---

## 2. Merging into a branch that still has build artifacts committed

Target branches such as `main`, `demo` and `alleksu` committed `bin/`, `obj/` (and on
`main`, `publish/`) long ago. The feature branch deleted those from tracking, so Git
reports **modify/delete** conflicts (`deleted by them` / `deleted by us`) or a flood of
binary conflicts. The generated files are now covered by `.gitignore`, so **accept the
deletions** — do not keep them.

### Option A — resolve the conflicts after they happen

```bash
git checkout main
git pull --ff-only
git merge --no-ff customfix            # stops on conflicts

# see what conflicted
git status
git diff --name-only --diff-filter=U

# accept removal of every generated/build file, then finish the merge
git rm -r --cached --ignore-unmatch bin obj publish
git add -A
git commit                             # completes the merge with the default message

# sanity checks
git status                             # no build artifacts staged/tracked
git ls-files bin obj publish           # should print nothing
```

If something looks wrong and you want to start over:

```bash
git merge --abort                      # back to the pre-merge state on main
```

### Option B — untrack the artifacts on the target first (fewer conflicts)

Do this on the target branch *before* merging, so there is nothing to conflict with:

```bash
git checkout main
git pull --ff-only
git rm -r --cached --ignore-unmatch bin obj publish
git add .gitignore
git commit -m "chore: stop tracking build artifacts"
git merge --no-ff customfix            # merges cleanly
```

### Notes

- Add `publish` to the `git rm` only if the branch actually tracks it (`git ls-files
  publish`); `main`/`origin/main` do, most others do not.
- The artifacts stay in **old history** even after this. That only matters for repo
  size / leaking the old DB password — see the `filter-repo` note in section 0.
- Never re-add `bin/obj/publish` in a later commit; `.gitignore` should keep them out.

---

## 3. Resolving other common conflicts

### a. `.env` / credentials
Never commit `.env` (it is git-ignored). If a conflict involves `appsettings.json`, keep
the version **without** the password (`"DefaultConnection": ""`); the real value comes
from `.env`.

### b. `.gitignore`
If both branches edited `.gitignore`, keep both sets of rules (union them).

### c. Aborting
```bash
git merge --abort     # return to the pre-merge state on the target branch
```

---

## 4. After each merge — checklist

- [ ] `.env` exists locally with valid credentials.
- [ ] `git ls-files bin obj publish` prints nothing (artifacts still untracked).
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
