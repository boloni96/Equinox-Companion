# Navigation 0.5.1.471

## Route reuse and failure recovery

Obstructions now split cached routes around the failed connection, retaining useful
sections on either side instead of deleting every route to that destination.
Actual travelled progress is saved with the last two samples omitted near failure;
planned but unwalked geometry is never saved as successful travel.

A cached endpoint can connect to a changed approach point up to 2.2 yalms away if
physical traversal to the exact new destination succeeds. Previously the network
restricted this connector to the arrival tolerance (often only 0.4 yalms).
When no complete route is available, a connected saved prefix that reduces the
remaining straight-line distance by at least six yalms can be followed while the
remaining route is calculated. Entry checks are bounded at 32. Every movement
step retains live collision checks and the current blocked-point exclusions.
Cache misses, prefix reuse and section invalidation now appear in prompt exports.

## Planning cost

Wall-clearance cost is evaluated when a promising candidate is selected, not for
all sixteen neighbors at every expansion. Candidates are reprioritized after that
cost is known, before physical validation and closure. Planning-only wall costs
share quarter-yalm spatial cache entries, as live soft preferences already did.
Ground support and body-collision acceptance are not quantized or relaxed.

The synthetic open-road regression uses 24 wall-cost reads for 26 expanded nodes;
this demonstrates reduced preference-query work, not a live route timing guarantee.
The supplied .469 exports recorded 195,958 and 226,477 native queries during long
Goblet/Mist searches. Their captured geometry is insufficient for an exact native
collision replay, so their 81/90-second failures still require an in-game retest.

## Ground support

Walking rechecks missing support for at most two seconds, without marking an
obstacle or rebuilding from an unreadable origin on the first miss. Recovery
continues the route; persistent failure stops and retains travelled sections.
Diagnostics distinguish no support hit, rejected normal, height mismatch and
blocked headroom. No surface/slope/body thresholds were weakened. The Goblet
report's empty obstruction probe list cannot establish which ground test failed;
this change does not claim to correct a permanently misclassified stair surface.

## Validation

Independent tests cover cache splitting, safe prefix reuse, blocked entry/final
connectors, territory/floor isolation, bounded support retry, lazy-clearance query
counts and existing sliced/bidirectional search tests. Gate tests and Release build
pass. Live Goblet, Mist and stair traversal remain to be verified by the user.
