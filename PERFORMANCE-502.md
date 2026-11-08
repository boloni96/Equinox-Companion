# Startup and garden performance — 0.5.1.502

Both clients' .501 reports retain large startup update stalls (2.43/2.91 seconds), first-save capture peaks (545/857 ms), and garden/Organizer drawing spikes. They do not yet isolate the full update cost; other-plugin stalls also remain visible in Dalamud.

Targeted changes:
- Prepare the existing configuration snapshot serializer using a fresh empty configuration on a worker. This worker never reads live settings or calls game services and never writes a file. Normal saves remain dirty until it completes, then use the existing snapshot/writer. A warmup failure falls back to the existing save path; unload still flushes the latest live snapshot synchronously.
- Garden overview house grouping refreshes at most once per second instead of every 250 ms when inputs are unchanged. Garden observations and roster/action changes still invalidate immediately. Unrelated observations no longer invalidate this overview cache.
- Completion-history indexing skips irrelevant collection categories and allocates found-reward sets only on a match. Quest evidence, owned storage boundaries, obtained/unlocked reward matching and append semantics remain unchanged.
- Separate timings for roster refresh, adventure update, celebration update, testing, maintenance, login observations, quest tools and delivery. Report save warmup state explicitly.

Validation: Release build and full GateTests pass, with tests for irrelevant history, obtained rewards, quest completion, shared-storage exclusion, immediate garden invalidation and fallback expiry. Existing snapshot isolation/reload tests also pass. Ten pre-existing build warnings remain. No in-game performance result claimed: first-use JIT, remaining UI work, save captures and other plugins may still stall.
