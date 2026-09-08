# Changelog

Entries are in the order they were written, not strictly version order.

## v0.1.1 additions

### Remote Bomb / Grenade steering

When a normal vanilla Bomb or Grenade is thrown while `TK REMOTE` is ON, keep item-use held to steer that projectile toward the cursor. Releasing item-use returns it to normal ballistic movement. Toggling remote mode OFF also disables steering. The vanilla fuse continues counting down while controlled.

`/tkexplosives` adds one Bomb and one Grenade.

## v0.1.13 fix

- Bomb/Grenade steering now reads the raw held-left-click trigger instead of `Player.controlUseItem`, preventing intermittent failure to begin steering when the throw click remains held continuously.

## v0.1.2 additions

- Levitation duration changed to the requested 100/150/200/250/300/350/400/450/500 HP progression.
- Ground contact no longer instantly refills levitation.
- Finite levitation recharges continuously whenever levitation is toggled OFF, at a 1:1 time rate.

## v0.1.4 additions

- Levitation now owns horizontal movement while active instead of inheriting vanilla walking acceleration/speed.
- Fixed horizontal acceleration: **0.35 px/tick²**.
- Fixed horizontal speed cap: **55 tiles/s** (about 75 mph), intentionally below the ~61.6 tiles/s / 84 mph Soaring Insignia + Celestial Starboard hover peak.
- No horizontal input brakes toward zero using the same fixed acceleration.
- Boots, sprint bonuses, and wings no longer change levitation's horizontal movement while levitation is ON.


## v0.1.4 changes

- Remote broadsword/tool melee collision is now decided from the remote hitbox and remote grip, not the physical player. This is intended to fix NPCs becoming unhittable when pressed against walls/doors while still preventing hits through solid terrain.
- Levitation horizontal acceleration reduced from 0.35 to 0.20 px/tick^2.
- Levitation horizontal cap reduced from 55 to 48 tiles/s.
- Levitation is now true suspension: with no vertical input, vertical velocity is immediately zero; with no horizontal input, horizontal velocity is immediately zero. Direction keys create movement, so levitation acts as a precision-positioning mode even after faster endgame flight becomes available.


## v0.1.6 changes

- Remote broadswords no longer rely on Terraria's final physical-player `CanHit` tile check after the remote grip collision has already succeeded.
- Wall blocking is still decided from the remote grip to the target.
- Shortsword/projectile behavior is unchanged.


## v0.1.7 changes

- Levitation activation now preserves existing jump/fall/horizontal momentum and decelerates that inherited momentum using levitation acceleration instead of snapping velocity to zero.
- Once inherited momentum has been absorbed, normal true-hover behavior is unchanged: releasing levitation-created movement stops immediately.


## v0.1.10 changes

- Added remote support for melee spear/lance-style weapons whose main projectile uses vanilla spear AI.
- Vanilla spear thrust timing/reach is preserved; the completed spear pose is translated from the player to the remote grip.
- Spear owner LOS is replaced with the same grip-to-target wall check used by other remote melee.
- `/tktest` now also grants Spear, Trident, and Jousting Lance for testing.

## v0.1.12 changes

### Enemy manipulation

- Added a separate hold-to-use enemy manipulation ability, default **V**.
- Hold V while the cursor is over an eligible hostile NPC to grab it; once grabbed, the same NPC remains selected until V is released or the target becomes invalid.
- The grabbed enemy is accelerated toward the cursor rather than teleported or hard-positioned.
- Base manipulation acceleration is **0.50 px/tick² × NPC.knockBackResist**.
- NPCs with zero knockback resistance are immune and cannot be selected.
- Manipulation speed is capped at **10 px/tick**, with smooth braking near the cursor.
- Enemy manipulation is independent of the G remote-item toggle.

### Shared sustained-use charge

