using BepInEx.Configuration;
using UnityEngine;

namespace RideZombie;

static class RidePose
{
    const float CarryForce = 500f;
    const float ShinReach = 0.1f;

    static readonly (BodypartType Leg, BodypartType Knee, BodypartType Foot, float Side)[] Legs =
    [
        (BodypartType.Leg_L, BodypartType.Knee_L, BodypartType.Foot_L, -1f),
        (BodypartType.Leg_R, BodypartType.Knee_R, BodypartType.Foot_R, 1f),
    ];

    public static ConfigEntry<bool> SitOnShoulders;
    public static ConfigEntry<float> SeatHeight;
    public static ConfigEntry<float> SeatBack;
    public static ConfigEntry<float> LegRaise;
    public static ConfigEntry<float> LegSpread;

    static Character SeatedOn(Character rider) => SitOnShoulders.Value ? Rides.MountOf(rider) : null;

    public static bool Face(Character rider)
    {
        if (SeatedOn(rider) is not { } zombie)
            return false;
        rider.refs.rigCreator.transform.rotation = Quaternion.LookRotation(zombie.data.lookDirection_Flat);
        return true;
    }

    public static bool Carry(Character rider)
    {
        if (Rides.MountOf(rider) is not { } zombie)
            return false;
        rider.refs.animations.SetBool("IsCarried", !SitOnShoulders.Value);
        if (!SitOnShoulders.Value)
            return false;
        var seat = zombie.GetBodypart(BodypartType.Head).transform.position
            + Vector3.up * SeatHeight.Value - zombie.data.lookDirection_Flat * SeatBack.Value;
        var hip = rider.GetBodypart(BodypartType.Hip).transform.position;
        rider.AddForce(Vector3.ClampMagnitude(seat + zombie.data.avarageVelocity * 0.06f - hip, 1f) * CarryForce);
        rider.refs.movement.ApplyExtraDrag(0.5f, ignoreRagdoll: true);
        rider.data.sinceGrounded = 0f;
        return true;
    }

    public static void Straddle(Character rider)
    {
        if (SeatedOn(rider) == null)
            return;
        var body = rider.refs.rigCreator.transform.rotation;
        var shin = body * (Vector3.down + Vector3.forward * ShinReach);
        foreach (var (leg, knee, foot, side) in Legs)
        {
            var thigh = body * Quaternion.AngleAxis(side * LegSpread.Value, Vector3.forward)
                * Quaternion.AngleAxis(-LegRaise.Value, Vector3.right) * Vector3.down;
            Aim(rider.GetBodypart(leg).transform, rider.GetBodypart(knee).transform, thigh);
            Aim(rider.GetBodypart(knee).transform, rider.GetBodypart(foot).transform, shin);
        }
    }

    static void Aim(Transform joint, Transform end, Vector3 direction) =>
        joint.rotation = Quaternion.FromToRotation(end.position - joint.position, direction) * joint.rotation;
}
