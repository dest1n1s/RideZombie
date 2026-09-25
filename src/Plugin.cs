using BepInEx;
using BepInEx.Configuration;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;

namespace RideZombie;

[BepInPlugin(Guid, "RideZombie", "0.1.0")]
public class Plugin : BaseUnityPlugin, IOnEventCallback
{
    const string Guid = "dest1n1.RideZombie";

    ConfigEntry<bool> testZombieKey;

    void Awake()
    {
        testZombieKey = Config.Bind("Testing", "F8SpawnsFriendlyZombie", false,
            "Press F8 to spawn a friendly, rideable zombie in front of you.");
        new Harmony(Guid).PatchAll();
        PhotonNetwork.AddCallbackTarget(this);
    }

    void Update()
    {
        if (PhotonNetwork.InRoom)
        {
            Rides.Tick();
            if (testZombieKey.Value)
                TestZombie.Tick();
        }
        else
            Rides.Reset();
    }

    public void OnEvent(EventData photonEvent) => Rides.OnEvent(photonEvent);
}
