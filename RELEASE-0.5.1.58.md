Companion 0.5.1.58 — false gardening replacement instructions

Older Journal exports incorrectly converted replantOrder=0 (no replacement) into 1.
Companion now ignores a replacement step at or before its original planting step.
This removes false remove/replant prompts and inflated totals without changing
observed crops, planting/care timestamps or batch identity. Valid later replacements
(such as starter Bed 1, step 9) remain supported.
Deploy Journal V7.11.75 to fix the exporter as well. No reset or replant is needed.
973 automated checks passed; Release build has zero warnings/errors.
In-game confirmation of the affected existing garden remains pending.
