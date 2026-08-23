using System;
using System.Collections.Generic;
using UnityEngine;

namespace missileoverhaul.lib;

public abstract class OverhaulGuidance : MonoBehaviour
{
    // Structure
    [SerializeField]
    protected Missile missile;
    protected static float UnityTime => Time.timeSinceLevelLoad;


    // Init
    public bool hasProximityFuse;
    protected bool hasDatalinkAfterLaunch = false;
    protected bool hasDatalinkOnLaunch = true;


    // State
    protected bool isArmed = false;
    protected float? armedDelay;
    protected bool isGuidance = false;
    protected float? guidanceDelay;
    protected bool isTangible = false;
    protected float? tangibleDelay;


    // Flight Parameters
    protected float minSpeed = 200f; // [m/s] Minimum speed the missile will still consider itself active.
    protected float maxLead = 10f;
    protected float maxTargetSpeed = float.NaN;
    protected float loftAmount = 0f;


    // Flight Convenience Values
    protected float topSpeed; // [m/s] Estimated maximum speed based on launch conditions.


    // Launch Values
    protected Track launchTrack; // Track of missile's own launch.
    protected Unit? launchTarget; // Unit that missile was launched to target, if applicable.
    protected GlobalPosition launchAimpoint; // The initial aimpoint the missile was launched at.


    // Targeting
    protected Track targetTrackData; // The last confirmed track on the target.
    protected Track targetTrack; // A predicted track on target, used for helper functions.

    protected OverhaulGuidance(Missile missile)
    {
        this.missile = missile;
    }

    public Track GetTargetTrack()
    {
        return targetTrack;
    }

    // Initialization
    public void Initialize(Unit? target, GlobalPosition aimpoint)
    {
        // Launch Values
        launchTarget = target;
        launchAimpoint = aimpoint;
        launchTrack = new Track(missile, missile.GlobalPosition(), PlatformVelocity, null, UnityTime);


        targetTrackData = new Track(launchTarget, null, null, null, null);

        // Target Information
        if (launchTarget != null)
        {
            // If launch target is not null, collect known target data for track.
            if (missile.NetworkpersistentID == target?.NetworkpersistentID)
            {
                // If launch target is in the same network, we already know everything.
                targetTrackData.position = launchTarget.GlobalPosition();
                targetTrackData.velocity = launchTarget.rb.velocity;
                targetTrackData.time = UnityTime;
            }
            else if (missile.NetworkHQ != null)
            {
                // If we have a network, try to gather target data from it.
                if (missile.NetworkHQ.IsTargetBeingTracked(launchTarget))
                {
                    // If launch target is being tracked, just get the real values.
                    targetTrackData.position = launchTarget.GlobalPosition();
                    targetTrackData.velocity = launchTarget.rb.velocity;
                    targetTrackData.time = UnityTime;
                }
                else if (missile.NetworkHQ.trackingDatabase.TryGetValue(launchTarget.persistentID,
                             out TrackingInfo trackingInfo))
                {
                    // Otherwise, get last known tracking info.
                    targetTrackData.position = trackingInfo.lastKnownPosition;
                    targetTrackData.time = trackingInfo.lastSpottedTime;
                }
            }
        }


        // Calculating
        topSpeed = missile.GetTopSpeed(missile.GlobalPosition().y, targetTrackData.GetPosition().y);

        targetTrack = new Track(targetTrackData.reference, targetTrackData.position, targetTrackData.velocity, targetTrackData.acceleration, targetTrackData.time);

        // Seeker Setup
        List<OverhaulSeeker> seekers = missile.GetSeekers();
        for (int i = 0; i < seekers.Count; i++)
        {
            seekers[i].InitializeAbstract(i);
        }
    }

