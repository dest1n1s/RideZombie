# RideZombie

Ride the mushroom zombies in PEAK.

## Controls

| Action | Default key |
|---|---|
| Get on | Look at a standing zombie and press **E** (not while holding an item you can use on a friend) |
| Steer | Move and look as usual |
| Sprint / jump | **Shift** / **Space** |
| Climb | Hold **left mouse** |
| Lunge and bite | **Right mouse** |
| Get off | **E** |

You are thrown off when the zombie falls over or dies, and when you pass out. A wild zombie starts chasing
again a few seconds after you get off.

## Multiplayer

The host and every player who wants to ride need the mod. Players without it can still join, but see riders
walking behind the zombie instead of on its back.

## Testing

Set `F8SpawnsFriendlyZombie = true` under `[Testing]` in `BepInEx/config/dest1n1.RideZombie.cfg` (or in
ModConfig) and press **F8** to spawn a zombie in front of you that never attacks.
