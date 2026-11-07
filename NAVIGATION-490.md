# General route shortcuts 0.5.1.490

Report 072041 on 0.5.1.489 showed three successful Limsa approaches with a repeated backward detour in the planned continuation. Near (-92.29, 8.29), the route returned toward (-97, 7) before proceeding toward the crystal. Actual travelled positions also reversed X direction. This was present in the planned path, not established as a camera-only issue.

Shared map-guide validation now tries a shorter connection past multiple bends before following each guide segment. Candidate chords are at most twelve horizontal yalms and within twenty yalms of guide arc. Each half-yalm step requires native floor support, legal elevation change and full swept-body clearance. Nothing is committed until the entire shortcut succeeds. A rejected shortcut preserves the original guide and its obstacle detour. Work remains inside the existing sliced search budget.

Shared steering scans up to sixty-four onward samples / sixteen yalms of route arc for nearby loop exits, rather than sixteen samples / 4.4 yalms. It still attempts at most three shortcut collision checks, each for a complete connector no longer than 2.2 yalms. Dense samples no longer hide nearby onward points behind the old short arc limit. Physical clearance and floor checks remain authoritative.

No Limsa-specific coordinates or routes are added. This applies to map-guided native walking and shared ground steering in general. It does not guarantee that every route becomes one straight line or remove necessary obstacle turns.

Validation: plugin build, independent regression suite including open-floor hairpin removal, blocked-shortcut fallback, complete-edge validation and dense-loop steering, plus gate suite. In-game Limsa loop removal still needs verification.