- Enemy manipulation uses the **same HP-scaled charge pool** as self-levitation.
- Using either ability drains the shared pool at the existing rate of one tick per tick.
- Using both simultaneously still drains one tick per tick; they are powered by one shared sustained-use timer rather than two independent costs.
- The pool recharges at the existing 1:1 rate only when neither sustained ability is using it (with the existing mounted-levitation behavior preserved).
- When the finite pool is exhausted, manipulation releases and requires the V key to be released before it can be grabbed again.


## v0.1.14.2 fix

- Fixed compact HUD build error by allowing `FontAssets.MouseText.Value` to retain its tModLoader `DynamicSpriteFont` type instead of incorrectly assigning it to XNA `SpriteFont`.

## v0.1.14.4 changes

- Ordinary visible direct-melee swings may now remain remote-controlled even when the weapon also emits a projectile.
- Cursor-aimed projectiles launched from near the player are relocated to the telekinetic grip and automatically redirected toward the nearest visible hostile NPC with grip-based line of sight.
- Existing projectile speed and angular spread are preserved by rotating vanilla velocity rather than replacing it.
- Unusual projectile spawn patterns that are not conventional player-launched cursor shots are left unchanged for targeted compatibility later.
- Yoyos, boomerangs, flails and other noUseGraphic/noMelee/channel melee families remain excluded.
- `/tktest` now also grants Ice Blade, Enchanted Sword, and Starfury as representative projectile-emitting sword cases.

## v0.1.14.5 changes

- Shared levitation/enemy-manipulation charge now ramps sharply near endgame: 450 HP = 12s, 460 = 15s, 470 = 20s, 480 = 30s, 490 = 60s, 500 = infinite; finite values interpolate linearly and 490-499 continues the +3s/HP slope.
- Fixed remote direct-melee/tool visuals on rotated mounts such as minecarts travelling on 45-degree track: the remote item draw no longer inherits the player's mount rotation, while collision/gameplay remain unchanged.
- Added `/tkextractinator`, which gives one Extractinator for testing.
- Remote explosive steering now applies to directly-used consumable explosive projectiles generically instead of only Bomb and Grenade. This covers thrown Bomb/Grenade/Dynamite variants, Scarab Bomb, liquid/dirt bombs, Bomb Fish, Beenades, etc., while excluding launcher ammunition such as rockets/mines.

## v0.1.14.6 changes

- Replaced the piecewise levitation/shared-charge duration breakpoints with a smooth convex curve.
- 100-400 HP follows continuous exponential growth (1s → 2s → 4s → 8s every +100 HP).
- 400-485 HP accelerates smoothly further to 30 seconds at 485 HP.
- 485-489 HP is capped at 30 seconds.
- 490+ permanent max HP now unlocks infinite sustained telekinesis.


## v0.1.14.7

- Replaced the two-stage finite levitation/shared-charge curve with one exponential formula across 100-485 permanent HP: `30 ^ ((HP - 100) / 385)` seconds.
- 100 HP remains exactly 1 second; 485 HP is exactly 30 seconds; 490+ HP remains infinite.


## v0.1.14.8 changes

- Vertical levitation acceleration increased slightly from **0.50 to 0.60 px/tick²**; vertical top speed is unchanged.
- While levitation duration is finite (<490 permanent HP), Left Shift is now **hold-to-levitate**. Releasing Shift ends levitation and allows the shared charge to recharge.
- If finite charge is exhausted while Shift remains held, levitation stays locked out until Shift is released, preventing one-tick restart/recharge jitter.
- At 490+ permanent HP, where levitation is infinite, Left Shift automatically uses **toggle** behavior instead.
- Crossing into infinite levitation while already levitating preserves the current ON state.

## v0.1.14.9 changes

- Normal vertical levitation acceleration remains **0.60 px/tick²**.
- Opposing vertical momentum now brakes at **1.20 px/tick²** until vertical velocity reaches zero.
- After reversal reaches zero, acceleration in the newly requested direction returns to the normal 0.60 px/tick².
- Releasing vertical input while levitating still immediately holds altitude; vertical top speed is unchanged.



