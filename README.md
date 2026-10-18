# REWIND RAMPAGE

> **The past was never erased.**

[English](README.md) | [简体中文](README_CN.md)

**REWIND RAMPAGE** is a fast-paced, neon-styled 2D action game created for **Ludum Dare 60 (Compo)**, inspired by the theme **Old Tech**.

Move, leave a trail, and rewind through your own movements to fight back against relentless pursuers. But the machine is recording more than your survival: every second you leave behind may become something you have to face.

## The Game

Inside a malfunctioning, tape-based temporal system, time behaves like an old recording. It can be recorded, rewound, and played back—but it cannot always be erased.

Play through a three-act story, then take on an endless, escalating boss encounter that turns your own greatest tool—rewinding time—against you.

## Story Mode

### Act I — RECORD

**Survive for 90 seconds.**

Enemy pursuers keep spawning as the pressure rises. Move across the arena, create your own path, and use rewind to turn your movements into attacks. During the final 30 seconds, the system records your actual actions for what comes next.

*Timer: `00:00 → 01:30` (counting up).*

### Act II — REWIND

**Hunt your past self in 30 seconds.**

A phantom faithfully replays the final 30 seconds of your Act I run. Intercept its recorded path while avoiding projectiles aimed at the phantom's position. These shots travel straight after being fired, so timing and positioning matter. The phantom's upcoming path is briefly visible to help you plan your strike.

*Timer: `00:00 → 00:30` (counting up).*

### Act III — ERASE

**Face the copies that were never deleted.**

The archive releases independent echoes of you. Their opening collision leaves you with just **1 HP**. Unlike the recorded phantom, these copies act on their own and launch telegraphed rewind attacks.

When the time core appears with 30 seconds remaining, land **three valid hits** to destroy it. Breaking the core is not enough: you must also survive until the purge countdown reaches zero. The final blow triggers a dramatic freeze-frame, close-up, and explosion—then the fight continues.

*Timer: `01:30 → 00:00` (counting down).*

## Endless Mode — THE TAPE NEVER STOPS

**The story teaches the rules. Endless Mode tests how far you can push them.**

Survive a sequence of phases built around homing pursuers, arena-wide hazards, and mechanics inspired by MMORPG raid encounters. The same hazards that threaten you can also destroy pursuing enemies. Read their patterns, create a safe route, and use time itself as a weapon.

### Rewind the Arena

Rewinding affects more than your character. Active temporal hazards move backward along their recorded timelines and **replay their attacks when time moves forward again**. A detonation can happen twice. A sweeping beam can strike again.

Hazards do not inflict damage while traveling backward. Their phase clocks and attack schedules rewind too, so reversing time cannot simply skip an unfinished mechanic. Defeated enemies, lost HP, and earned score do not generally rewind; P2 has a special rule for restoring surviving pursuers to their pre-detonation states.

Starting at P4, a dedicated boss survival timer begins at `00:00.00`. It runs forward during normal play and **moves backward during rewinds**, including boss-forced rewinds. Repeating the same moment cannot be used to farm elapsed time.

### Opening Phases

| Phase | Encounter | What happens |
| --- | --- | --- |
| **P1 — BULLET STORM** | 30-second survival | Face a concentrated wave of homing pursuers while your actual movements are recorded for P3. |
| **P2 — DETONATION** | Rewindable mass explosion | The remaining P1 pursuers stop, reveal their blast zones, and explode together. Rewind far enough to restore their earlier positions and replay the event. Pursuers defeated before P2 do not return. |
| **P3 — MECHANIC RUSH** | 30-second mechanics introduction | A **purely visual, non-combat** phantom replays your P1 movements as four arena hazards are introduced in sequence, with very few pursuers to distract you. |

P3 introduces the four mechanics used throughout the boss encounter:

- **TRACK LOCK** — A targeting circle follows you, locks onto a position, then detonates.
- **HEAD SWEEP** — A sweeping beam crosses the arena, leaving a readable safe gap.
- **BAD SECTORS** — Marked grid cells detonate in successive waves.
- **OVERWRITE** — A dangerous region advances across the arena and lingers behind it.

### P4–P7 — THE ENDLESS BOSS

From **P4** onward, one enormous magnetic cassette becomes the boss. It drops into the arena, makes weighty jumps, and shakes the screen when it lands. **Its jumps, landings, and physical presence are not contact-damage attacks**; the danger comes from its arena mechanics and the pursuing enemies.

