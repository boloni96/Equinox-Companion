# Companion 0.5.1.118 — observation times and submarine dyes

- Upload envelopes include source-side sentAt. Each non-cache event exposes observedAt from its original At; queue retries preserve it. Imported submarine caches keep observation time unknown.
- Observe each submarine component's first stain from loaded workshop equipment only after every equipment item matches the recorded SubmarinePart ID. Unloaded or mismatched equipment reports unknown colours; it never substitutes another vessel's dyes.
- Journal V7.11.121 preserves observation, transmission and receipt times separately and renders modular regular/modified submarine artwork with recorded stain colours. Existing Journal versions ignore the added optional fields.
- No changes to Helper, FollowThem, QuickLoot or coffer interaction.

Validation: the publication workflow must pass the transition/regression suite and full plugin build before atomically updating the installer feed. The new workshop dye observation needs in-game confirmation with the equipment panel loaded; no live game test is claimed.
