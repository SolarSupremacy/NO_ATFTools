using HarmonyLib;
using UnityEngine;
using Random = UnityEngine.Random;

using missileoverhaul.lib;

namespace missileoverhaul.patchSubclass;

// ARHSeekerOverhaul: Drop-in replacement for ARHSeeker with better functionality.
public class ARHSeekerPatch : ARHSeeker, MissileSeekerOverhaul
{
    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        lockPerseveranceRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("lockPerseverance");

    private static readonly AccessTools.FieldRef<ARHSeeker, bool>
        homeOnJamRef =
            AccessTools.FieldRefAccess<ARHSeeker, bool>("homeOnJam");

    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        homingLockDelayRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("homingLockDelay");

    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        maxDatalinkAngleRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("maxDatalinkAngle");

    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        minReacquireRangeRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("minReacquireRange");

    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        datalinkPositionalErrorRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("datalinkPositionalError");

    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        maxTrackingAngleRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("maxTrackingAngle");

    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        armDelayRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("armDelay");

    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        guidanceDelayRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("guidanceDelay");

    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        terminalRangeRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("terminalRange");

    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        maxLeadRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("maxLead");

    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        selfDestructAtSpeedRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("selfDestructAtSpeed");

    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        loftAmountRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("loftAmount");

    private static readonly AccessTools.FieldRef<ARHSeeker, RadarParams>
        radarParametersRef =
            AccessTools.FieldRefAccess<ARHSeeker, RadarParams>("radarParameters");

    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        jamToleranceRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("jamTolerance");

    private static readonly AccessTools.FieldRef<ARHSeeker, JinkEvasion>
        jinkEvasionRef =
            AccessTools.FieldRefAccess<ARHSeeker, JinkEvasion>("jinkEvasion");

    // seekerFOV
    // Field of view of the seeker sensor in degrees.
    private readonly float seekerFOV = 2.5f;

    // seekerSlewMax
    // Max degrees off boresight seeker can be slewed (half of total FOV).
    // 180 or higher is unlimited.
    private readonly float seekerSlewMax = 60f;

    private bool achievedLock;

    private bool armed;
    private bool guidance;
    private float homingLockTime;
    private bool isJammed;
    private float jamAccumulation;


    // Tracking variables.
    private float knownTime;
    private GlobalPosition knownPos;
    private Vector3 knownVel;
    private Vector3 knownVelPrev;
    private Vector3 knownAccel;
    private GlobalPosition ghostPos;
    private Vector3 ghostVel;
    private Vector3 ghostAccel;

    
    private float lastActiveTrackAttempt;
    private float lastDatalinkTrackAttempt;
    private bool launchLock;


    // Overhaul variables.
    private Unit? launchTarget;
    private bool multipleInbound;

    private Vector3 positionalErrorVector;
    private bool radarLockEstablished;
    private float lastTargetReturnStrength;
    private float targetDist;
    private float timeToTarget;
    private float timeWithoutReturn;
    private float topSpeed;

    private Missile.SeekerMode seekerMode
    {
        get => missile.seekerMode;
        set
        {
            missile.seekerMode = value;
            missile.NetworkseekerMode = value;
        }
    }

    private Missile.SeekerMode seekerModeLocal
    {
        get => missile.seekerMode;
        set => missile.seekerMode = value;
    }

    private float lockPerseverance =>
        lockPerseveranceRef(this);

    private bool homeOnJam =>
        homeOnJamRef(this);

    private float homingLockDelay =>
        homingLockDelayRef(this);

    private float maxDatalinkAngle =>
        maxDatalinkAngleRef(this);

    private float minReacquireRange =>
        minReacquireRangeRef(this);

    private float datalinkPositionalError =>
        datalinkPositionalErrorRef(this);

    private float maxTrackingAngle =>
        maxTrackingAngleRef(this);

    private float armDelay =>
        armDelayRef(this);

    private float guidanceDelay =>
        guidanceDelayRef(this);

    private float terminalRange =>
        terminalRangeRef(this);

    private float maxLead =>
        maxLeadRef(this);

    private float selfDestructAtSpeed =>
        selfDestructAtSpeedRef(this);

    private float loftAmount =>
        loftAmountRef(this);

    private RadarParams radarParameters =>
        radarParametersRef(this);

    private float jamTolerance =>
        jamToleranceRef(this);

    private JinkEvasion jinkEvasion =>
        jinkEvasionRef(this);

