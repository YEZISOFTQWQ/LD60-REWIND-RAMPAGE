# ACT III opening v1.1 — TEMPORAL COLLISION

This direct user amendment replaces the opening in the Act III v1.0 story script.

- Duration: 4–5 seconds; keep the countdown at 01:30 throughout.
- Initialize the player at 3 HP. Hold movement, rewind recording and all combat AI.
- Use independent visual phantoms around the arena, with no combat colliders or damage registration.
- Display RESIDUAL INSTANCES DETECTED / ORIGINAL RECORD UNAVAILABLE / ALL INSTANCES MARKED FOR ERASURE.
- Turn the phantoms toward the operator. At about one second, charge on straight paths with one shared arrival time.
- At the collision, show a strong cyan/red flash and horizontal tracking displacement.
- Apply one guarded story damage event for exactly two HP. It bypasses combat immunity and cannot repeat.
- Animate the HUD from 3 to 2 to 1, with about 0.15 seconds between the visible losses; actual health is already 1 after the one story event.
- Display TEMPORAL COLLISION / OPERATOR INTEGRITY: CRITICAL / REMAINING INTEGRITY: 1/3.
- Scatter the visual phantoms into red/magenta afterimages and remove them.
- Display EMERGENCY PURGE INITIATED / PURGE CYCLE: 90 SECONDS / SURVIVE.
- Only then start the 90-second clock and the first two combat Echoes. Grant one second of spawn protection.
- Thereafter, one effective enemy hit kills the remaining 1-HP operator (the existing optional F2 debug god mode remains available and is off by default).
- Echo targets are 2 / 3 / 4 over 90–60 / 60–30 / 30–0 seconds. The core appears only at 30 seconds, has three integrity layers and must be destroyed before surviving to zero.
- All retries, TEST ACT III and F3 must run the same full initialization and collision sequence.
- Later timed story cues, core-destruction survival requirement and the final ending follow the complete Act III v1.0 script.

## Latest user correction (2026-10-17)

The original population values above are superseded by the user's follow-up: retain the previous combat density, with 6 / 10 / 14 active Echoes and 3 / 5 / 7 simultaneous warning/attack slots. The opening therefore starts six combat Echoes once its cinematic ends. Attack timings and replenishment remain at their previous accelerated settings.

Opening phantoms and combat Echoes are the same kind of instance in the story. Both use `EchoVisual` for the identical triangle, ring, additive halo, four-color palette and warning/rewind styling. Only the opening instances lack AI, damage registration and recording access. The guarded 3-to-1 HP event and all opening timing remain unchanged.
