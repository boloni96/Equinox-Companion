Equinox Companion 0.5.1.124

Submarine reserve warnings now require at least one recorded vessel in the
character's matched FC fleet. Supply-only/empty-cache characters remain neutral,
including their account/group summaries. Missing values are never treated as zero.
A recorded fleet is workshop evidence for FC members as well as estate owners.

Observed empty workshop capacity can show registration availability without
inventing repair/fuel needs. Registration uses direct workshop capacity, not
ambiguous cached capacity. Local voyage observations now retain their slot count.
Voyage Complete uses calm green; dispatch/registration stays yellow and applicable
repair/stock warnings remain orange. Solari's one-vessel low repair reminder stays.

Validated: Release build with zero warnings/errors; GateTests for absent fleet,
zero/unknown supplies, registration-only workshop, invalid capacity, orphan repairs,
one-vessel shortages and aggregate colour. Journal V7.11.133 attention list audited:
already requires vessel/observed-slot evidence, deduplicates FCs, and does not turn
carried supply counts into shortage alerts. No new Journal deployment required.
No native in-game UI test performed here; update Companion and compare the rows.

Performance: Submarines history-to-display projection, gardening source plans and
resolved local garden actions are reused between frames with a 250ms maximum age.
New roster/source stamps invalidate immediately. Timers/input remain live; game
observation and network schedules are unchanged. Cache invalidation/expiry tests
passed. This removes repeated rendering work; real-game FPS impact is unmeasured.