The cassette **has no HP and cannot be defeated**. P4 through P7 are attack phases of the same continuous encounter, not separate boss fights. Each phase advances only after its required attack patterns have actually finished—not after a fixed amount of real time.

| Phase | Title | First-loop challenge |
| --- | --- | --- |
| **P4** | **MAGNETIC IMPACT** | Learn **TRACK LOCK** and **HEAD SWEEP** separately, then survive their overlapping combination. |
| **P5** | **BAD SECTORS + OVERWRITE** | Handle **BAD SECTORS** and **OVERWRITE**, individually and then together. |
| **P6** | **HEAD SWEEP + BAD SECTORS** | Survive **HEAD SWEEP** and **BAD SECTORS**, including an overlapping sequence that demands precise routing. |
| **P7** | **FINAL OVERWRITE** | Survive **TRACK LOCK + BAD SECTORS**, **HEAD SWEEP + OVERWRITE**, and a three-mechanic combination before the boss's **MEMORY LOCK** attack. |

### MEMORY LOCK — REMEMBER.

In P7, the boss first shows you an attack with its normal warnings. After you survive it, the cassette **seizes control and forcibly rewinds time**—including your character, who travels backward along the movement path you actually took.

The rewind reaches a point **before the first warning appeared**, giving you time to reposition. Control returns, but the **same attack repeats without advance danger-zone or safe-zone warnings**. The attack layout, order, and timing remain the same. You have seen the answer once; now you must remember it.

The attack itself remains visible when it actually takes effect. A MEMORY LOCK cannot recursively trigger itself, and the boss cannot erase damage you have already taken.

### LOOP 02+ — OVERLOAD LOOP

Clearing P7 sends you straight back to P4. The cassette does not respawn, the survival timer does not reset, and the encounter continues until you fall.

From **LOOP 02 onward**, every P4–P7 phase uses **complex overlapping combinations only**—no more standalone introductory attacks. **Every phase ends with a boss-forced rewind attack:** you see a combination once with its warnings, are rewound along your own recorded path to before it started, and must survive the same combination again without advance warnings.

The number of homing pursuers created by **each spawn event** also triples with each loop, up to **81 pursuers per event**:

| Boss loop | Pursuers per spawn event |
| --- | ---: |
| LOOP 01 | 1 |
| LOOP 02 | 3 |
| LOOP 03 | 9 |
| LOOP 04 | 27 |
| LOOP 05 and beyond | 81 |

**81 is a per-spawn-event limit, not a cap on enemies alive in the arena.** Previously spawned pursuers can remain on screen alongside new ones. Their movement speed and damage do not triple with the spawn count.

If you rewind across a phase or loop boundary, the phase, loop number, attack progress, and corresponding spawn multiplier return to the appropriate point in the recorded timeline. The boss encounter still ends only when the player is defeated.

## Core Mechanics

- **Move and trace:** Your real movement leaves a route through the arena.
- **Rewind to attack:** Rush backward along that recorded route to strike pursuers.
- **Exploit intersections:** Crossing your recorded trails can trigger explosive attacks.
- **Fight your own history:** Act II makes your last 30 seconds of movement the target.
- **Survive the archive:** Act III's independent echoes can move and rewind on their own.
- **Rewind the battlefield:** In Endless Mode, reverse and replay arena hazards to survive or eliminate enemies.
- **Read complex attacks:** Learn overlapping raid-style patterns—and eventually repeat them from memory without warnings.

## Controls

| Action | Input |
| --- | --- |
| Move | `W` `A` `S` `D` |
| Rewind | `Space` |
| Other actions | See the in-game controls |

## Visual Style & Theme

The game combines a dark 2D grid arena, cyan movement trails, red pursuers, glowing geometric effects, vintage digital displays, and magnetic-tape machinery.

The **Old Tech** theme is built into the world: the time system operates like an aging magnetic-tape recorder. Its failed erase mechanism leaves behind copies that should no longer exist. **RECORD**, **PLAY**, **REWIND**, and **ERASE** are both machine operations and parts of the gameplay.

## Development Status

**REWIND RAMPAGE is under active development and refinement.** This README describes the designed gameplay and intended encounter structure; the exact content and balancing available in a particular build may vary.

## Built With

- **Unity** — game engine
- **C#** — gameplay and systems

## Game Jam

Created for **Ludum Dare 60 (Compo)** — **Old Tech**.

---

*Record. Rewind. Erase. Repeat.*