    public void UpdateTarget(Track track)
    {
        if (targetTrackData.reference == track.reference)
        {
            // Assume target update is the same as previous target.
            Vector3? dPosition = track.position - targetTrackData.position;
            Vector3? dVelocity = track.velocity - targetTrackData.velocity;
            //Vector3? dAcceleration = track.acceleration - targetTrack.acceleration;
            float? dTime = track.time - targetTrackData.time;

            // Assume same target, update old values.
            targetTrackData.position = track.position;
            targetTrackData.velocity = track.velocity ?? dPosition / dTime;
            targetTrackData.acceleration = track.acceleration ?? dVelocity / dTime;
        }
        else
        {
            // Assume target update is entirely new target.
            targetTrackData = track;
        }
    }

    public void UpdateAbstract()
    {
        // Check for enabling guidance.
        if (!isGuidance && (!guidanceDelay.HasValue || missile.timeSinceSpawn >= guidanceDelay.Value))
            isGuidance = true;

        // Check for arming warhead (once).
        if (!isArmed && (!armedDelay.HasValue || missile.timeSinceSpawn >= armedDelay.Value))
        {
            missile.Arm();
            isArmed = true;
        }

        // Check for making tangible (once).
        if (!isTangible && (!tangibleDelay.HasValue || missile.timeSinceSpawn >= tangibleDelay.Value))
        {
            missile.SetTangible(true);
            isTangible = true;
        }

        // Calculate new targetTrack.
        if (targetTrackData.time.HasValue)
        {
            targetTrack = targetTrackData.Predict(UnityTime - targetTrackData.time.Value);
        }

        Vector3 targetDirection = TargetDirection;
        // Run seekers.
        foreach (OverhaulSeeker seeker in missile.GetSeekers())
        {
            seeker.UpdateAbstract(targetDirection);
        }

        if (isGuidance)
            Update();
    }

    protected abstract void Update();

    public virtual void Dispose()
    {

    }


    // Helper Properties
    protected Vector3 PlatformVelocity =>
        missile.rb.velocity;

    protected Vector3 PlatformVelocityNominal =>
        missile.timeSinceSpawn < 3.0 ? missile.transform.forward * topSpeed : PlatformVelocity;

    protected float TargetDistance => FastMath.Distance(missile.GlobalPosition(), targetTrack.GetPosition());

    protected Vector3 TargetDisplacement => targetTrack.GetPosition() - missile.GlobalPosition();

    protected Vector3 TargetDirection => TargetDisplacement.normalized;

    protected float TimeToTarget =>
        TargetDistance / Mathf.Max(Vector3.Dot(TargetDisplacement, PlatformVelocity), minSpeed);

    protected float TimeToTargetNominal =>
        TargetDistance / Mathf.Max(Vector3.Dot(TargetDisplacement, PlatformVelocityNominal), minSpeed);


    // Guidance Methods
    protected Vector3 TargetLeadVector()
    {
        return OverhaulCalc.GetLeadVector(targetTrack.GetPosition(),
            missile.GlobalPosition(), targetTrack.GetVelocity(), PlatformVelocity,
            maxLead);
    }

    protected Vector3 TargetLeadVectorWithAccel()
    {
        return OverhaulCalc.GetLeadVectorWithAccel(targetTrack.GetPosition(),
            missile.GlobalPosition(), targetTrack.GetVelocity(), PlatformVelocity,
            targetTrack.GetAcceleration(), maxLead);
    }

    protected Vector3 LoftVector()
    {
        if (loftAmount <= 0)
            return Vector3.zero;

        float timeToTarget = TimeToTargetNominal;

        float loft = Mathf.Min(timeToTarget * timeToTarget * loftAmount * 4.90500020980835f, TargetDistance * loftAmount);
        return loft * Vector3.up;

    }

    protected GlobalPosition AimpointFromTargetVector(Vector3 vector)
    {
        GlobalPosition aim = targetTrack.GetPosition();
        aim.y = Mathf.Max(aim.y, 0);
        return aim;
    }

    protected void SetAimpoint(GlobalPosition aimPoint, Vector3 targetVel)
    {
        missile.SetAimpoint(aimPoint, targetVel);
    }


}

public enum GuidanceType : byte
{
    Unknown = 0,
    Unguided = 1
}