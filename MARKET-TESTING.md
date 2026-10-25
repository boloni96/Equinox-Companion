# Equinox Market & Seller — verification checklist

Optional local tools, disabled by default. Open `/eqmarket` or `/eqseller` and enable them. The Options launcher and Settings → Market lead to the same controls. Both appearance styles use the same implementation. Closing a window does not stop a run: use **Stop** or `/eqseller stop`.

## Included

- Compact Seller window at the summoning-bell retainer list, with a direct settings button and current/selected/all-retainer scopes for the current character. Check prices does not submit changes; Update prices checks again and applies the snapshotted rules.
- Stack and undercut toolbar attached above the native selling list, or movable. Stack limits affect new listings only.
- Matching HQ/NQ unit prices, exact known own-retainer price matching, configurable minimums, item exclusions, undercut, rounding and low-price-gap rule. The default 40% rule uses 250,000 from 100,000 / 250,000 / 260,000, then subtracts one gil.
- A successful empty comparison can use recent matching sales. Default: median, seven-day age limit, at least three sales, 50% median outlier filter. Failed or insufficient comparisons keep the existing price.
- Manual comparison click-to-price, optional click-to-confirm/copy, Shift bypass, Ctrl clipboard price, Alt history toggle.
- Selling and Retainers highlight priorities, show character/world and reuse the existing account-aware character switch controls. The optional top-bar count opens Selling; right-click opens settings.
- Adjustable six-day visit reminders use actual observed retainer interactions. They are a precaution, not an officially verified expiry or proof that selling has stopped. Background checks never reset visits.
- Listing snapshots, observed sale receipts, confirmed market purchases, filtered summaries, price-change audit, CSV export, full JSON backup and Allagan Market sale-history CSV import.
- Background Universalis checks batch up to 50 distinct items per world, one request at a time. Default: 15-minute refresh and maximum data age of 60 minutes. They run with Market/Seller windows closed, yield to active gameplay uploads and Seller runs, and back off on errors/rate limits. Known retainer names are matched on their own world without undercutting. This is cached advice, never the source for an automatic price submission.
- Red means update suggested; green means checked and competitive; amber means history-based or skipped; blue means checking. Checks expire after five minutes.
- Marketbuddy's lock and AutoRetainer's published busy/pause APIs prevent simultaneous repricing. Missing compatibility APIs stop the run rather than continue concurrently.

## Accounting boundaries

Listed value is an asking value. Confirmed net revenue comes only from supported game sale receipts. Offline stock disappearance with a matching retainer gil increase is explicitly **inferred**, without a confirmed net amount or exact sale time. Withdrawals alone never count as sales. Imported history has gross values but no verified net amount. Use the Source filter to distinguish them; imported and native records are not assumed to identify the same transaction. Reimporting the same CSV does not duplicate its rows, including multiple identical sales within one second.

Purchase spending records completed market purchases, including the reported tax. It is not yet assigned to individual resale lots or crafting recipes. Vendor purchases, gathered materials, crystals, shared stock transfers, old inventory and intermediate crafts are not automatically costed. Profit therefore is not claimed.

Records currently stay on this computer in `market-ledger.json`, with a previous-file backup and background writes. They are separate from gameplay observations and the Journal's sync queue. A read failure preserves the file and pauses tracking. Exported sales and backup files are available through **Files → Open export folder**.

## In-game checklist — pending

Use **Check prices** first, then test a single inexpensive listing before an all-retainer update.

