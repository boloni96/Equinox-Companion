# Companion 0.5.1.102
Repairs two failures observed in 20:29 diagnostics on 0.5.1.101:
- Destination's recording was committed with only interaction/scene steps before its cutscene appeared. Keep recording while a native event is active regardless of step count; allow a 15-second quiet interval for interaction-only recordings (normal completed dialogue retains two seconds).
- Cid's recorded skip was queued behind a dialogue line that had already advanced. With follower skip permission, a recorded skip supersedes preceding consecutive Talk steps only for that same scene and NPC conversation. Quest acceptance, choices, scene confirmations and post-cutscene dialogue remain intact.
Cutscene Talk no longer blocks requesting Escape. Other menus and text entry still block. The existing default-off opt-in, live game-owned prompt/callback checks and single attempt remain.
Automated regression cases cover these recordings and boundaries. No live crash reproduction; native Escape delivery and Yes behavior remain unverified.

Also retains recordings for delayed post-completion Talk pages, aligns only to a unique exact later recorded Talk line in the same uninterrupted scene/conversation if dialogue already advanced, and records expected/actual text and scene on mismatches. The 20:32 screenshot alone does not establish which of these conditions affected that page.
