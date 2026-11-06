# 0.5.1.442 — native travel corrections

The 15:41 test export confirmed the DC introduction clicked node ID 3 (Help), while Select World is ID 4. Use the typed ConfirmButton and wait for the destination list before advancing. Logout confirmation is sent once.

After a home return, a different-DC destination now continues from character selection, retaining the exact character and service account. A final home-world destination still logs in; a same-home-DC World Visit still logs in to reach its aetheryte.

Aetheryte approaches now probe the character's floor height plus bounded lower heights instead of requiring the elevated crystal origin to be within 0.7 yalms of the floor. Use the established eight-yalm crystal interaction range. Game-follow ownership remains guarded; request its stop and report any wait. The supplied export did not record which pre-navigation condition blocked those two attempts, so these walking changes require an in-game retest.

Exports retain bounded snapshots of large menus instead of dropping them, include DC/world tree item states, and record walking waits with positions. Ignore timeline-label animation noise.

Validation: full build; five regression suites, including home-return continuation and raised-crystal floor approach. Live tests still needed: Odin → another Light world; visiting Light → home Raiden → Sagittarius without intermediate login; service-account selection; one-click shopping continuation; right-click stop.
