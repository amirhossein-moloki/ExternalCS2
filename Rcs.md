# RCS (Recoil Control System) — Detailed Design

The RCS feature provides standalone recoil compensation (No Recoil) that works independently of the AimBot. It counteracts the weapon kick by moving the mouse in the opposite direction of the aim punch.

## Files
- **Implementation**: `Features/Rcs.cs`
- **Configuration**: `Utils/ConfigManager.cs` (`RcsConfig`)

## Logic
1. **Shots Fired Check**: Only runs when the player is actively shooting (`ShotsFired > 0`).
2. **Punch Delta**: Calculates the change in `AimPunchAngle` between frames.
3. **Conversion**: Converts the angular delta (degrees) to pixel offsets using the calibrated angles-per-pixel.
4. **Compensation**: Applies a multiplier (Global or Weapon-specific) and moves the mouse in the opposite direction.
5. **Humanization**: Adds slight randomization (95-105%) to the movement to mimic human variance.

## Configuration
- `enabled`: Global toggle for the service.
- `globalScale`: The default compensation multiplier (typically 2.0).
- `weaponScales`: A dictionary for weapon-specific overrides (e.g., `"Ak47": 2.2`).

## Interaction with AimBot
The RCS service is designed to be legit. It runs as a background service and applies corrections based on raw memory values. When the AimBot is also active, they both contribute to mouse movement.
