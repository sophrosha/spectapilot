# Settings

Everything you may want to change lives in the "Handler settings" window. The sections here mirror the window itself; hover any field for a tooltip. Don't forget: after "Save", restart `WheelControl` for the changes to apply.

## General

**Debug** — extra messages in the console. Off by default; turn it on only when digging into the log with someone.

**Smooth motion** — the wheel eases into a new target. Turn it off for an instant move, still capped by the force and speed limits. Advice: leave it on; turning it off is a test-only thing.

**Simulate** — the app runs without a real wheel. Use it only on a rig with no base.

**Smooth time, ms** — how long the wheel takes to reach the target. Advice: 300–800. Higher — softer motion but laggier; lower — faster response, snappier motion.

**Spring stiffness (Kp)** — how firmly the wheel holds a point. Advice: 1.0 is the baseline. Lower it for a softer hold with fewer clicks; raise it to hold stiffer. (In the window this field is shown under "General".)

**Damping (Kd)** — suppresses wheel oscillations while holding. Advice: 1.0 is the baseline. Don't lower it without reason: a weak damper brings the jitter back; raising it makes the approach stiffer and slower. (In the window — also under "General".)

## Wheel

**Max torque, %** — the motor's peak force. Advice: 100 is the maximum; lower it (not below 50) if the wheel yanks your hands too hard.

**Full lock, °** — how many game degrees fit into the base's full travel. 90 is one-to-one with the base physics, 180 doubles the range. Change it only if the in-game wheel moves at the wrong scale.

**Invert force** — flips the motor direction. Turn it on only if the very first test drives the wheel to the lock instead of the target. Normally off.

## Hold

**Hold force, %** — how firmly the wheel holds the chosen point. Advice: higher — harder to push away, lower — a weaker hold.

**Hold position, °** — where the wheel goes when hold engages and the current angle is unknown. Advice: touch it only if the starting hold position bothers you.

**Hold ramp, ms** — how smoothly the force ramps up when hold engages. Advice: if the wheel jerks at the moment of engage — raise it.

**Progressive hold** — the further you push the wheel, the harder it helps (up to 100%). Off — it holds evenly all the time.

**Lock nudge** — at the extreme position, soft rhythmic nudges instead of a hard hit against the stop. Like that feedback — keep it; don't — just switch it off.

## Move

**Max speed, °/s** — the cap on the angle change rate (anti-jerk guard). Advice: the wheel jitters — lower it.

**Move force limit** — the force with which the wheel travels to the target. Advice: slams into the target — lower it; arrives sluggishly — raise it.

**Max move speed, rpm** — motor RPM on the way to the target. Advice: acceleration too sharp — lower it.

## Socket

**Socket host** — where the app listens for the game. Advice: don't change it, keep `127.0.0.1` — that is this computer.

**Socket port** — the UDP port for angles from the game, 7777 by default. Change it only if the port is taken by another app (and change it in the mod too).

**Auto-start socket** — turn it on so the game connection comes up by itself at launch: nothing to type by hand.

**Invert socket** — the direction of the angle from the game. Advice: if the in-game wheel turns the wrong way — flip it.

**Mode: hold my wheel** — game points are held firmly, so you can safely hold the wheel. Off — the wheel eases to the point instead of springing.

**Socket smooth, ms** — responsiveness to the game. Advice: lower — faster and snappier, higher — softer but laggier; 50 is the baseline, 100–150 if it jitters.

**Data interval, ms** — an informational field; it changes nothing.

---
[← Back to contents](../../README.md)
