# Telekinesis Prototype

Single-player prototype for the telekinetic character. This source is not compiled or run by the authoring environment.

Current version is in `build.txt`; per-version history is in `CHANGELOG.md`.

The core fantasy is remotely operating ordinary physical objects rather than replacing Terraria combat with generic magic attacks. A remote weapon behaves as though an invisible hand is physically holding it at the cursor: real swing/thrust geometry matters, the cursor is a grip and pivot rather than an auto-hit point, and the physical player's position does not determine remote melee collision.

## Levitation

Levitation is an innate movement ability on **Left Shift** (rebindable in tModLoader Controls).

### Controls

- While duration is finite, Left Shift is **hold-to-levitate**. Releasing it ends levitation and allows the shared charge to recharge. If the charge is exhausted while Shift is still held, levitation stays locked out until Shift is released.
- At 490+ permanent max HP, where duration is infinite, Left Shift becomes a **toggle**: press to turn on, press again to turn off. Crossing into infinite duration while already levitating preserves the current ON state.
- While levitating:
  - **Up or Jump** rises, **Down** descends.
  - **Left/Right** use levitation's own acceleration and speed cap.
  - Releasing input holds position. Levitation is true suspension, not glide: no input means no falling and no drifting.
- Activating levitation during a jump or fall does not erase existing momentum; it transitions using levitation's own acceleration.
- Left Shift normally activates vanilla Smart Select. While Left Shift is assigned to levitation, levitation takes priority and Smart Select is suppressed.
- Levitation neither drains nor applies while mounted.

### Movement tuning

- Horizontal acceleration **0.20 px/tick²**, top speed **48 tiles/s**.
- Vertical acceleration **0.80 px/tick²**, top speed **22.5 tiles/s** (6 px/tick).
- One single vertical acceleration value covers ordinary acceleration, braking, and reversing momentum.
- Levitation owns horizontal movement and does not inherit walking, sprint, boots, or wing movement stats.

### HP progression

**Duration is the only stat that scales with permanent max HP.** Acceleration and control are fixed at every stage.

Finite duration uses one smooth exponential formula for the entire finite HP range:

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

Only permanent max HP counts. Accessory and buff health modifiers do not change innate duration.

Finite duration behaves as a rechargeable resource:

- it drains continuously while sustained telekinesis is in use;
- it recharges at **1 second per 1 second not in use**;
- recharge does **not** require touching the ground, so running, falling, boots, wings, and mounts can coexist with recharge;
- touching the ground does not instantly refill the resource.

Eating a Life Crystal/Fruit grants the additional capacity unlocked by the new permanent max HP without erasing already-spent duration.

## Remote control

- **G** toggles telekinetic remote item control ON/OFF (rebindable).
- Remote OFF gives completely vanilla weapon/tool/placement behavior, and is the reliable fallback for unsupported or unusual items.
- The remote grip has finite travel speed and must move from the player to the cursor.
- Remote melee collision is generated from the remote grip and the current swing rotation, independent of the physical player. Wall blocking is anchored at the grip, so an enemy pressed against terrain can still be hit from the open side.

### Supported remotely

- Ordinary swinging and thrusting melee (broadswords and similar).
- Melee weapons that also emit projectiles (Ice Blade, Enchanted Sword). The projectile is relocated to the grip and auto-aimed at the nearest visible enemy, preserving vanilla speed and any intentional spread or multishot.
- Shortswords, which are projectile-based and use custom remote thrust handling.
- Terragrim (and Arkhalis), which are held projectiles. Vanilla AI still owns the spin animation and timing; the finished pose is translated to the grip, owner line-of-sight is replaced by grip-to-target, and the blade auto-aims at the nearest enemy within 25 tiles of the grip, falling back to the cursor direction.
- Sky-strike melee such as Starfury and Star Wrath. These aim at a *point* rather than along a line, so the whole vanilla spawn is shifted by cursor-to-target: fall angle, speed, and multi-star spread are unchanged, and the stars land on the nearest visible enemy instead of the cursor. Line-of-sight is not required, because vanilla stars already fall through terrain.
- Bullet guns, fired from the grip and auto-aimed at the nearest visible enemy, with the visible weapon rotated to match. Bows are deliberately excluded: drawing a bow remotely does not fit the fantasy, while a grip is a plausibly *more* stable firing platform for a gun.
- Spears and lances, which retain vanilla spear AI, timing, and reach, with the completed pose translated to the grip and owner line-of-sight replaced by grip-to-target.
- Pickaxes, axes, and hammers. Tool tile effects and tool melee damage are deliberately separate systems: the tool always swings at the cursor, while mining/chopping/hammering is authorized from a reachable operation point within the item's normal tile interaction range.
- Tile and wall placement.

