# Companion 0.5.1.105

- Allow 12 seconds after each NPC interaction for delayed dialogue/duty prompts before retrying, while continuing to observe scene and window readiness. Keep the three-attempt limit and exact quest/prompt checks.
- Prevent a continuation scene after a committed conversation (including solo-duty Proceed) from being recorded as another NPC/Destination click. A genuine next interaction restores fallback capture.
- Add regression checks for delayed prompts, final-attempt waiting, and continuation capture suppression.

Build/test validation does not establish in-game success. Automatic cutscene skipping remains unresolved; keep it disabled. This release does not request a crash reproduction or add post-Proceed cutscene replay. No Journal deployment required.