- [ ] Disabled tool leaves native selling behavior unchanged.
- [ ] Enable and open bell: Seller appears; closing it does not disable tracking.
- [ ] Attached toolbar stays above the selling list at the current game UI scale; movable mode remembers its position.
- [ ] New listing stack size 1 / 99 works; repricing an existing stack never changes its quantity.
- [ ] Check prices traverses the chosen retainers, records each listing and performs no confirmations.
- [ ] Single-retainer Update prices applies the reviewed unit-price rule, then observes the price in the list before continuing.
- [ ] HQ and NQ comparisons remain separate. A lowest known own listing is matched exactly, including tied external offers.
- [ ] Empty market loads history; busy/failed comparisons keep the old price and record a reason.
- [ ] Pause submits nothing; Resume refreshes comparisons; Stop submits no later price and leaves the current window available.
- [ ] Changing the item/quantity while paused stops the run without dismissing the changed editor. Logging out or changing worlds stops it.
- [ ] Manual comparison click selects the displayed price; Shift/Ctrl/Alt behavior matches settings. Clipboard paste does not confirm a listing.
- [ ] Top-bar click opens Selling; right-click opens settings. Retainer priority order, visits and existing character-switch/account confirmation work correctly.
- [ ] A retainer visit clears its visit reminder without changing listing observation timestamps. Empty retainers receive no visit reminder.
- [ ] Background checks use each retainer’s home world, distinguish HQ/NQ, match known own retainer prices without undercutting, and do not turn old/missing/error data green. Tooltips show the upload time, not just the retrieval time.
- [ ] Highlight colors survive native list refreshes and restore when disabled. Hover reasons in Market match the item.
- [ ] Starting Seller pauses new AutoRetainer scheduling and waits for queued actions to settle before touching the menus; a completed/stopped run restores its previous pause state. Marketbuddy does not also alter prices.
- [ ] A confirmed market purchase appears once under Spending with quantity, unit cost and tax. Failed/cancelled purchases do not appear.
- [ ] A supported sale message adds its actual net amount once; an unobserved retainer/quantity remains labelled unknown. Verify receipt parsing on the installed game build.
- [ ] CSV import, reimport, filtered export and backup succeed without freezing the game. Net totals exclude inferred/imported amounts.
- [ ] Goddess and classic designs remain usable at minimum size; both windows minimize and reopen through their icons.

## Automated verification

`market-tests/SellerTests.csproj` covers pricing boundaries, freshness, failed requests, history filtering, stop/pause transitions, changed listings, confirmation timeouts, offline accounting and CSV import identity. Existing transition and native garden regressions also run. These tests and a successful build do not replace the pending native in-game checks above.

Live Universalis access from the build environment returned HTTP 403, so the network contract is covered with documented-schema fixtures; actual connectivity must be checked in-game. The game UI click paths and installed sale-message templates are also pending in-game verification.

## Cost tracking next

The next accounting step is allocating observed purchase lots to resale stock, then recording confirmed recipe consumption/output and allowing manual missing costs. A cost-complete sale can show profit; incomplete allocations must remain marked unknown. Shared transfers need their own provenance before carrying costs between characters or persons.


## 0.5.1.244: native selling and compact organization

Install through the normal Companion feed. These are in-game checks, not results inferred from the automated tests.

- Leave Helper and automatic coffer opening enabled, then open a bell. Check prices should start and show the current phase. Those actions yield during the Seller run; saved settings stay enabled.
- Open a new listing or Adjust Price. Comparison and recent market history should follow the manual settings. Shift bypasses automatic comparison; Ctrl pastes a price without confirming.
- Click a comparison row. The matching HQ/NQ adjusted unit price should fill the editor and/or clipboard according to settings. Confirm remains explicit unless enabled in settings. Test a sorted results list too.
- Check prices across a retainer with duplicate items at different prices, then all retainers. Check must not write prices. Update verifies live offers and handles every listing independently.
- Retainer selling counts show red updates/total, such as 2/13. A question mark means incomplete or uncertain data. Per-slot highlights must match the rows, including duplicate stacks and mixed HQ/NQ.
- An ignored unusually low price stays visible as Lower listing. The automatic update continues to respect the gap rule, minimum and exclusions.
- Open the game's View sale history twice. Sales should appear once, with confirmed net gil and timestamps. Repeated reads must not add revenue again. Mannequin sets are excluded from item-level history capture.
- Escape from a focused Seller/Market window should dismiss one native retainer window at a time and leave the plugin open. Editing a text field or plugin popup keeps its normal Escape handling.
- Settings are grouped under Manual selling, Pricing rules, Background checks & visits, Windows & highlighting, and Per-item minimums & exclusions. Hover controls for explanations.
- Check the selling toolbar in your game theme and UI scale. It attaches near the upper-right of the selling window and remains movable when detached.