    public override void Initialize(Unit target, GlobalPosition aimpoint)
    {
        // Initiate flight parameters.
        seekerModeLocal = Missile.SeekerMode.passive;
        positionalErrorVector = Random.insideUnitSphere * datalinkPositionalError;
        lastActiveTrackAttempt = Time.timeSinceLevelLoad - 0.9f;
        topSpeed = missile.GetTopSpeed(0.0f, 0.0f);
        multipleInbound = false;
        missile.onJam += ARHSeeker_OnJam;
        missile.onDisableUnit += ARHSeeker_OnMissileDestroyed;

        knownPos = aimpoint;
        knownVel = Vector3.zero;
        knownAccel = Vector3.zero;
        knownTime = Time.timeSinceLevelLoad;

        // Target logic.
        launchTarget = target;
        launchLock = false; // TODO: Implement.


        //UpdateUnitLock(target);

        if (launchTarget != null)
        {
            if (launchLock)
            {
                // TODO: Implement.
            }
            else
            {
                GlobalPosition? targetDatalink = missile.NetworkHQ.GetKnownPosition(launchTarget);
                if (targetDatalink.HasValue)
                {
                    knownPos += positionalErrorVector;
                    if (missile.NetworkHQ.IsTargetBeingTracked(launchTarget))
                        knownVel = !(launchTarget != null) || !(launchTarget.rb != null)
                            ? Vector3.zero
                            : launchTarget.rb.velocity;
                }
            }
        }

        Plugin.Log?.LogDebug(
            $" ARH {missile.persistentID} # . . . Launch target: {launchTarget} - Launch lock: {launchLock}");

        this.StartSlowUpdate(1f, SlowChecks);
    }

    public override string GetSeekerType()
    {
        return "ARH";
    }

    private void Guidance(GlobalPosition targetPos, Vector3 targetVel, Vector3 targetAccel)
    {
        // Calculate lead vector.
        Vector3 platformVel = missile.timeSinceSpawn < 3.0
            ? missile.transform.forward * topSpeed
            : missile.rb.velocity;
        Vector3 leadVector = TargetCalc.GetLeadVectorWithAccel(targetPos, missile.GlobalPosition(),
            targetVel, platformVel, targetAccel, maxLead);

        // If lofted, factor that into lead vector.
        if (loftAmount > 0.0)
        {
            if (missile.timeSinceSpawn < 3.0)
                timeToTarget = targetDist / topSpeed;
            float num = Mathf.Min((float)(timeToTarget * (double)timeToTarget * 4.90500020980835) * loftAmount,
                targetDist * loftAmount);
            leadVector += num * Vector3.up;
            // Since timeToTarget is seemingly only used for lofting, only subtract from it in this case.
            timeToTarget -= Time.fixedDeltaTime;
        }

        // Apply jink adjustment to vector if multiple missiles are inbound on the target.
        float jinkMult = Mathf.InverseLerp(3f, 10f, missile.timeSinceSpawn);
        if (jinkEvasion.amount > 0.0 && multipleInbound && targetDist > (double)terminalRange)
            leadVector += jinkMult * jinkEvasion.ApplyJink(transform.GlobalPosition(), targetPos, missile.speed,
                targetDist);

        // Calculate aim point.
        GlobalPosition aimPoint = targetPos + leadVector;
        aimPoint.y = Mathf.Max(aimPoint.y, 0.0f);

        // Set aimpoint.
        missile.SetAimpoint(aimPoint, targetVel);

        // Debug visualization.
        if (!PlayerSettings.debugVis) return;

        GameObject debugGameObject = Instantiate(GameAssets.i.debugArrowGreen, missile.transform);
        debugGameObject.transform.rotation = Quaternion.LookRotation(aimPoint - missile.GlobalPosition());
        debugGameObject.transform.localScale = new Vector3(1f, 1f, (aimPoint - missile.GlobalPosition()).magnitude);
        Destroy(debugGameObject, 0.017f);

        GameObject debugGameObject2 = Instantiate(GameAssets.i.debugArrow, missile.transform);
        debugGameObject2.transform.rotation = Quaternion.LookRotation(targetPos - missile.GlobalPosition());
        debugGameObject2.transform.localScale = new Vector3(1f, 1f, (targetPos - missile.GlobalPosition()).magnitude);
        Destroy(debugGameObject2, 0.017f);
    }

