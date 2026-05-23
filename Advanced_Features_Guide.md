# Technical Implementation Guide: Advanced Features

This document provides the technical requirements and logic flow for implementing advanced features in the CS2GameHelper project.

> **Disclaimer:** This guide is for educational and research purposes. Implementing these features in live multiplayer games violates Terms of Service.

---

## 1. Standalone RCS (Recoil Control System)
Standalone RCS compensates for weapon kick without requiring a target.

### Key Offsets:
- `m_AimPunchAngle` (Vector3): The current screen shake/kick angle.
- `WeaponRecoilScale` (float): Usually `2.0` in CS2.

### Logic:
1. Every frame, read `localPlayerPawn + m_AimPunchAngle`.
2. Store the "previous" aim punch from the last frame.
3. Calculate the delta: `currentPunch - lastPunch`.
4. Convert the delta to pixels using the bot's `anglePerPixel` calibration.
5. Move the mouse by `-(delta * WeaponRecoilScale)`.
6. **Humanization:** Add a small delay (10-20ms) or scale the movement by `0.95` to avoid perfect compensation.

---

## 2. Glow ESP
Glow highlights entity models with a colored outline.

### Logic (External):
In CS2, "Glow" is often handled via the `GlowObjectManager` or by setting specific flags on the `SceneEntity`.
1. Find the `m_bGlowRuntimeControl` or `m_clrGlow` offsets within the entity classes.
2. Set `m_bGlowRuntimeControl = true`.
3. Write the desired color (RGBA) to the `m_clrGlow` memory address.
4. Set the `m_fGlowWidth` to define the outline thickness.

---

## 3. Backtracking
Backtracking allows you to hit enemies by shooting where they *were* up to 200ms ago.

### Logic:
1. **History Buffer:** Create a `Dictionary<int, List<TickRecord>>` where the key is the PlayerID.
2. **Recording:** Every game tick (approx 64/128 per second), store the enemy's position, bones, and simulation time.
3. **Cleaning:** Remove records older than 200ms.
4. **Targeting:** When the AimBot/TriggerBot is active, iterate through the history of the current target.
5. Find the "best" tick (e.g., the one closest to your crosshair or the last one where they were visible).
6. **Exploit:** Override the target position in the AimBot logic with the position from that historical tick.

---

## 4. Grenade Trajectory Helper
Predicts and draws the path of a grenade.

### Logic:
1. Detect if the local player is holding a grenade.
2. Get the player's `EyePosition` and `EyeDirection`.
3. **Physics Simulation:**
   - Initial Velocity: `EyeDirection * GrenadePower`.
   - Loop for `t = 0` to `3.0` seconds:
     - `position = startPos + (velocity * t) + (0.5 * Gravity * t^2)`.
     - Perform a `TraceRay` (memory-based collision check) to see if the point hits a wall.
     - If it hits, calculate the bounce vector and continue.
4. **Rendering:** Draw lines between the calculated points using the `Graphics` overlay.

---

## 5. Movement Features (Bhop/Strafe)
### Bhop:
1. Check if the Jump key is held.
2. Read `m_fFlags` from the local player.
3. If `(flags & FL_ONGROUND)` is true, write `+jump` to the game's input buffer or simulate a Space key press.

### Auto-Strafe:
1. While in the air, if the mouse moves left, simulate pressing 'A'.
2. If the mouse moves right, simulate pressing 'D'.
3. This synchronizes movement with camera rotation to maintain/increase air speed.
