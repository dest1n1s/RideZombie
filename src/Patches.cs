using HarmonyLib;
using UnityEngine;

namespace RideZombie;

[HarmonyPatch(typeof(CharacterInteractible))]
static class RidePrompt
{
    static bool Rideable(CharacterInteractible target)
    {
        var item = Character.localCharacter.data.currentItem;
        return target.TryGetComponent(out MushroomZombie zombie)
            && (item == null || !item.canUseOnFriend)
            && Rides.CanRide(Character.localCharacter, zombie);
    }

    [HarmonyPostfix, HarmonyPatch(nameof(CharacterInteractible.IsInteractible))]
    static void IsInteractible(CharacterInteractible __instance, ref bool __result) =>
        __result = __result || Rideable(__instance);

    [HarmonyPostfix, HarmonyPatch(nameof(CharacterInteractible.IsPrimaryInteractible))]
    static void IsPrimaryInteractible(CharacterInteractible __instance, ref bool __result) =>
        __result = __result || Rideable(__instance);

    [HarmonyPostfix, HarmonyPatch(nameof(CharacterInteractible.GetInteractionText))]
    static void GetInteractionText(CharacterInteractible __instance, ref string __result)
    {
        if (Rideable(__instance))
            __result = "Ride";
    }

    [HarmonyPrefix, HarmonyPatch(nameof(CharacterInteractible.Interact))]
    static bool Interact(CharacterInteractible __instance, Character interactor)
    {
        if (!Rideable(__instance))
            return true;
        Rides.RequestRide(interactor, __instance.GetComponent<MushroomZombie>());
        return false;
    }
}

[HarmonyPatch(typeof(CharacterInput), nameof(CharacterInput.Sample))]
static class RiderInput
{
    static void Postfix(CharacterInput __instance) => Rides.Capture(__instance);
}

[HarmonyPatch(typeof(MushroomZombie), nameof(MushroomZombie.Update))]
static class ZombieSteering
{
    static void Postfix(MushroomZombie __instance) => Rides.Steer(__instance);
}

[HarmonyPatch(typeof(MainCameraMovement), nameof(MainCameraMovement.CharacterCam))]
static class RiderCamera
{
    static void Postfix(MainCameraMovement __instance) => RideCamera.Follow(__instance.transform);
}

[HarmonyPatch(typeof(Character), nameof(Character.SetRotation))]
static class RiderFacing
{
    static bool Prefix(Character __instance) => !RidePose.Face(__instance);
}

[HarmonyPatch(typeof(CharacterCarrying), nameof(CharacterCarrying.GetCarried))]
static class RiderSeat
{
    static bool Prefix(CharacterCarrying __instance) => !RidePose.Carry(__instance.character);
}

[HarmonyPatch(typeof(CharacterAnimations), nameof(CharacterAnimations.ConfigureIK))]
static class RiderLegs
{
    static void Postfix(CharacterAnimations __instance) => RidePose.Straddle(__instance.character);
}

[HarmonyPatch(typeof(MushroomZombie), nameof(MushroomZombie.DoLunging))]
static class RiddenLunge
{
    static bool Prefix(MushroomZombie __instance) => !Rides.LandLunge(__instance);
}

[HarmonyPatch(typeof(MushroomZombieBiteCollider), nameof(MushroomZombieBiteCollider.OnTriggerEnter))]
static class ZombieBite
{
    static void Postfix(MushroomZombieBiteCollider __instance, Collider other) => Rides.Bite(__instance.parentZombie, other);
}
