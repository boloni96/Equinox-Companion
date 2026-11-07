# Direct interaction approaches and rejected home returns — 0.5.1.491

Report 182519 still showed the same map-guided backward loop after 0.5.1.490. Interaction-region searches now first check a direct approach toward the target, ending at a reachable interaction position instead of requiring floor beneath the target marker. Supported half-yalm steps retain full collision and step-height checks. Verified chunks can stream while planning continues. A blocked direct approach continues from its verified endpoint with local geometry near the target; longer approaches may rebase map guidance. This is shared behavior, without Limsa coordinates or player avoidance.

Diagnostics retain the direct approach point count, stop reason and rejected connection. A collision rejection is not assumed to be false. Map artwork does not override physical geometry.

The supplied character-select screenshot also showed an explicit home-return rejection [1000] while travel was at 5/7. The matching English game message is now detected in return-home confirmation and waiting stages before lobby navigation. The routine stops, records the message, and leaves the game dialog visible. It does not treat rejection as arrival, retry indefinitely, or claim an accepted transfer is still pending. Other languages and other error messages are not matched by this specific handler.

Validation: independent route tests exercise a raised target marker, straight travel despite a bent map guide, streamed progress, physical obstacle fallback and rejection diagnostics. Gate tests cover rejection before/after submission and unrelated dialogs. Live navigation and the game rejection still require in-game verification.
