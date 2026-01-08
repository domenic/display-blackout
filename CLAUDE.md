# Claude Code Project Guidelines

## Package Version Policy

NEVER trust your own intuition on the latest versions of things, especially packages. ALWAYS search the appropriate package registry to make sure you're adding the latest version, of a non-deprecated package, with the correct name.

## Warning Suppression Policy

Do NOT suppress warnings with #pragma or NoWarn. Instead, fix the underlying issue properly. If a warning truly cannot be fixed, discuss with the user first.

## Commit Policy

When in interactive debugging mode with the user, do NOT commit fixes until the user has verified they work. Wait for explicit confirmation before committing.

## Git Staging Policy

ALWAYS carefully review what you're staging before committing. The user may have other unstaged changes from a different session or manual edits. Never use `git add -A` or `git add .` blindly. Instead:

1. Run `git status` to see all modified files
2. Only `git add` the specific files relevant to your current task
3. If a file contains mixed changes (some yours, some not), use `git add -p <file>` to stage only your hunks
4. Verify staged changes with `git diff --staged` before committing