    private float GetRadarReturn(Unit unit, float? lastReturn)
    {
        if (unit is not IRadarReturn unitR)
            return 0.0f;
        
        lastReturn ??= 0.0f;

        if ((isJammed && !homeOnJam) || (lastReturn == 0.0f && targetDist < 5000.0f && unitR.GetECMIntensity() > 2.0f))
            return 0.0f;

        // Future note to myself: I replaced a bunch of `transform` with `missile.transform`.
        // If shit's busted, maybe look into that.
        GlobalPosition platformPos = missile.GlobalPosition();
        GlobalPosition targetPos = unit.GlobalPosition();
        Vector3 lateralDifference = (targetPos - platformPos) with { y = 0.0f };
        float distance = FastMath.Distance(platformPos, targetPos);
        float lateralDistance = lateralDifference.magnitude;
        float platformHorizonDist = Mathf.Sqrt(12742000f * platformPos.y);
        float targetHorizonDist = Mathf.Sqrt(12742000f * targetPos.y);
        
        // If outside of maximum range, return zero.
        if (distance > radarParameters.maxRange)
            return 0.0f;
        
        // If no line of sight to target, return zero.
        if (platformHorizonDist + targetHorizonDist < distance || !TargetCalc.LineOfSight(missile.transform, targetUnit.transform, 10f))
            return 0.0f;
        
        // If not tracking and too close to acquire or out of tracking angle, return zero.
        if ((lastReturn < radarParameters.minSignal && distance < minReacquireRange)
            || Vector3.Angle(missile.transform.forward, unit.transform.position - missile.transform.position) > maxTrackingAngle)
            return 0.0f;
        
        // Calculate clutter.
        float backgroundClutter = 0.0f;
        if (lateralDistance < platformHorizonDist && targetPos.y < platformPos.y * (1.0 - lateralDistance / platformHorizonDist))
        {
            float backdropDistance = distance * unit.radarAlt / (platformPos.y - targetPos.y);
            backgroundClutter += Mathf.Min(distance, 1000f) / backdropDistance;
        }

        float proximityClutter = targetUnit.maxRadius * targetUnit.maxRadius * 2.0f /
                                 (targetUnit.radarAlt * targetUnit.radarAlt);

        float clutter = backgroundClutter + proximityClutter;
        return unitR.GetRadarReturn(missile.transform.position, null, missile, distance, clutter,
            radarParameters, true);
    }

    private float GetTargetRadarReturn()
    {
        if (targetUnit is not IRadarReturn)
        {
            lastTargetReturnStrength = 0.0f;
            return 0.0f;
        }

        // Only check every half-second.
        if (Time.timeSinceLevelLoad - lastActiveTrackAttempt < 0.5)
            return lastTargetReturnStrength;
        lastActiveTrackAttempt = Time.timeSinceLevelLoad;

        lastTargetReturnStrength = GetRadarReturn(targetUnit, lastTargetReturnStrength);
        return lastTargetReturnStrength;
    }

    private bool SeekerIsLocked()
    {
        bool result = targetUnit != null && targetUnit.transform != null;
        if (!result) SeekerLockUnit(null);
        return result;
    }

    private void SeekerLockUnit(Unit? unit)
    {
        if (targetUnit is IRadarReturn) targetUnit.onAddRadarChaff -= ARHSeeker_OnChaff;
        targetUnit = unit;
        if (targetUnit is IRadarReturn) targetUnit.onAddRadarChaff += ARHSeeker_OnChaff;

        missile.SetTarget(targetUnit);

        if (proximityFuse && targetUnit != null)
            missile.SetProxyFuse(targetUnit.GetRandomPart().transform, targetUnit.rb);

        if (targetUnit != null)
            targetUnit.RecordDamage(missile.ownerID, 1f / 1000f);
    }

