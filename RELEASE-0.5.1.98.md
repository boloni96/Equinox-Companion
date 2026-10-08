# Companion 0.5.1.98

Decode solo-duty prompt SeStrings through the existing bounded menu-text reader before signing and relaying them. Raw native formatting bytes are not valid Journal action text; preserve the visible quest prompt and existing exact matching checks.

Include solo-duty captures and failed action payloads in diagnostics. On an explicit HTTP 400 for a completed conversation, send cancellation for that conversation so its follower recording reservation does not linger. Do not replay the rejected action.

Evidence: 19:20 exports show a solo-duty conversation committed at 19:19:38, action HTTP 400, and the follower still waiting for the recording. Raw rejected payload was not exported by .96, so the exact cause remains unconfirmed pending in-game validation. Both clients reached Central Shroud instance 2 earlier; automatic destination-crystal instance switching remains separate unfinished work. Cutscene skip remains disabled.
