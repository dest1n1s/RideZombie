using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace RideZombie;

readonly struct RideInput
{
    public readonly Vector2 Move;
    public readonly Vector2 Look;
    public readonly bool Sprint;
    public readonly bool Climb;
    public readonly int Jumps;
    public readonly int Attacks;

    public RideInput(Vector2 move, Vector2 look, bool sprint, bool climb, int jumps, int attacks)
    {
        Move = move;
        Look = look;
        Sprint = sprint;
        Climb = climb;
        Jumps = jumps;
        Attacks = attacks;
    }

    public RideInput Idle() => new(Vector2.zero, Look, false, false, Jumps, Attacks);

    public object[] ToEvent(int zombieId) => [zombieId, Move.x, Move.y, Look.x, Look.y, Sprint, Climb, Jumps, Attacks];

    public static RideInput FromEvent(object[] data) =>
        new(new Vector2((float)data[1], (float)data[2]), new Vector2((float)data[3], (float)data[4]),
            (bool)data[5], (bool)data[6], (int)data[7], (int)data[8]);
}

readonly struct Steering
{
    public readonly RideInput Latest;
    public readonly float ReceivedAt;
    public readonly RideInput Applied;

    public Steering(RideInput latest, float receivedAt, RideInput applied)
    {
        Latest = latest;
        ReceivedAt = receivedAt;
        Applied = applied;
    }
}

static class Rides
{
    const string KeyPrefix = "RideZombie.";
    const byte RideEvent = 181;
    const byte DismountEvent = 182;
    const byte InputEvent = 183;
    const byte StaminaEvent = 184;
    const float MaxMountDistance = 4f;
    const float InputInterval = 0.05f;
    const float InputTimeout = 0.5f;
    const float SprintStamina = 0.05f;
    const float JumpStamina = 0.05f;
    const float SprintJumpStamina = 0.15f;
    const float LungeStamina = 0.2f;
    const float DismountDistance = 1.2f;
    const float StandingCenterHeight = 0.9f;

    static readonly Dictionary<int, int> riders = new();
    static readonly Dictionary<Character, Character> attached = new();
    static readonly Dictionary<int, int?> published = new();
    static readonly Dictionary<int, Steering> steering = new();
    static RideInput captured;
    static float nextSend;

    public static void Tick()
    {
        riders.Clear();
        foreach (var entry in PhotonNetwork.CurrentRoom.CustomProperties)
            if (entry.Key is string key && key.StartsWith(KeyPrefix) && entry.Value is int riderId)
                riders[int.Parse(key.Substring(KeyPrefix.Length))] = riderId;

        Attach();
        foreach (var zombieId in steering.Keys.Where(id => !riders.ContainsKey(id)).ToList())
            steering.Remove(zombieId);
        if (PhotonNetwork.IsMasterClient)
            foreach (var (zombieId, riderId) in riders)
            {
                var zombie = Find<MushroomZombie>(zombieId);
                var rider = Find<Character>(riderId);
                if (zombie == null || rider == null || !Conscious(rider)
                    || !(Upright(zombie) || zombie.currentState is MushroomZombie.State.Lunging))
                    Publish(zombieId, null);
            }
        else
            published.Clear();
        SendInput();
    }

    public static void Reset()
    {
        riders.Clear();
        attached.Clear();
        published.Clear();
        steering.Clear();
        captured = default;
    }

    public static bool IsRiding(Character character) => ZombieRiddenBy(character.photonView.ViewID) != null;

    public static Character MountOf(Character rider) => attached.TryGetValue(rider, out var zombie) ? zombie : null;

    public static bool CanRide(Character rider, MushroomZombie zombie) =>
        !riders.ContainsKey(zombie.photonView.ViewID) && !IsRiding(rider)
        && Upright(zombie) && Conscious(rider)
        && !rider.data.isCarried && rider.data.carriedPlayer == null && !rider.data.isClimbingAnything;

    public static void RequestRide(Character rider, MushroomZombie zombie) =>
        ToMaster(RideEvent, new[] { zombie.photonView.ViewID, rider.photonView.ViewID });

    static void RequestDismount(Character rider) => ToMaster(DismountEvent, rider.photonView.ViewID);