    private void SlowChecks()
    {
        if (missile.disabled)
            return;

        // Update multiple inbound.
        if (missile.NetworkHQ != null && targetUnit != null && !targetUnit.disabled &&
            targetUnit.NetworkHQ != missile.NetworkHQ)
        {
            TrackingInfo trackingData = missile.NetworkHQ.GetTrackingData(targetUnit.persistentID);
            multipleInbound = targetUnit is Aircraft && trackingData.missileAttacks > 1;
        }
        else
        {
            multipleInbound = false;
        }

        // Lofting logic.
        if (loftAmount > 0.0)
        {
            Vector3 vector3 = knownPos - missile.GlobalPosition();
            float a = Vector3.Dot(vector3.normalized, missile.rb.velocity);
            GlobalPosition knownPosition;
            if (targetUnit != null && missile.NetworkHQ.TryGetKnownPosition(targetUnit, out knownPosition))
                vector3 = knownPosition - missile.GlobalPosition();

            // TODO: Why are these in SlowChecks()?
            targetDist = vector3.magnitude;
            timeToTarget = targetDist / Mathf.Max(a, 10f);
        }

        // Handle self-destruct logic.
        if ((missile.timeSinceSpawn < 10.0 && (missile.EngineOn() || missile.timeSinceSpawn < 2.0)) ||
            (!missile.LosingGround() && !missile.MissedTarget() && missile.speed >= (double)selfDestructAtSpeed))
            return;

        if (missile.timeSinceSpawn >= 10.0)
            Plugin.Log?.LogInfo($" ARH {missile.persistentID} . . . # Self-destruct A: 10 seconds passed.");
        if (!missile.EngineOn() && missile.timeSinceSpawn >= 2.0)
            Plugin.Log?.LogInfo(
                $" ARH {missile.persistentID} . . . # Self-destruct A: 2 seconds passed and engine is off.");
        if (missile.LosingGround())
            Plugin.Log?.LogInfo($" ARH {missile.persistentID} . . . # Self-destruct B: Losing ground.");
        if (missile.MissedTarget())
            Plugin.Log?.LogInfo($" ARH {missile.persistentID} . . . # Self-destruct B: Missed target.");
        if (missile.speed < selfDestructAtSpeed)
            Plugin.Log?.LogInfo(
                $" ARH {missile.persistentID} . . . # Self-destruct B: Below minimum speed ({missile.speed}).");

        missile.Detonate(missile.rb.velocity, false, false);
    }

    private void ARHSeeker_OnJam(Unit.JamEventArgs e)
    {
        // Jamming only works if jamming unit is within tracking angle.
        if (Vector3.Angle(e.jammingUnit.GlobalPosition() - missile.GlobalPosition(), missile.transform.forward) >
            maxTrackingAngle)
            return;
        jamAccumulation += e.jamAmount;
        missile.RecordDamage(e.jammingUnit.persistentID, 0.01f);
        // Only continue past this part if homeOnJam is true and jamAccumulation is above jamTolerance.
        if (jamAccumulation < jamTolerance || !homeOnJam)
            return;
        // missile.SetTarget(e.jammingUnit);
        // targetUnit = e.jammingUnit;
        SeekerLockUnit(e.jammingUnit);
        knownPos = e.jammingUnit.GlobalPosition();
        knownVel = e.jammingUnit.rb.velocity;
        radarLockEstablished = false; // TODO: Address.
    }

    private void ARHSeeker_OnChaff(RadarChaff source)
    {
        // TODO: Implement new chaff mechanics.
        if (!SeekerIsLocked())
            return;
        float num1 = RangeCoef(FastMath.Distance(transform.position, targetUnit.transform.position));
        float num2 = Mathf.Clamp01(1f - Mathf.Abs(Vector3.Dot(
            FastMath.NormalizedDirection(transform.position, targetUnit.transform.position),
            FastMath.NormalizedDirection(source.transform.position, targetUnit.transform.position))));
        float num3 = (float)(num1 * (double)num2 / (1.0 + jamTolerance));
        jamAccumulation += num3;
        Plugin.Log?.LogDebug(
            $"Target chaff angle: {num2:F2}, range coeff: {num1:F2}, dazzle : {num3:F2} jam: {jamAccumulation:F2} success {jamAccumulation > (double)jamTolerance}");
    }

    private void ARHSeeker_OnMissileDestroyed(Unit obj)
    {
        SeekerLockUnit(null);
        Plugin.Log?.LogDebug($" ARH {missile.persistentID} . . . # Destroyed!");
    }

    private float RangeCoef(float targetDistance)
    {
        float maxRange = radarParameters.maxRange;
        return Mathf.Clamp01(1f - targetDistance / maxRange);
    }

    public override void Seek()
    {
        // if (missile.targetID.NotValid)
        // {
        //     knownPos += knownVel * Time.fixedDeltaTime;
        //     missile.SetAimpoint(knownPos, Vector3.zero);
        //     return;
        // }

        // Arm and tangible after armDelay.
        if (!armed && missile.timeSinceSpawn > armDelay)
        {
            armed = true;
            missile.Arm();
            missile.SetTangible(true);
        }

        // Enable guidance after guidanceDelay.
        if (!guidance && missile.timeSinceSpawn > guidanceDelay)
        {
            guidance = true;
            missile.DeployFins();
        }

        // Handle jam accumulation.
        jamAccumulation -= Mathf.Max(jamAccumulation, 0.2f) * Mathf.Max(jamTolerance, 0.1f) * Time.deltaTime;
        jamAccumulation = Mathf.Clamp01(jamAccumulation);
        isJammed = jamAccumulation > jamTolerance;

        if (targetUnit == null)
        {
            missile.SetTarget(null);
            missile.SetAimpoint(missile.GlobalPosition() + missile.transform.forward * 10000f, Vector3.zero);
        }
        else if (!guidance)
        {
            missile.SetAimpoint(missile.GlobalPosition() + missile.transform.forward * 10000f, Vector3.zero);
        }
        else
        {
            if (!radarLockEstablished)
            {
                DatalinkMode();
                knownPos += knownVel * Time.fixedDeltaTime;
            }
            else
            {
                TerminalMode();
            }

            Guidance(ghostPos, ghostVel, ghostAccel);
        }
    }

