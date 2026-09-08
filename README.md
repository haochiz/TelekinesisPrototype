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
