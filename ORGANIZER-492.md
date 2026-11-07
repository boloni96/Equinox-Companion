# Organizer transfer recovery — 0.5.1.492

Report 212811 on 0.5.1.490 stopped at transfer confirmation stage 7. The pending withdrawal of item 28710 left both its source and destination unchanged. Sixteen earlier withdrawn items remained in transit. The original native submission result had been overwritten by confirmation snapshots, so the report does not establish why the game did not apply the move.

Preserve the original submission, timestamp and native result alongside live confirmation, plus a bounded history of deferred transfers. Confirmation still requires matching source and destination deltas. Selected-retainer identity and loaded inventory are verified before submission and confirmation.

After eight seconds, if both slots remain unchanged and the native inventory-operation queue is empty, defer that item/quality for this run. Remove its remaining dependent transfers, retain unrelated transfer intent, refresh live stock and rebind the next visit before moving another item. Do not resend the unconfirmed command. Deferred types stay excluded from subsequent replanning. Existing carried-item counts are not increased for an unconfirmed withdrawal. Deferred types and remaining cargo are reported at completion or stop.

A partial or unexpected delta stops without retry. Native operations still pending retain the outer timeout; its message now identifies the unconfirmed item rather than incorrectly implying that stock has not loaded. This does not diagnose the original server refusal or guarantee delivery if remaining live capacity or stock has also changed.

Validation: plugin build and gate suite, including delayed confirmation, late successful confirmation, unchanged slots, one-sided changes and removal of a failed withdrawal's dependent delivery while preserving unrelated cargo. In-game continuation requires testing.