### Reachability

Telekinetic reachability is **connected open space within the visible screen area**, not strict line of sight. The remote hand can travel around corners through connected open space. If the only route leaves the visible region and returns, it does not count, and sealed or disconnected regions remain inaccessible.

- Weapons require the exact grip/cursor position to be reachable.
- Tools and placement require only that some reachable grip position exists within the item's normal local tile interaction range of the target.

Wiring tools, paint tools, and buckets keep their vanilla player-relative range.

### Containers

Chests and dressers are opened and used at telekinetic range rather than vanilla arm's length, using the same authorization as tools: some reachable grip position must exist within the item's normal tile interaction range of the container. An open chest stays open while its own tile remains telekinetically in range, so the cursor can move freely over the inventory. Piggy Bank/Safe-style item containers keep vanilla behavior.

Opening is currently capped at **20 tiles**. Vanilla's own interaction check clamps its reach to 20 tiles regardless of the player's tile range, so that is the limit reachable without patching vanilla code. Keeping an already-open chest open has no such clamp and follows telekinetic reach fully.

Vanilla evaluates every tile interaction against one shared range value, so other right-click tiles such as doors, levers, and signs share this reach while a container is authorized.

### Explosives

While remote control is ON, thrown consumable explosives can be steered toward the cursor for as long as the left-click hold that threw them is maintained. Releasing left click permanently returns that projectile to vanilla ballistic motion. Only velocity is modified; fuse and explosion timing remain vanilla. Launcher ammunition such as rockets is not steered.

### Pickup

Pickup uses a fixed 60-tile (960 px) vanilla grab range with no line of sight or pathfinding, and vanilla item attraction handles the actual movement. This convenience feature is independent of the G remote toggle.

## Enemy manipulation

- **V** is hold-to-use (rebindable).
- Hold V with the cursor over a valid hostile NPC to grab it. The target stays selected while V is held, and moving the cursor telekinetically accelerates the NPC toward it. Releasing V releases the target.
- Enemy AI remains active: telekinesis modifies velocity rather than hard-setting position.
- Acceleration scales with the NPC's knockback susceptibility. Knockback-immune enemies cannot be manipulated.
- Enemy manipulation and self-levitation share the **same** sustained-use charge pool. Using both at once still drains it at one tick per tick, not twice as fast.

## HUD

A single compact status strip is anchored at the bottom-right, away from the inventory, chests, buffs, minimap, chat, and boss bars. It shows remote control state, levitation state and remaining duration (`INF` at 490+ HP), and enemy manipulation state, each with its assigned key.

## Commands

- `/tkday` — set the world to noon.
- `/tktest` — grant representative swords, a shortsword, Spear, Trident, Jousting Lance, tools, building materials, and a Target Dummy.
- `/tkexplosives` — grant one Bomb and one Grenade for steering tests.
- `/tkextractinator` — grant one Extractinator.

## Suggested levitation tests

1. Fresh 100-HP character: verify a full charge is ~1.0 second.
2. Spend part of the charge, release Shift in midair, and verify the HUD recharges gradually rather than instantly.
3. Land with levitation off and verify landing itself does not instantly refill the resource.
4. Run/use boots or wings with levitation off and verify recharge continues.
5. Exhaust the charge while holding Shift and verify levitation stays locked out until Shift is released.
6. Check the smooth duration progression as max HP increases: 150≈1.6s, 200≈2.4s, 250≈3.8s, 300≈5.9s, 350≈9.1s, 400≈14.2s, 450≈22.0s, 485≈30s.
7. At 490 permanent max HP, verify the HUD says `INF`, levitation no longer expires, and Left Shift behaves as a toggle rather than hold-to-use.
8. Verify Left Shift still controls levitation without also activating Smart Select.
9. Activate levitation mid-jump and mid-fall and verify momentum transitions rather than vanishing instantly.
10. Levitate and manipulate an enemy simultaneously and verify the shared pool drains at the normal rate.

## Intentionally excluded

- Yoyos
- Boomerangs
- Flails
- Drills and chainsaws
- Terragrim and other unusual special swords
- Launcher ammunition as steerable explosives
- Multiplayer
