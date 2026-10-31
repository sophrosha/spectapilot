# Configuration

The file — `Assets/config.json` next to the exe. If it is missing, it is created with the defaults; if it is broken, the console starts on the defaults and prints a warning. Values are clamped to their valid ranges on load (a guard against a hand-broken file).

**The defaults in the table come from the code** (`Config.cs`): the `assets/config.json` file in the repository is the author's working example with his tunes, not the reference defaults. Changes take effect after `WheelControl` restarts; `WheelControlSettings` preserves the key order and any unknown fields as-is.

The sections here are thematic; in the settings window the fields follow the file's sections: `kpScale`/`kdScale` fall into "General" (that is how keys missing from the sections behave), `maxDegPerSec` — into "Move", `dataIntervalMs` — into "Socket".

## General

| Key | Default (from code) | What it does | When to change |
|---|---|---|---|
| `smooth` | `true` | "Smooth motion": the target is approached smoothly; `false` — instantly (speed and torque are limited anyway). | `false` only to test a snappy response. |
| `smoothTimeMs` | `500` | "Smooth time": the target covers ~95% of the way in this time, ms (50–10000). | 300–800: higher — smoother and laggier, lower — snappier. |
| `simulate` | `false` | "Simulate": the SDK is never touched, the app runs without a wheel (same as `--simulate`). | `true` for a rig without a base. |
| `maxDegPerSec` | `720.0` | Anti-jerk guard: maximum angle change rate, °/s (30–5000). | The wheel jitters — lower it. |
| `dataIntervalMs` | `33` | Expected packet interval from the mod, ms (~30 Hz). Informational/debug only (5–1000). | No need to change. |
| `debug` | `false` | Print DEBUG messages in the console (read directly from the JSON, not through the config class). | `true` while troubleshooting. |
| `uiLanguage` | `"ru"` | UI language of the settings app and console texts; written by the Language menu. | Don't edit by hand — pick it from the menu. |

## Wheel

| Key | Default (from code) | What it does | When to change |
|---|---|---|---|
| `maxTorque` | `100.0` | "Max torque": peak torque, % of maximum (hardware limit 50–100). | Feels too brutal — lower it (not below 50). |
| `userFullLockDeg` | `90.0` | "Full lock, °": full wheel travel in user degrees (10–720). 90 = 1:1 with the base physics, 180 = twice the range. | The mod sends a different range, or you want a different "game angle ↔ base travel" mapping. |
| `forceInvert` | `false` | "Invert force": torque sign — with `true` the wheel runs to the lock instead of the target (the sign depends on the base model/firmware). | Only if the very first test drives the wheel the wrong way. |

## Hold

| Key | Default (from code) | What it does | When to change |
|---|---|---|---|
| `holdForce` | `50.0` | "Hold force, %": how hard the wheel holds its position (0–100). | Higher — harder to push away, lower — weaker. |
| `holdPosition` | `90.0` | "Hold position, °": where to hold when the current angle is unknown (−3600–3600). | Rarely — the starting position of `hold`. |
| `holdRampMs` | `300` | "Hold ramp, ms": force ramp-up time when hold engages, so it doesn't jerk (0–5000). | Jerks on engage — raise it. |
| `progressiveHold` | `true` | "Progressive hold": the further you deviate, the more smoothly the force rises to 100%. | `false` — always hold hard. |
| `lockNudge` | `true` | "Lock nudge": at full lock, soft rhythmic nudges of 6–12% force (~1.2 Hz) instead of slamming into the stop. | `false` if you don't like the nudges. |
| `kpScale` | `1.0` | "Spring stiffness (Kp)": multiplier (0.1–3); 1.0 = full force at 8° of error. | Lower — softer with fewer stick-slip clicks, higher — stiffer. |
| `kdScale` | `1.0` | "Damping (Kd)": multiplier (0.1–3); below 1.0 it weakens the ~15 Hz wave suppression (the jitter returns), above 1 — a stiffer damper and a slower approach (max speed = 180/KdScale °/s). | Keep 1.0; lower it only consciously. |

## Move

| Key | Default (from code) | What it does | When to change |
|---|---|---|---|
| `moveForceLimit` | `1500.0` | "Move force limit": force on the way to the target, DirectInput scale 100–10000 (800 in the SDK example). | Slams to the target — lower it; sluggish — raise it. |
| `maxMoveSpeedRpm` | `90.0` | "Max move speed, rpm": motor speed limit while moving to the target (5–300; 160 in the SDK example). | Acceleration too sharp — lower it. |

## Socket

| Key | Default (from code) | What it does | When to change |
|---|---|---|---|
| `socketHost` | `"127.0.0.1"` | "Socket host": the UDP server address, localhost only (security); an empty value falls back to `127.0.0.1`. | Don't change. |
| `socketPort` | `7777` | "Socket port": UDP port for angles from BeamNG (1–65535). | The port is taken by another app. |
| `socketAutoStart` | `false` | "Auto-start socket": the server comes up by itself at console start. | `true` — no need to type `socket` by hand. |
| `invertSocket` | `true` | "Invert socket": sign of the angle from the game (does not affect the console). | The in-game wheel turns the wrong way — flip it. |
| `socketHandsMode` | `true` | "Mode: hold my wheel": game positions are held by a hard spring with `holdForce` — you can safely hold the wheel; engages on the first packet. | `false` — a soft approach to the target instead of a spring. |
| `socketSmoothTimeMs` | `50` | "Socket smooth, ms": how fast a target from the game is caught up (10–5000). | Lower — faster but snappier, higher — smoother with delay. |

---
[← Back to contents](../../README.md) | [Next section →](04-classes.md)
