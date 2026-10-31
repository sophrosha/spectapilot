# FAQ / Troubleshooting

Format: **Problem** → **Cause** → **Fix**.

## The wheel does not connect

**Problem:** the console reports that the base is not found, or throws an SDK error at startup.

**Cause:** Pit House is not running, the base is not connected to Pit House over USB, or another process is already holding the base.

**Fix:** start Pit House and wait for the base status; close other programs that use the wheel (SimPro / other Moza utilities); close extra `WheelControl` instances and restart it.

## Access denied / port busy

**Problem:** starting the socket gives "Access denied", "address already in use", or the UDP server stays silent.

**Cause:** port 7777 is taken by a second console instance or a third-party app; or the Windows firewall blocks incoming UDP.

**Fix:** the app closes duplicate instances by itself — just run a single one; if a foreign app owns the port, change the "Socket port" in the settings (and the address in the mod); allow incoming UDP for `WheelControl.exe` in the firewall.

## The wheel does not move

**Problem:** messages from the game arrive (or not), but the wheel stays still.

**Cause:** the UDP server is not started (by default it takes the `socket` command if "Auto-start socket" is off); the BeamNG mod is not installed; "Simulate" is enabled (`simulate: true` — the SDK is never touched); the base is not connected to Pit House.

**Fix:** type `socket` and `status` in the console — the server should be up; check the mod and the port; turn "Simulate" off; check the base connection. If packets are there (`status` moves) but the wheel is dead — see "The wheel does not connect".

## The wheel jitters, hums or vibrates

**Problem:** while moving or holding, the wheel shakes, hums or "gears up" on small angles.

**Cause:** a too-weak damper or a too-stiff spring (stick-slip clicks), a too-snappy target from the game ("Socket smooth" too low), "Lock nudge" near the stop, a too-high speed cap.

**Fix:** start with `kdScale` = 1.0 and `kpScale` = 1.0 (lower kp if it clicks); raise "Socket smooth" (50 → 100–150 ms); lower "Max speed, °/s" for jerks; record a CSV log with `log 10` in the console and inspect it. "Lock nudge" can simply be turned off in the settings.

## No packets arrive from the socket

**Problem:** no motion in the console, `status` never changes, while the wheel works by commands.

**Cause:** the UDP server is not started; the mod sends to the wrong address/port (it must be `127.0.0.1:7777`); the firewall; the mod is not active in the current game session.

**Fix:** `socket` → `status` in the console; match the port in the settings with the mod's settings; allow incoming UDP; rejoin the session after installing the mod. The "Data interval" field affects nothing — it is informational.

## Pit House conflicts

**Problem:** the wheel is "torn away" from the app: the connection drops, the device disappears.

**Cause:** a second `WheelControl` instance, another Moza utility or a game holds the HID session — Pit House gives the base to a single consumer.

**Fix:** keep a single instance (the console closes extras itself at startup), close third-party Moza utilities, replug the base in Pit House and only then start the handler.

## Settings saved but nothing changed

**Problem:** the settings window says "Saved", yet the wheel behaves the same.

**Cause:** `WheelControl` reads `config.json` at startup; a running instance does not pick up changes.

**Fix:** restart `WheelControl` (the status line in the settings window reminds you — "Saved. WheelControl is running…"). If the settings window refuses to start with a message about `config.json` — the file is in the wrong place: next to the exe there must be an `Assets` folder holding it.

---
[← Back to contents](../../README.md) | [Next section →](06-licensing.md)
