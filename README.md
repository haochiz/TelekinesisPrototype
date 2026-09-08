# Telekinesis Prototype v0.1.7

Single-player prototype for the telekinetic character. This source was not compiled or run by the authoring environment.

## v0.1.0: levitation

Levitation is now included as a separate innate movement ability.

### Controls

- **Left Shift** toggles levitation ON/OFF by default. It is rebindable in tModLoader Controls.
- Levitation is a toggle, not a hold action.
- While levitation is ON:
  - **Up or Jump** rises.
  - **Down** descends.
  - no vertical input automatically brakes vertical velocity toward a hover.
  - **Left/Right** use levitation's own fixed horizontal acceleration and speed cap.
  - releasing horizontal input brakes toward a stationary hover.
- Left Shift normally activates vanilla Smart Select. While Left Shift is assigned to levitation and being pressed, levitation takes priority and Smart Select is suppressed for that press.

### HP progression

**Duration is the only stat that scales with permanent max HP.** Acceleration/control are fixed at every stage.

Finite duration now uses one smooth exponential formula for the entire finite HP range:

`seconds = 30 ^ ((permanentMaxHP - 100) / 385)`

- 100 HP: **1.0 s**
- 150 HP: **~1.6 s**
- 200 HP: **~2.4 s**
- 250 HP: **~3.8 s**
- 300 HP: **~5.9 s**
- 350 HP: **~9.1 s**
- 400 HP: **~14.2 s**
- 425 HP: **~17.7 s**
- 450 HP: **~22.0 s**
- 475 HP: **~27.5 s**
- 485 HP: **30.0 s**
- 490 HP: **infinite**

The curve is continuously convex: duration rises faster and faster with HP without any internal breakpoint or formula change. 485-489 is capped at 30 seconds, then 490 HP unlocks infinite sustained telekinesis.

Finite levitation now behaves as a rechargeable resource:

- while levitation is **ON**, remaining time drains continuously;
- while levitation is **OFF**, it recharges at **1 second of levitation per 1 second OFF**;
- recharge does **not** require touching the ground, so running, falling, boots, wings, mounts, etc. can coexist with recharge as long as levitation itself is toggled OFF;
- touching the ground does not instantly refill the resource;
- if the timer reaches zero, levitation switches OFF and immediately begins recharging.

Eating a Life Crystal/Fruit still grants the additional capacity unlocked by the new permanent max HP without erasing already-spent duration.

The HUD shows `TK LEVITATE: ON/OFF`, the assigned key, and remaining duration (`INF` at 490 HP).

While levitation is ON, it owns horizontal movement as well as vertical movement. Horizontal acceleration and top speed are fixed and do not inherit walking/sprint/boots/wing movement stats. Current playtest values are **0.35 px/tick² acceleration** and **55 tiles/s maximum horizontal speed**.

## Existing remote-control system

- **G** toggles telekinetic remote item control ON/OFF (rebindable).
- Remote OFF gives completely vanilla weapon/tool/placement behavior.
- Remote melee collision is generated from the remote grip and swing, independent of the physical player.
- Tool tile effects are independent of melee collision: the selected tile can be mined/chopped/hammered from a valid nearby remote operation point while the tool's actual melee swing remains centered on the cursor grip.
- Telekinetic connectivity is evaluated through open space inside the currently visible screen area.
- Pickup uses a fixed 60-tile vanilla grab range with no pathfinding.

## Commands

- `/tkday` — set the world to noon.
- `/tktest` — grant representative swords, a shortsword, Spear, Trident, Jousting Lance, tools, building materials, and a Target Dummy.
- `/tkexplosives` — grant one Bomb and one Grenade for steering tests.

## Suggested levitation tests

1. Fresh 100-HP character: verify a full charge is ~1.0 second.
2. Spend part of the charge, toggle levitation OFF in midair, and verify the HUD recharges gradually rather than instantly.
3. Land with levitation OFF and verify landing itself does not instantly refill the resource.
4. Run/use boots or wings with levitation OFF and verify recharge continues.
5. Toggle levitation ON while grounded and verify the finite resource still drains while ON.
6. Check the smooth duration progression as max HP increases: 150≈1.6s, 200≈2.4s, 250≈3.8s, 300≈5.9s, 350≈9.1s, 400≈14.2s, 450≈22.0s, 485≈30s.
7. At 490 permanent max HP, verify the HUD says `INF` and levitation no longer expires.
8. Verify Left Shift still toggles levitation without also activating Smart Select.
9. Verify G remote-control mode and Bomb/Grenade steering remain unchanged.

## Still intentionally excluded

- Terragrim
- Other projectile/special swords
- Yoyos
- Boomerangs
- Flails
- Multiplayer


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

