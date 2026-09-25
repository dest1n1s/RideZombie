using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RideZombie;

static class TestZombie
{
    const string Marker = "RideZombie.Friendly";
    const Key SpawnKey = Key.F8;
    const float SpawnDistance = 3f;

    public static bool IsFriendly(MushroomZombie zombie) => zombie.photonView.InstantiationData is [Marker];

    public static void Tick()
    {
        var local = Character.localCharacter;
        if (local == null || Keyboard.current is not { } keyboard || !keyboard[SpawnKey].wasPressedThisFrame)
            return;
        var ahead = local.Center + local.data.lookDirection_Flat * SpawnDistance;
        var ground = HelperFunctions.LineCheck(ahead + Vector3.up * 3f, ahead + Vector3.down * 10f, HelperFunctions.LayerType.TerrainMap);
        var zombie = PhotonNetwork.Instantiate(
            "MushroomZombie", ground.transform ? ground.point : ahead,
            Quaternion.LookRotation(-local.data.lookDirection_Flat), 0, [Marker]).GetComponent<MushroomZombie>();
        zombie.lifetime = float.PositiveInfinity;
    }
}
