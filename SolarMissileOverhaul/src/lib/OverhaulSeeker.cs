using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace missileoverhaul.lib;

public abstract class OverhaulSeeker : MonoBehaviour
{
    // Structure
    protected Missile missile;
    protected OverhaulGuidance guidance;
    protected float UnityTime => Time.timeSinceLevelLoad;


    // Init
    protected int priority; // 0: First, 1: Second, etc...
    protected SeekerType seekerType = SeekerType.Unknown;
    protected bool triggerMissileWarning;


    // Parameters
    protected virtual Vector3 SeekerForward => missile.transform.forward;
    protected float maxRange = 15000f; // Absolute maximum range of the seeker.
    protected float targetRange = 10000f; // Range at which seeker will attempt to locate target.
    protected float seekerSlewMax = 0f; // Maximum degrees seeker can slew from seekerForward. 0: No slew.
    protected float seekerFOV = 0f; // Field of view of the seeker. 0: No area scanning. 360: Omnidirectional.
    protected float seekerSlewRate = 120f; // Maximum seeker head slew rate in degrees per second.

    // Tracking
    protected Vector3 seekerTarget; // What direction the seeker should be trying to search in.
    protected Vector3 seekerSlew; // What local rotation the seeker has relative to the missile.


    // Class Methods
    public void InitializeAbstract(int seekerPriority)
    {
        priority = seekerPriority;
        guidance = missile.GetGuidance();

        Initialize();
        this.StartSlowUpdateDelayed(0.25f, UpdateSlow);
    }

    protected abstract void Initialize();

    public void UpdateAbstract(Vector3 targetDirection)
    {
        seekerTarget = targetDirection;

        if (seekerSlewMax > 0f)
        {
            seekerSlew = Vector3.RotateTowards(SeekerForward, seekerTarget, seekerSlewMax*(float)Math.PI/180f, 0f);
        }
        else
        {
            seekerSlew = SeekerForward;
        }

        Update();
    }

    protected abstract void Update();

    protected abstract void UpdateSlow();

    public virtual void Dispose()
    {

    }

    public virtual GlobalPosition GetEvasionPoint() => missile.GlobalPosition();


    // Other Methods
    public string GetSeekerType() => seekerType.ToString();


    protected bool IsInSeeker(GlobalPosition position)
    {
        if (FastMath.SquareDistance(missile.GlobalPosition(), position) > maxRange * maxRange)
            return false;

        Vector3 displacement = position - missile.GlobalPosition();

        return seekerFOV >= 360f || (seekerFOV > 0f && Vector3.Angle(seekerSlew, displacement) <= seekerFOV / 2f);
    }

    protected IEnumerable<Unit> GetUnitsInSeeker()
    {
        return UnitRegistry.allUnits.Where(unit => IsInSeeker(unit.GlobalPosition()));
    }

    protected IEnumerable<Aircraft> GetAircraftInSeeker()
    {
        return UnitRegistry.allAircraft.Where(aircraft => IsInSeeker(aircraft.GlobalPosition()));
    }



    protected IEnumerable<IRFlare> GetFlaresInSeeker()
    {
        return FindObjectsOfType<IRFlare>().Where(flare => IsInSeeker(flare.transform.GlobalPosition()));
    }

    protected IEnumerable<RadarChaff> GetChaffInSeeker()
    {
        return FindObjectsOfType<RadarChaff>().Where(chaff => IsInSeeker(chaff.transform.GlobalPosition()));
    }

}


public enum SeekerType : byte
{
    Unknown = 0,
    SARH = 1,
    IR = 2,
    ARH = 3,
    ARAD = 4,
    INS = 5,
    Laser = 6,
    Optical = 7,
    Datalink = 8
}


