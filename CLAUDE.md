# Claude Code Project Guidelines

## Package Version Policy

NEVER trust your own intuition on the latest versions of things, especially packages. ALWAYS search the appropriate package registry to make sure you're adding the latest version, of a non-deprecated package, with the correct name.

## Warning Suppression Policy

Do NOT suppress warnings with #pragma or NoWarn. Instead, fix the underlying issue properly. If a warning truly cannot be fixed, discuss with the user first.

## Commit Policy

When in interactive debugging mode with the user, do NOT commit fixes until the user has verified they work. Wait for explicit confirmation before committing.