    public static void OnEvent(EventData photonEvent)
    {
        switch (photonEvent.Code)
        {
            case RideEvent or DismountEvent when PhotonNetwork.IsMasterClient:
                Handle(photonEvent.Code, photonEvent.CustomData);
                break;
            case InputEvent:
                var data = (object[])photonEvent.CustomData;
                Receive((int)data[0], RideInput.FromEvent(data));
                var zombie = Find<Character>((int)data[0]);
                if (zombie != null && zombie.photonView.IsMine)
                    PhotonNetwork.RaiseEvent(
                        StaminaEvent, new object[] { (int)data[0], zombie.data.currentStamina },
                        new RaiseEventOptions { TargetActors = [photonEvent.Sender] }, SendOptions.SendUnreliable);
                break;
            case StaminaEvent:
                var stamina = (object[])photonEvent.CustomData;
                var mount = Find<Character>((int)stamina[0]);
                if (mount != null)
                    mount.data.currentStamina = (float)stamina[1];
                break;
        }
    }

    public static void Capture(CharacterInput input)
    {
        var local = Character.localCharacter;
        if (!IsRiding(local))
        {
            captured = default;
            return;
        }
        if (input.interactWasPressed)
            RequestDismount(local);
        captured = new RideInput(
            input.movementInput, default, input.sprintIsPressed, input.usePrimaryIsPressed,
            captured.Jumps + (input.jumpWasPressed ? 1 : 0),
            captured.Attacks + (input.useSecondaryWasPressed ? 1 : 0));
        var look = input.lookInput;
        input.ResetInput();
        input.lookInput = look;
    }

    public static void Steer(MushroomZombie zombie)
    {
        var zombieId = zombie.photonView.ViewID;
        var ridden = riders.ContainsKey(zombieId);
        if (!zombie.photonView.IsMine || !ridden && !TestZombie.IsFriendly(zombie))
            return;
        if (zombie.currentState is MushroomZombie.State.Sleeping)
            zombie.WakeUpFromSleep();
        if (zombie.currentState is MushroomZombie.State.Idle or MushroomZombie.State.Chasing)
            zombie.currentState = MushroomZombie.State.LungeRecovery;
        zombie.timeSpentRecoveringFromLunge = 0f;
        if (!ridden)
            return;

        steering.TryGetValue(zombieId, out var state);
        var fresh = Time.time - state.ReceivedAt <= InputTimeout;
        var input = fresh ? state.Latest : state.Latest.Idle();
        var character = zombie.character;
        if (fresh)
            character.data.lookValues = input.Look;
        if (input.Attacks != state.Applied.Attacks && character.data.isGrounded
            && zombie.currentState is MushroomZombie.State.LungeRecovery && character.GetTotalStamina() >= LungeStamina)
        {
            character.UseStamina(LungeStamina);
            zombie.lungeTargetForward = character.Center + character.data.lookDirection * 100f;
            character.input.jumpWasPressed = true;
            zombie.currentState = MushroomZombie.State.Lunging;
            zombie.timeSpentLunging = 0f;
        }
        else if (zombie.currentState is not MushroomZombie.State.Lunging)
        {
            character.input.movementInput = input.Move;
            character.input.sprintIsPressed = input.Sprint;
            if (character.data.isGrounded && character.data.isSprinting)
                character.UseStamina(SprintStamina * Time.deltaTime);
            if (input.Jumps != state.Applied.Jumps)
            {
                character.input.jumpWasPressed = true;
                if (character.data.isGrounded)
                    character.UseStamina(character.data.isSprinting ? SprintJumpStamina : JumpStamina);
            }
            if (character.data.isClimbing)
                character.input.usePrimaryWasReleased = state.Applied.Climb && !input.Climb;
            else if (input.Climb)
                character.refs.climbing.TryClimb();
        }
        steering[zombieId] = new Steering(state.Latest, state.ReceivedAt, input);
    }

    public static bool LandLunge(MushroomZombie zombie)
    {
        if (!riders.ContainsKey(zombie.photonView.ViewID) || zombie.timeSpentLunging + Time.deltaTime < zombie.lungeTime)
            return false;
        zombie.timeSpentLunging = 0f;
        zombie.currentState = MushroomZombie.State.LungeRecovery;
        return true;
    }

    public static void Bite(MushroomZombie biter, Collider other)
    {
        if (riders.ContainsKey(biter.photonView.ViewID)
            && CharacterRagdoll.TryGetCharacterFromCollider(other, out var victim) && victim != biter.character
            && victim.photonView.IsMine && victim.TryGetComponent(out MushroomZombie _) && victim.data.fallSeconds <= 0f)
            victim.Fall(biter.biteStunTime);
    }

    static bool Upright(MushroomZombie zombie) =>
        zombie.currentState is MushroomZombie.State.Idle or MushroomZombie.State.Chasing or MushroomZombie.State.LungeRecovery
        && zombie.character.data.fallSeconds <= 0f && !zombie.character.data.passedOut;