Native integration references: [Marketbuddy event handling](https://github.com/chalkos/Marketbuddy/blob/main/Marketbuddy/MarketGuiEventHandler.cs), [FFXIVClientStructs inventory links](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/InventoryItem.cs), [Allagan Market row highlighting](https://github.com/Critical-Impact/AllaganMarket/blob/main/AllaganMarket/Services/HighlightingService.cs), [CashFlow history layout](https://github.com/NightmareXIV/CashFlow/blob/main/CashFlow/Data/ExplicitStructs/RetainerHistoryData.cs), and [AscendedLedger history interpretation](https://github.com/jkleinne/ascended-ledger/blob/main/AscendedLedger/Services/RetainerHistoryCaptureService.cs).

## 0.5.1.245: diagnose native comparison failure

User verification of 244: automatic runs reach the item editor, but Compare Prices does not open and manual price clicks have no effect. The root cause remains unverified; passing fixture tests did not establish that these native actions work in-game. This version adds evidence without changing the native click or inventory validation rules speculatively.

Seller > Diagnostics > Start fresh trace, then open Adjust Price. If comparison does not open, click the game's Compare Prices button yourself and click a price. Export diagnostics from Seller and use Open export folder to retrieve the JSON. A second fresh trace can capture a Check prices run reaching the editor; Stop before exporting. No general garden recording is required.

The local bounded trace includes native window transitions, retainer/item identity, inventory readiness and symbolic slots, comparison button node/events, search loading state, run phases, manual capture, pricing settings/locks, comparison responses and reasons for ignored clicks. It is copied into the existing export and never sent through pairing.

Retainer names now share the review color. Counts read `13 selling · 6 updates`, or `13 selling · unchecked` when no update is known and data remains uncertain. Known update counts do not include unchecked listings. Empty retainers show `0 selling`.

Close Seller when leaving the bell defaults on under Market & Seller > Windows & highlighting. Retainer transitions keep Seller open; leaving the bell closes it after a one-second settling period. Turning the option off keeps the window open.

## 0.5.1.246: comparison, per-item history and AutoRetainer handoff

Starting Check prices or Update prices now suppresses AutoRetainer scheduling immediately, then waits for existing queued actions to finish and for a short stable idle period. The Seller run starts automatically after that handoff. It never aborts AutoRetainer's task queue or changes its venture, multi-mode or character configuration. Waiting is cancellable with Stop and times out after 45 seconds.

Finishing, stopping, losing the game context, disabling the tool or unloading Companion releases the pause. AutoRetainer stays paused if it was paused before Seller. Temporary IPC restoration errors stay visible and are retried rather than discarded. During a run, losing the verified pause or observing new external automation stops Seller before its next action. Marketbuddy's lock is acquired after AutoRetainer settles so queued work can finish first.

The handoff and restoration status are included in Seller diagnostics. Test with AutoRetainer idle, working on a venture, already paused, and with Stop pressed while waiting. Also verify completion restores its original state. The 245 game trace identified the comparison blocker: `WaitingForListings` remained true with no results window and again after usable listings appeared. Seller now uses the enabled native Compare button and validates a fresh response plus every received listing, including listing identity, before reading or applying prices. It no longer treats that flag as a readiness gate. Manual game Compare clicks start the same request tracking before the game handles the click. Partial, stale, different-item and failed responses cannot supply a price.

Every newly opened item editor and comparison clears its history state. This also covers automatic comparison being bypassed with Shift or disabled in settings, which previously left the first item's history-request flag set. Requested history is awaited before an automatic run moves on. Retainer View sale history is a separate capture: tests verify all 20 different native sale rows and repeated reads without duplicate revenue. Its row count and any validation failure now appear in diagnostics; no change to the verified packet layout was needed.

In-game checks for this update:
- Open Adjust Price: Compare and History should open. Click a matching-quality price and check the editor/clipboard according to your settings.
- Close it, open a different item, and repeat. Also try Shift plus the game's Compare button; the second item must show its own history.
- Run Check prices through several items before Update prices. Check must leave prices unchanged.
- Open a retainer's View sale history with multiple sold items twice. Confirm all rows appear once in Market.

Automated checks do not replace these native in-game tests. Garden detection and mapping paths are unchanged.

Upstream contracts checked: [suppression API](https://github.com/PunishXIV/AutoRetainerAPI/blob/main/AutoRetainerAPI/AutoRetainerApi.cs), [AutoRetainer scheduling](https://github.com/PunishXIV/AutoRetainer/blob/master/AutoRetainer/Scheduler/SchedulerMain.cs), [busy-state API](https://github.com/PunishXIV/AutoRetainer/blob/master/AutoRetainer/Modules/EzIPCManagers/IPC_PluginState.cs), and [busy-state sources](https://github.com/PunishXIV/AutoRetainer/blob/master/AutoRetainer/Helpers/Utils.cs).


## 0.5.1.247: consistent tables and status explanations

Sales, Spending and Price changes use compact, resizable tables like Retainers and Summary. Search, person/account/character/world filters and period controls stay above the scrolling records. Table headings stay visible. Selling groups retainers under one character heading, preserves game retainer order, and uses the displayed character group when opening Seller. Narrow windows put secondary price/status details in hover text. Item names copy on click and retain their right-click actions. Price checks distinguish confirmed updates, suggestions, kept prices and skipped checks; gross amounts are never labelled as confirmed net. Files groups export/backup and import, and Settings remains the last tab.

Hover the native retainer list's selling count, or a retainer/status inside Equinox Market, to see the affected items and their actual reasons. Unknown data, an expired live comparison, changed listings, old background data, exclusions and skipped checks are explained. Skipped checks have their own label; a single update uses the singular. Native hover reads only the visible row and builds its explanation only while hovered; it does not change the game's UI events or pricing automation.

Checks in both Goddess and classic designs:
- Resize Market to its minimum width; use every tab, filters, pagination and scrolling. Headers and controls must remain accessible, and full clipped names/reasons must be readable on hover.
- In Sales/Spending, click an item name to copy it and right-click for item actions. Hover amounts for evidence, tax or gross/net detail.
- In Price changes, confirm Updated is reserved for confirmed writes; competitive prices say Kept and unapplied targets say Suggested.
- Selling and Retainers retain game order within each character. Open Seller selects the displayed retainers of that character, not only the first one.
- With Market closed, hover the game's selling count. Verify unchecked/skipped reasons and affected item names. Move or scale the game window, cover it with another window, and close the bell; the tooltip must follow the current native row and disappear when no longer hovered.

The 246 diagnostic run completed all ten retainers (51 confirmed updates, 16 skips), restored AutoRetainer suppression, and captured all 20 native sale records on several retainers. This update changes presentation/reporting and leaves comparison, pricing, AutoRetainer handoff and garden detection paths intact.


## 0.5.1.248: own-retainer matches, increase limits and history evidence

When the lowest matching HQ/NQ offer is from a known own retainer, automatic pricing matches it exactly instead of ignoring it or subtracting another undercut. A tie with an outside seller also matches the own price. Fixed/percentage undercut and rounding are bypassed for that match; item minimums, exclusions, freshness and the raising setting remain in force. When an outside seller is cheaper, the existing competitor/gap rules still apply. Manual comparison clicks at a verified own price use the same exact match. Background Universalis estimates recognize known names on the requested world; automatic submissions still require fresh in-game evidence.

Automatic increases now have a configurable maximum: 100% by default (at most doubling the current price). This applies to live offers, own-retainer matches and history-based targets. A larger proposed increase is skipped with the old price preserved and the reason shown on hover. An explicit item minimum may still raise the price to that minimum; explicit manual price clicks are not capped. The supplied Hempen Camise example (50,000 gil current price versus 10,000,000 gil offers) stays unchanged even when automatic raises are enabled. This is an increase limit, not a claim that a seller or transaction is fraudulent.

The empty-market fallback retains its existing default: median of at least three matching-quality sales in the past seven days, after the outlier filter. It fetches recent transactions even if the optional history-window setting is off. The later 246 export contains a manually opened HQ Replica Heavy High Allagan Coat comparison with zero listings and 20 received transactions. It does not contain their dates/quality or the resulting pricing decision, so it cannot prove whether those sales qualified. Opening a manual editor calculates a suggestion and does not automatically submit it. Update prices applies permitted targets; Check prices only reviews them.

A manual suggestion or skip now appears in Seller with its reason on hover. Insufficient-history reasons include the loaded count, matching quality count, age window and required minimum. Diagnostics include pricing settings, recent decisions, up to 20 received sale rows for the decision (without buyer names), and the last 200 persisted price checks so the explanation survives the detailed trace rolling over. The full market backup retains up to 2,000 price checks.

Verify after the normal plugin update:
- Own retainer at 12,345 gil, second retainer above it: the target is exactly 12,345, even with a percentage undercut or rounding enabled. A second pass must not lower it again.
- A 50,000-gil listing compared with 10,000,000-gil offers remains unchanged even with Allow raising competitive prices on. Hover the skipped check for the increase limit.
- A cheaper outside offer uses the existing undercut/gap policy. HQ/NQ, minimums and excluded items remain separate.
- Click a verified own comparison price manually: editor/clipboard use the exact match according to the existing click settings.
- With no matching listings, run Check prices for an item with sufficient recent matching sales. Hover its result in Price changes. Then use Update prices to test a confirmed change.
- An item with old, wrong-quality or insufficient sales stays unchanged and explains the skip. Export Seller diagnostics if the live result differs from that explanation.

Automated checks cover exact matches, duplicate stacks, ties, external offers, minimums, raising, quality, mannequins, unknown IDs, cached estimates, extreme increases from listings or history, configurable cap boundaries and explicit minimums, history quality/age explanations and delayed empty-result history through the actual Seller run state machine. Native game clicks still need the in-game checks above.
