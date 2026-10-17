# REWIND RAMPAGE

> **The past was never erased.**

[English](README.md) | [简体中文](README_CN.md)

**REWIND RAMPAGE** is a fast-paced, neon-styled 2D action game created for **Ludum Dare 60 (Compo)**, inspired by the theme **Old Tech**.

Move, leave a trail, and rewind through your own movements to fight back against relentless pursuers. But the machine is recording more than your survival: every second you leave behind may become something you have to face.

## The Game

Inside a malfunctioning, tape-based temporal system, time behaves like an old recording. It can be recorded, rewound, and played back—but it cannot always be erased.

The game has a three-act story mode and an expanding endless challenge.

## Story Mode

### Act I — RECORD

**Survive for 90 seconds.**

Enemy pursuers keep spawning as the pressure rises. Move across the arena, create your own path, and use rewind to turn your movements into attacks. During the final 30 seconds, the system records your actions for what comes next.

*Timer: `00:00 → 01:30` (counting up).*

### Act II — REWIND

**Hunt your past self in 30 seconds.**

A phantom faithfully replays the final 30 seconds of your Act I run. Intercept its recorded path while dodging projectiles aimed at the phantom's position. Watch its upcoming route, plan your attack, and eliminate it before playback ends.

*Timer: `00:00 → 00:30` (counting up).*

### Act III — ERASE

**Face the copies that were never deleted.**

The archive releases independent echoes of you. Their opening collision leaves you with just **1 HP**. Unlike the recorded phantom, these copies move freely and launch their own rewind attacks. Destroy the time core, then survive until the purge countdown reaches zero.

*Timer: `01:30 → 00:00` (counting down).*

## Endless Mode — THE TAPE NEVER STOPS

**The story teaches the rules. Endless Mode tests how far you can push them.**

Survive a sequence of phases built around dense pursuers, arena-wide hazards, and boss mechanics inspired by MMORPG raid encounters. Hazards are not merely obstacles: they can also destroy pursuing enemies.

### Rewind the Arena

Rewinding affects more than your character. Active temporal hazards rewind along their recorded timelines, and **replay their attacks when time moves forward again**. A blast can explode twice. A sweeping beam can strike again. Master the timing to turn dangerous mechanics into weapons.

Phase and mechanic timelines rewind alongside the player, so rewinding cannot skip an attack. The survival timer used during the endless boss encounter also moves backward with the player's rewind, preventing time from being farmed by repeatedly replaying the same moments.

### Opening Phases

| Phase | Encounter | What happens |
| --- | --- | --- |
| **P1 — BULLET STORM** | 30-second survival | Fight a concentrated wave of homing pursuers while the system records your actions. |
| **P2 — DETONATION** | Rewindable explosions | Surviving pursuers freeze in place, reveal their blast zones, and explode. Rewind far enough to restore their previous states and replay the detonation. |
| **P3 — MECHANIC RUSH** | 30-second mechanics introduction | A non-combat phantom replays your P1 movements as you encounter **TRACK LOCK**, **HEAD SWEEP**, **BAD SECTORS**, and **OVERWRITE**, with only a few pursuers to distract you. |

### P4 and Beyond — ENDLESS BOSS

From **P4**, an enormous magnetic cassette becomes the boss. It falls under gravity, lands with a heavy screen shake, and moves through weighty jumps. Its landings are dramatic, **but do not deal damage in P4**.

The first boss phase introduces **TRACK LOCK** and **HEAD SWEEP** individually, then combines them into a single sequence. You advance only after completing all the mechanics—not when a fixed timer expires.

The boss **cannot be defeated**. Later phases will extend the encounter, eventually looping from the final boss phase back to P4. The challenge ends only when the player is defeated.

*The detailed P4 encounter is designed; later boss phases and the full endless loop are still in development.*

## Core Mechanics

- **Move and trace:** Your movement leaves a path through the arena.
- **Rewind to attack:** Retrace your route at high speed and use your recorded movement offensively.
- **Exploit intersections:** Use intersecting trails to trigger powerful attacks.
- **Fight your own history:** Your recorded actions become the opponent in Story Mode's Act II.
- **Survive the archive:** In Act III, copies of you can rewind and attack independently.
- **Rewind the battlefield:** In Endless Mode, replay arena hazards to dodge danger or eliminate pursuers.
- **Read and resolve mechanics:** Recognize telegraphs, find safe routes, and exploit raid-style attacks.

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

**REWIND RAMPAGE is actively in development.** The core movement, pursuer, rewind, and visual systems have a playable prototype. The three-act story and the opening Endless Mode phases have been designed and are being implemented and refined. The first endless boss phase (P4) has a detailed technical design; the later repeating boss phases are planned, not yet finalized.

## Built With

- **Unity** — game engine
- **C#** — gameplay and systems

## Game Jam

Created for **Ludum Dare 60 (Compo)** — **Old Tech**.

---

*Record. Rewind. Erase. Repeat.*