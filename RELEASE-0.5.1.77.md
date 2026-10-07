# Companion 0.5.1.77

Fix false aethernet arrival timeouts when characters land on different sides of a main crystal. .76 successfully performed both Saucer directions, but the follower's landing was outside 15 yalms of the leader's recorded landing in two return trips.

Record the exact native aethernet destination ID when submitting its matched menu choice. After an observed loading cycle, confirm the same trip/world/territory/map/instance and proximity to that specific destination crystal, rather than requiring proximity to the leader's landing. Preserve existing arrival checks as fallback when native destination metadata is unavailable. No arrival based only on leader visibility or being in the same map.

Regression tests include the two reported Saucer landing positions and rejected missing-selection, missing-loading, wrong-map/world, distant and wrong-floor cases. In-game retest: Saucer crystal -> shard -> crystal; follow should resume promptly without false timeout. No Cloudflare update; Journal V7.11.81.
