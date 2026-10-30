# Organizer and Trader request audit — 0.5.1.311
This is an implementation audit, not a claim that every path has been tested in game.

## Post-transfer refresh — 0.5.1.311
- With complete recorded stock, withdrawals visit only recorded holders; missing items no longer trigger visits to every other retainer. Incomplete records retain the fallback scan.
- A verified open retainer session can record stable complete slot changes even while the summary ItemCount lags. Confirmation can establish that session; identity changes, incomplete containers and closed windows revoke it.
- Withdrawals wait for stable capture before closing a changed retainer. Completion/Stop request inventory and UI refreshes, with a short settling window for already-sent operations.
- Recorded-storage browsing prefers complete dated Companion snapshots/observations over undated Allagan cache, including empty snapshots. Assignment coverage and browsing caches are invalidated after capture.

## Armoury withdrawal acknowledgement — 0.5.1.310
- Confirm newly withdrawn gear when the game places it in the Armoury Chest. Include its net change during the current run without subtracting pre-existing Armoury gear from the requested quantities.
- Preserve quality/tradability filters and normal bag/crystal behavior. Reset the Armoury baseline for each run, including restart and All available.
- Delayed/missing receipt regression checks ensure no blind repeated withdrawal; game inventory routing preferences remain unchanged.

## Transfer notice — 0.5.1.309
- Replaced the unbounded list of non-craftable item names with a collapsed count and a six-row scrolling detail area. Long names are available on hover.
- Cached missing-recipe names until the plan or catalog changes. Transfer mode and quantities are preserved; items without recipes still transfer as listed.

## Assignment coverage and compact review — 0.5.1.308
- Fewer item moves compares zero-move shared homes before proposing consolidation; other priorities may gather fitting categories into fewer destinations.
- Clearing all assignments no longer leaves large categories without suggested homes: categories that cannot fit one destination receive shared homes on current holders, requiring no extra slots for those holdings.
- Prefer five spare normal slots, then allow otherwise-valid tighter fits; never count crystals against the 175 normal slots.
- Crystals use separate per-item capacity (9999); quantities too large for one home can remain across several assigned retainers.
- Unknown categories receive specific-item homes rather than a catch-all category-zero rule.
- The exact proposed additions are applied to a copied setup and checked again. The preview shows the number of item types still without homes before saving.
- Compact default summary; proposed homes grouped by retainer in a collapsed, bounded scrolling area. Slot/move explanations on hover. Missing-item details also scroll within a bounded area. Restore/Clear and Save/Load remain available in expandable sections.
- Existing item/plan overrides and other character assignments remain intact. Optional capacity relief remains separate from assignment coverage; coverage is not a promise that every physical move is immediately executable.

## UI cleanup — 0.5.1.307
- Trader: item icons loaded independently of opening Organizer; Nearby players first and default; Gil & Item Trader labels in launcher/settings/floating icons. Existing standalone window retained.
- Organizer: Inventory / Plans / Transfer / Organize / FC chest. Inventory contains current bags and recorded-storage browsing; Plans contains Add items and saved/Artisan/import/export sources.
- Organize: one scope selector and one Preview/Organize pair. Source/destination appear for Selected movement. Assignments grouped together; Sort after organizing and staging reserve in Options.
- Visible category/quality filters also narrow the All retainers scope with a selected plan.
- Goddess Blue Organizer launcher artwork; Classic retains the original icon. Organizer added to floating-icon settings.

## Already implemented
- Item trading: tradable copies only; bulk select shown, clear selection, Shift-hover add and Alt-hover remove; refresh remaining selection after transfers/restarts.
- Temporary inventory selection and Entrust without saving a plan; categories, HQ/NQ and tradable/untradable filtering.
- Add all matching items / Clear all / Undo clear when building a plan. Categories plus Add all can populate a whole category.
- Plan quantities or All available listed items for transfers. Imported Entrust Keep/protection rules remain in force.
- Assignment-driven destinations do not expand a selected plan. No plan allows all eligible RETAINER items; existing bag items remain protected staging exclusions.
- Company chest direct crystal controls without a saved plan; dedicated retainer crystal storage handling.
- Inventory export includes latest recorded bags, saddlebags, retainers, FC/housing storage, armoury, armoire and glamour dresser plus assignment and plan snapshots. Missing or old observations are not a live universal scan.
- Assignment hover summaries: full parent category only; selected children for partial categories; linked plans and specific items.
- Assignment checker counts unassigned item types, occupied slots and affected categories; hover summaries plus expanded item/retainer detail. Reports incomplete inventory coverage.
- Capacity recommendations compare all recorded owned retainers, quantities, HQ/NQ, stack capacity and existing incoming assignments. Priorities: fewer moves, more spare space, fewer new homes. Optional Relieve full homes adds category destinations for overflow; explicit-item/plan homes are preserved.
- Apply saves recommendations immediately. Save/Load named setups, Undo recommendation, Clear assignments and Undo clear. Restoring rules does not reverse completed transfers.
- Organizing continues with independent feasible transfers after a stranded speculative batch, preserves completed safe prefixes, and revisits blocked groups after progress.
- Sort items after organizing visits owned retainers and invokes normal item-menu Sort.
- Assignment footer no longer claims “available space first.”
- Goddess Blue light-blue control borders; Classic style unchanged.
- Sales table shows recorded Gross gil separately from confirmed Net gil; inferred last listing prices are identified.

## Verification limits and remaining review
- Build and regression results are recorded in GitHub Actions; native game interaction and visual behavior still need in-game checking.
- The original reported trade stop could not be conclusively reconstructed from the available reports; stale selections/restart behavior was addressed.
- Leonis inventory assignment coverage was reviewed. A complete item-by-item keep/sell/usefulness review has not been performed.
- Recommendation heuristics estimate storage fit conservatively; they do not value items or claim a globally optimal layout.