## v0.1.14.10 fix

- Fixed Up + finite hold-to-levitate becoming stuck at zero vertical velocity after the stronger reversal-braking change.
- When 1.20 px/tick² reversal braking reaches zero, the same tick now continues into the normal 0.60 px/tick² acceleration in the requested direction.
- Applied symmetrically to upward and downward reversal; top speeds and other movement behavior are unchanged.

## v0.1.14.11 changes

- Removed the separate 1.20 px/tick^2 vertical braking/reversal path introduced in v0.1.14.9-10.
- Vertical levitation now uses one acceleration value for ascent, descent, and activation momentum transition.
- Vertical acceleration increased to **0.80 px/tick^2**.
- Vertical top speed remains unchanged.


## v0.1.14.12 fix

- Fixed remote pickaxes/axes/hammers dealing no damage to enemies when the cursor was over a solid tile.
- The tool's melee hitbox and visible swing still stay exactly at the cursor. Only the wall-blocking check moved: when the cursor grip is inside solid terrain, obstruction is now probed from the open point the tile operation was already authorized from.
- No effect when the cursor is in open space. Walls still block remote melee.

## v0.1.14.13 fix

- Fixed thrown explosive steering being driven by the global left-button state instead of the throw that created it.
- Steering now belongs only to the uninterrupted left-click hold that threw the explosive. Releasing left click permanently returns that projectile to vanilla ballistic motion.
- This stops an already-released bomb from resuming steering on a later unrelated click, and stops in-flight bombs from being steered while an unrelated weapon is being swung.
- Fuse/explosion timing, collisions, and vanilla explosive AI are unchanged.

## v0.1.14.14 optimization

- Reachability rebuilds no longer re-probe the same solid tile once per adjacent open tile. Each tile's solidity is now tested at most once per rebuild.
- The flood-fill queue is reused instead of being allocated on every rebuild.
- No gameplay change: the reachable region, the visible-screen boundary, and rebuild frequency are all unchanged.

## v0.1.14.15 optimization

- Grip path smoothing now scans forward from the current waypoint instead of backward from the end of the path, reducing it from one full visibility pass per kept segment to a single pass overall.
- Long paths around corners no longer cost thousands of line-of-sight checks each time the cursor moves to a new tile.
- Forward scanning stops at the first blocked waypoint rather than jumping to a later visible one, so a smoothed path may keep a few more waypoints than before. Grip routes and all reachability rules are otherwise unchanged.

## v0.1.15 additions

- Remote Terragrim/Arkhalis support. Vanilla held-projectile AI is left intact and its finished pose is translated to the grip, with grip-anchored wall blocking and knockback. Aim auto-targets the nearest enemy within 25 tiles of the grip and falls back to the cursor direction.
- Starfury/Star Wrath stars now land on the nearest visible enemy instead of the cursor. The entire vanilla spawn is offset by cursor-to-target, preserving fall angle, speed, and per-star spread. No target means vanilla cursor behavior. Line-of-sight is not required, since vanilla stars fall through blocks.
- Remote bullet guns: fired from the grip, auto-aimed at the nearest visible enemy, with the held weapon rotated to match the shot. Bows, magic weapons, and non-bullet guns remain vanilla.
- Auto-target selection is now one shared helper used by every remote weapon that auto-aims.

## v0.1.16 additions

### Telekinetic chest range

While `TK REMOTE` is ON, chests and dressers can be opened and used at telekinetic range instead of vanilla arm's length. A container counts as in range when a reachable grip position exists within the player's normal tile interaction range of it, which is the same rule tools and placement already use: connected open space within the visible screen area.

An open chest stays open as long as its own tile remains telekinetically in range, so the cursor is free to move around the inventory. Remote OFF restores fully vanilla container range. Piggy Bank/Safe-style item containers are unchanged.

Because vanilla evaluates all tile interactions against one shared range value, other right-click tiles (doors, levers, signs) are also reachable during the same window.