    private void DatalinkMode()
    {
        if (Time.timeSinceLevelLoad - (double)lastDatalinkTrackAttempt < 1.0)
            return;
        lastDatalinkTrackAttempt = Time.timeSinceLevelLoad;
        if (FastMath.Distance(knownPos, missile.GlobalPosition()) < (double)terminalRange)
        {
            lastTargetReturnStrength = GetTargetRadarReturn();
            Missile.SeekerMode newSeekerMode = lastTargetReturnStrength > (double)radarParameters.minSignal
                ? Missile.SeekerMode.activeLock
                : Missile.SeekerMode.activeSearch;
            if (seekerMode != newSeekerMode)
                seekerMode = newSeekerMode;
        }

        if (lastTargetReturnStrength > (double)radarParameters.minSignal)
        {
            knownPos = this.targetUnit.GlobalPosition();
            knownVel = this.targetUnit.rb != null ? this.targetUnit.rb.velocity : Vector3.zero;
            radarLockEstablished = true;
            if (achievedLock || !(this.targetUnit is Aircraft targetUnit))
                return;
            targetUnit.RecordDamage(missile.ownerID, 1f / 1000f);
            achievedLock = true;
        }
        else
        {
            if (missile.NetworkHQ.IsTargetBeingTracked(targetUnit))
                knownVel = targetUnit.rb != null ? targetUnit.rb.velocity : Vector3.zero;
            if (missile.NetworkHQ.IsTargetPositionAccurate(targetUnit, 2000f))
            {
                knownPos = missile.NetworkHQ.GetKnownPosition(targetUnit).Value;
                knownPos += positionalErrorVector;
            }

            if (maxDatalinkAngle < 180.0 &&
                Vector3.Angle(missile.transform.forward, FastMath.Direction(missile.GlobalPosition(), knownPos)) >
                (double)maxDatalinkAngle)
                knownPos = missile.GlobalPosition() + missile.transform.forward * 10000f;
            if (FastMath.InRange(knownPos, targetUnit.GlobalPosition(), 2000f))
                return;
            missile.SetTarget(null);
            targetUnit = null;
        }
    }

    private void TerminalMode()
    {
        lastTargetReturnStrength = GetTargetRadarReturn();

        Missile.SeekerMode newSeekerMode = lastTargetReturnStrength > (double)radarParameters.minSignal
            ? Missile.SeekerMode.activeLock
            : Missile.SeekerMode.activeSearch;
        if (seekerMode != newSeekerMode)
            seekerMode = newSeekerMode;

        if (lastTargetReturnStrength < (double)radarParameters.minSignal)
        {
            if (lastTargetReturnStrength == -1.0)
                missile.SetTarget(null);
            homingLockTime = 0.0f;
            timeWithoutReturn += Time.deltaTime;
            GlobalPosition knownPosition;
            if (missile.NetworkHQ.TryGetKnownPosition(targetUnit, out knownPosition))
                knownPos = knownPosition + positionalErrorVector;
            if (Vector3.Angle(knownPos - missile.GlobalPosition(), missile.transform.forward) >
                (double)maxTrackingAngle)
                knownPos = missile.GlobalPosition() + missile.transform.forward * 1000f;
            else
                knownPos += knownVel * Time.fixedDeltaTime;
            if (timeWithoutReturn <= (double)lockPerseverance)
                return;
            missile.SetTarget(null);
        }
        else
        {
            homingLockTime += Time.fixedDeltaTime;
            timeWithoutReturn = 0.0f;
            if (homingLockTime > (double)homingLockDelay)
            {
                knownPos = targetUnit.GlobalPosition();
                knownVel = targetUnit.rb != null ? targetUnit.rb.velocity : Vector3.zero;
                knownAccel = (knownVel - knownVelPrev) / Time.fixedDeltaTime;
                knownVelPrev = knownVel;
            }

            missile.SetTarget(targetUnit);
        }
    }

    public RadarParams GetRadarParams()
    {
        return radarParameters;
    }
}