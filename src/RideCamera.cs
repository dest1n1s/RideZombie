using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Rendering;

namespace RideZombie;

static class RideCamera
{
    const float WallGap = 0.2f;

    public static ConfigEntry<float> Distance;
    public static ConfigEntry<float> Height;
    static bool showingSelf;

    public static void Follow(Transform camera)
    {
        var local = Character.localCharacter;
        if (local == null)
            return;
        var zombie = Rides.IsRiding(local) ? local.data.carrier : null;
        if (showingSelf != (zombie != null))
        {
            showingSelf = zombie != null;
            foreach (var part in local.GetComponentsInChildren<LocalPlayerRenderer>(true))
                part.GetComponent<MeshRenderer>().shadowCastingMode = showingSelf ? ShadowCastingMode.On : part.renderMode;
        }
        if (zombie == null)
            return;
        var look = local.data.lookDirection;
        var pivot = zombie.Center + Vector3.up * Height.Value;
        var wanted = pivot - look * Distance.Value;
        var wall = HelperFunctions.LineCheck(pivot, wanted, HelperFunctions.LayerType.TerrainMap);
        camera.SetPositionAndRotation(
            wall.transform ? wall.point + (pivot - wall.point).normalized * WallGap : wanted,
            Quaternion.LookRotation(look));
    }
}
