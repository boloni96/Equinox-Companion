# 0.5.1.498 — Leave blocking parties before world travel

World Visit previously neither cleared local party membership nor recognized the destination confirmation when the game appended the solo/cross-world-party advisory shown in the user's screenshot.

Shared native world travel now sends /leave once for a blocking local party, waits for live membership to clear, and only then advances World Visit. Same-DC cross-world parties are retained. DC travel leaves local or cross-world parties before sending /logout. An existing destination confirmation is cancelled only when its text matches this trip's exact destination; unrelated dialogs are not approved. Only an explicitly requested leave action can confirm the known English leave-party prompt. Failure to clear membership stops after 15 seconds without submitting the transfer or repeating /leave.

The exact destination prompt accepts either the original question or that question followed by the known solo/cross-world-party advisory, with whitespace normalized. Other destination names or additional text remain rejected. A late blocking party is checked again at world list / confirmation stages.

Reference: Lifestream WorldChange.LeaveParty / LeaveAnyParty uses /leave and the cross-world-party condition. This implementation keeps native travel independent of Lifestream and verifies membership before proceeding.

Validation: Release build, gate suite, and tests for solo/local/single-member-party-ID/cross-world/DC cases, exact advisory text, wrong destinations and unrelated confirmations. In-game leave and travel remain to be verified.
