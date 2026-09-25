using BepInEx;
using BepInEx.Configuration;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;

namespace RideZombie;

[BepInPlugin(Guid, "RideZombie", "0.1.2")]
public class Plugin : BaseUnityPlugin, IOnEventCallback
{
    const string Guid = "dest1n1.RideZombie";

    ConfigEntry<bool> testZombieKey;

    void Awake()
    {
        testZombieKey = Config.Bind("Testing", "F8SpawnsFriendlyZombie", false,
            "Press F8 to spawn a friendly, rideable zombie in front of you.");
        RideCamera.Distance = Config.Bind("Camera", "Distance", 4f, "How far behind the zombie the camera sits while riding.");
        RideCamera.Height = Config.Bind("Camera", "Height", 1f, "How far above the zombie the camera looks from while riding.");
        new Harmony(Guid).PatchAll();
        PhotonNetwork.AddCallbackTarget(this);
    }

    void Update()
    {
        if (PhotonNetwork.InRoom)
        {
            Rides.Tick();
            ZombieStaminaBar.Tick();
            if (testZombieKey.Value)
                TestZombie.Tick();
        }
        else
            Rides.Reset();
    }

    public void OnEvent(EventData photonEvent) => Rides.OnEvent(photonEvent);
}