    static bool Conscious(Character character) =>
        !character.data.dead && !character.data.passedOut && !character.data.fullyPassedOut;

    static void Attach()
    {
        var wanted = new Dictionary<Character, Character>();
        foreach (var (zombieId, riderId) in riders)
        {
            var zombie = Find<MushroomZombie>(zombieId);
            var rider = Find<Character>(riderId);
            if (zombie != null && rider != null)
                wanted[rider] = zombie.character;
        }
        foreach (var (rider, zombie) in attached.ToList())
            if (!wanted.TryGetValue(rider, out var carrier) || carrier != zombie)
            {
                attached.Remove(rider);
                if (rider == null)
                    continue;
                SetCarrier(rider, null);
                if (rider.IsLocal && zombie != null && DismountSpot(zombie) is Vector3 spot)
                    rider.WarpPlayer(spot, poof: false);
            }
        foreach (var (rider, zombie) in wanted)
            if (!attached.ContainsKey(rider))
            {
                attached[rider] = zombie;
                SetCarrier(rider, zombie);
            }
    }

    static Vector3? DismountSpot(Character zombie)
    {
        var back = -zombie.data.lookDirection_Flat;
        var side = Vector3.Cross(Vector3.up, back);
        foreach (var direction in new[] { back, side, -side, -back })
        {
            var spot = zombie.Center + direction * DismountDistance;
            if (HelperFunctions.LineCheck(zombie.Center, spot + direction * 0.3f, HelperFunctions.LayerType.TerrainMap).transform)
                continue;
            var ground = HelperFunctions.LineCheck(spot + Vector3.up, spot + Vector3.down * 3f, HelperFunctions.LayerType.TerrainMap);
            return ground.transform ? ground.point + Vector3.up * StandingCenterHeight : spot;
        }
        return null;
    }

    static void SetCarrier(Character rider, Character carrier)
    {
        rider.refs.carriying.ToggleCarryPhysics(carrier != null);
        rider.data.isCarried = carrier != null;
        rider.data.carrier = carrier;
    }

    static void SendInput()
    {
        var local = Character.localCharacter;
        if (local == null || ZombieRiddenBy(local.photonView.ViewID) is not int zombieId)
            return;
        var zombie = Find<MushroomZombie>(zombieId);
        if (zombie == null)
            return;
        var input = new RideInput(
            captured.Move, local.data.lookValues, captured.Sprint, captured.Climb, captured.Jumps, captured.Attacks);
        if (zombie.photonView.IsMine)
            Receive(zombieId, input);
        else if (Time.time >= nextSend)
        {
            nextSend = Time.time + InputInterval;
            PhotonNetwork.RaiseEvent(
                InputEvent, input.ToEvent(zombieId),
                new RaiseEventOptions { TargetActors = [zombie.photonView.ControllerActorNr] },
                SendOptions.SendUnreliable);
        }
    }

    static void Receive(int zombieId, RideInput input)
    {
        steering.TryGetValue(zombieId, out var state);
        steering[zombieId] = new Steering(input, Time.time, state.Applied);
    }

    static void ToMaster(byte code, object content)
    {
        if (PhotonNetwork.IsMasterClient)
            Handle(code, content);
        else
            PhotonNetwork.RaiseEvent(
                code, content, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    static void Handle(byte code, object content)
    {
        if (code == RideEvent)
        {
            var ids = (int[])content;
            var zombie = Find<MushroomZombie>(ids[0]);
            var rider = Find<Character>(ids[1]);
            if (zombie != null && rider != null && published.GetValueOrDefault(ids[0]) == null
                && !published.ContainsValue(ids[1]) && CanRide(rider, zombie)
                && Vector3.Distance(rider.Center, zombie.character.Center) <= MaxMountDistance)
                Publish(ids[0], ids[1]);
        }
        else if (ZombieRiddenBy((int)content) is int zombieId)
            Publish(zombieId, null);
    }

    static void Publish(int zombieId, int? riderId)
    {
        if (published.TryGetValue(zombieId, out var last) && last == riderId)
            return;
        published[zombieId] = riderId;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [KeyPrefix + zombieId] = riderId });
    }

    static int? ZombieRiddenBy(int riderId)
    {
        foreach (var (zombieId, rider) in riders)
            if (rider == riderId)
                return zombieId;
        return null;
    }

    static T Find<T>(int viewId) where T : Component
    {
        var view = PhotonNetwork.GetPhotonView(viewId);
        return view == null ? null : view.GetComponent<T>();
    }
}
