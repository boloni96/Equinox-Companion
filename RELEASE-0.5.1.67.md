# Companion 0.5.1.67 — FollowThem travel recovery

Requires Journal V7.11.80 for gate transitions and instance metadata. Existing
pairing keys and Journal data stay in place. FollowThem settings remain local.

- A stop blocked by chat remains queued and is attempted when text input closes.
  Preserve the current-process, configured backward-key cancellation and its
  acknowledgement; never send movement input into chat.
- Face only the exact validated door/crystal/NPC before interaction. No camera
  controls or target cycling. Named private-room YES prompts match the recorded
  room owner; different owners and unrelated confirmations are rejected.
- Failed dispatch is limited to three attempts. Direct approaches use the
  configured stuck timeout, stop movement, and clear dependent trips on failure.
- Capture unhandled walking zone transitions and explicit instance-gate choices.
  Replay the short approach through optional Lifestream (no vnavmesh), choose the
  recorded numbered instance, and require that instance for confirmed arrival.
  These are experimental native paths, not proof that every gate is supported.
- /equinox travel WorldName requests Lifestream and shares the exact destination
  before departure, only with active followers. /li WorldName observation is
  best-effort because command interception may hide it from the game hook.
  Menu-initiated travel retains the post-arrival fallback. Suppress intermediate
  teleports during a shared World/DC intent. World/DC options on the follower
  still apply; Lifestream's own travel and account restrictions remain in force.
- Ordinary teleport no longer waits indefinitely merely because the leader is a
  party member; a party offer must actually be active and visible.

Validation: Release build, zero warnings/errors; 1,166 policy assertions pass.
Journal relay integration tests pass, including instance bounds, boundary source
validation, session isolation, expiry, and zero Journal writes.

In-game validation remains required: Stop after closing chat, object facing,
private-room YES, first crystal/shard attempts, gate/instance transitions, and
World/DC travel. No in-game pass is claimed by the build or policy tests.
