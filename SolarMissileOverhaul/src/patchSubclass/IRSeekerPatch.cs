using System.Collections.Generic;
using System.Linq;
using System;
using HarmonyLib;
using UnityEngine;
using Debug = System.Diagnostics.Debug;
using Random = UnityEngine.Random;

namespace missileoverhaul.patchSubclass;

// IRSeekerOverhaul: Drop-in replacement for IRSeeker with better functionality.
public sealed class IRSeekerOverhaul : IRSeeker
{
    // Unity prefab references.
    private static readonly AccessTools.FieldRef<IRSeeker, float>
        flareRejectionRef =
            AccessTools.FieldRefAccess<IRSeeker, float>("flareRejection");

    private static readonly AccessTools.FieldRef<IRSeeker, float>
        positionalErrorRef =
            AccessTools.FieldRefAccess<IRSeeker, float>("positionalError");

    private static readonly AccessTools.FieldRef<IRSeeker, float>
        driftRateRef =
            AccessTools.FieldRefAccess<IRSeeker, float>("driftRate");

    private static readonly AccessTools.FieldRef<IRSeeker, float>
        guidanceDelayRef =
            AccessTools.FieldRefAccess<IRSeeker, float>("guidanceDelay");

    private static readonly AccessTools.FieldRef<IRSeeker, float>
        tangibleDelayRef =
            AccessTools.FieldRefAccess<IRSeeker, float>("tangibleDelay");

    private static readonly AccessTools.FieldRef<IRSeeker, float>
        selfDestructAtSpeedRef =
            AccessTools.FieldRefAccess<IRSeeker, float>("selfDestructAtSpeed");

    private static readonly AccessTools.FieldRef<IRSeeker, float>
        maxLeadRef =
            AccessTools.FieldRefAccess<IRSeeker, float>("maxLead");

    private static readonly AccessTools.FieldRef<IRSeeker, AnimationCurve>
        rangeFactorRef =
            AccessTools.FieldRefAccess<IRSeeker, AnimationCurve>("rangeFactor");

    // Cannot override serialized prefab fields. Must do harmony shenanigans instead...
    // [SerializeField] private float flareRejection;
    //
    // [SerializeField] private float positionalError;
    //
    // [SerializeField] private float driftRate;
    //
    // [SerializeField] private float guidanceDelay = 0.25f;
    //
    // [SerializeField] private float tangibleDelay = 0.25f;
    //
    // [SerializeField] private float selfDestructAtSpeed = 200f;
    //
    // [SerializeField] private float maxLead = 5f;
    //
    // [SerializeField] private AnimationCurve rangeFactor;

    
    // IRSeeker variables.
    private IRSource? seekerIRTarget;
    private bool achievedLock;
    private float dazzleAmount;
    private Vector3 driftError;
    private Vector3 errorOffset;
    private bool guidance;
    private float lastEvaluated;
    private float topSpeed;

    
    // Tracking variables.
    private float knownTime;
    private GlobalPosition knownPos;
    private Vector3 knownVel;
    private Vector3 knownVelPrev;
    private Vector3 knownAccel;
    private GlobalPosition ghostPos;
    private Vector3 ghostVel;
    private Vector3 ghostAccel;
        
    
    // Static variables.
    private float seekerArmDelay = 0.5f;
    private float seekerMaxRange = 10000f;
    
    
    // Overhaul variables.
    private Unit? launchTarget;
    private bool launchLock;
    // seekerSlewMax
    // Max degrees off boresight seeker can be slewed (half of total FOV).
    // 180 or higher is unlimited.
    private readonly float seekerSlewMax = 60f;
    // seekerFOV
    // Field of view of the seeker sensor in degrees.
    private readonly float seekerFOV = 2.5f;

    
    // Unity prefabs.
    private float flareRejection =>
        flareRejectionRef(this);

    private float positionalError =>
        positionalErrorRef(this);

    private float driftRate =>
        driftRateRef(this);

    private float guidanceDelay =>
        guidanceDelayRef(this);

    private float tangibleDelay =>
        tangibleDelayRef(this);

    private float selfDestructAtSpeed =>
        selfDestructAtSpeedRef(this);

    private float maxLead =>
        maxLeadRef(this);

    private AnimationCurve rangeFactor =>
        rangeFactorRef(this);

    
    public override void Initialize(Unit? target, GlobalPosition aimpoint)
    {
        // Initiate flight parameters.
        missile.NetworkseekerMode = Missile.SeekerMode.passive;
        errorOffset = Random.insideUnitSphere;
        topSpeed = missile.GetWeaponInfo().GetMaxSpeed();
        lastEvaluated = Time.timeSinceLevelLoad;
        missile.onDisableUnit += IRSeeker_OnMissileDestroyed;

        GetEvasionPoint();

        knownPos = aimpoint;
        knownVel = Vector3.zero;
        knownAccel = Vector3.zero;
        knownTime = Time.timeSinceLevelLoad;
        
        // Target logic.
        launchTarget = target;
        launchLock = SeekerCheck(target?.GetIRSource(), target?.transform.GlobalPosition());

        if (launchTarget != null)
        {
            if (launchLock)
            {
                SeekerLockUnit(launchTarget);
                SeekerLockIR(launchTarget.GetIRSource());
                knownPos = launchTarget.transform.GlobalPosition();
                knownVel = !(launchTarget != null) || !(launchTarget.rb != null) ? Vector3.zero : launchTarget.rb.velocity;
            }
            else
            {
                GlobalPosition? targetDatalink = missile.NetworkHQ.GetKnownPosition(launchTarget);
                if (targetDatalink.HasValue)
                {
                    knownPos = (GlobalPosition) targetDatalink;
                    if (missile.NetworkHQ.IsTargetBeingTracked(launchTarget))
                        knownVel = !(launchTarget != null) || !(launchTarget.rb != null) ? Vector3.zero : launchTarget.rb.velocity;
                }
            }
            
        }

        Plugin.Log?.LogDebug($"  IR {missile.persistentID} # . . . Launch target: {launchTarget} - Launch lock: {launchLock}");

        this.StartSlowUpdateDelayed(1.0f, SlowChecks);
    }

    public override string GetSeekerType() => "IR";

    private void Guidance(GlobalPosition targetPos, Vector3 targetVel, Vector3 targetAccel)
    {
        // Calculate lead vector.
        Vector3 platformVel = FastMath.NormalizedDirection(missile.GlobalPosition(), targetPos) *
                              (missile.timeSinceSpawn < 3.0 ? topSpeed : missile.speed);
        Vector3 leadVector = TargetCalc.GetLeadVectorWithAccel(targetPos, missile.GlobalPosition(), targetVel, platformVel,
            targetAccel, maxLead) + (driftError + errorOffset * (dazzleAmount + positionalError));

        // Calculate aim point.
        GlobalPosition aimPoint = targetPos + leadVector;
        aimPoint.y = Mathf.Max(aimPoint.y, 0.0f);
        
        // Update aimpoint.
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

    private bool SeekerCheck(IRSource? target, GlobalPosition? slewPosition)
    {
        // If no target or target doesn't have transform, just return false.
        if (target == null || target.transform == null)
            return false;
        // If slew is null, assign slew to forward.
        slewPosition ??= transform.GlobalPosition() + transform.forward * 1000f;
        // If seeker is requested to slew past maximum slew angle, return false.
        if (seekerSlewMax < 180.0f &&
            Vector3.Angle((GlobalPosition) slewPosition - transform.GlobalPosition(), transform.forward) >
            seekerSlewMax)
        {
            // Plugin.Log?.LogDebug(
            //     $"{missile.persistentID} . # . . Check failed due to out of slew angle ({Vector3.Angle((GlobalPosition) slewPosition - transform.GlobalPosition(), transform.forward)}).");
            return false;
        }

        // If target is out of maximum range, return false.
        if (!FastMath.InRange(transform.GlobalPosition(), target.transform.GlobalPosition(), seekerMaxRange))
        {
            // Plugin.Log?.LogDebug(
            //     $"{missile.persistentID} . # . . Check failed due to out of range ({FastMath.Distance((GlobalPosition) slewPosition, transform.GlobalPosition())}).");
            return false;
        }
        // If target is outside of slewed seeker FOV, return false;
        if (Vector3.Angle((GlobalPosition) slewPosition - transform.GlobalPosition(), target.transform.GlobalPosition() - transform.GlobalPosition()) >
            seekerFOV)
        {
            // Plugin.Log?.LogDebug(
            //     $"{missile.persistentID} . # . . Check failed due to out of FOV ({Vector3.Angle((GlobalPosition) slewPosition - transform.GlobalPosition(), target.transform.GlobalPosition() - transform.GlobalPosition())}).");
            return false;
        }
        // If seeker doesn't have line of sight to target, return false.
        if (Physics.Linecast(transform.position, target.transform.position, out _, PhysicsLayers.StaticsMask))
        {
            // Plugin.Log?.LogDebug(
            //     $"{missile.persistentID} . # . . Check failed due to line of sight.");
            return false;
        }
        // TODO: Implement more logic here for seeker sensitivity and shit later...
        
        return true;
    }
    
    private bool SeekerIsLocked()
    {
        bool result = seekerIRTarget != null && seekerIRTarget.transform != null;
        if (!result) seekerIRTarget = null;
        return result;
    }

    private void SeekerLockIR(IRSource? source)
    {
        seekerIRTarget = source;
    }

    private void SeekerLockUnit(Unit? unit)
    {
        // SeekerLockIR(unit != null ? unit.GetIRSource() : null);
        
        if (targetUnit != null) targetUnit.onAddIRSource -= IRSeeker_OnTargetFlare;
        targetUnit = unit;
        if (targetUnit != null) targetUnit.onAddIRSource += IRSeeker_OnTargetFlare;

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
        
        // Handle self-destruct logic.
        if (missile.EngineOn()
            || missile.timeSinceSpawn < 8.0f
            || (!missile.LosingGround() && !missile.MissedTarget() && missile.speed >= selfDestructAtSpeed))
            return;
        
        if (missile.LosingGround())
            Plugin.Log?.LogInfo($"{missile.persistentID} . . . # Self-destruct: Losing ground.");
        if (missile.MissedTarget())
            Plugin.Log?.LogInfo($"{missile.persistentID} . . . # Self-destruct: Missed target.");
        if (missile.speed < selfDestructAtSpeed)
            Plugin.Log?.LogInfo($"{missile.persistentID} . . . # Self-destruct: Below minimum speed ({missile.speed}).");
        
        missile.Detonate(missile.rb.velocity, false, false);
    }

    public override void Seek()
    {
        float now = Time.timeSinceLevelLoad;
        
        // Make tangible after tangibleDelay and a bunch of other bullshit.
        if (!missile.IsTangible() && missile.timeSinceSpawn > (double)tangibleDelay && (missile.owner == null ||
                (FastMath.OutOfRange(missile.owner.GlobalPosition(), missile.GlobalPosition(), 50f) &&
                 Vector3.Dot(missile.owner.GlobalPosition() - missile.GlobalPosition(), missile.rb.velocity) < 0.0)))
            missile.SetTangible(true);

        // Enable guidance after guidanceDelay.
        if (!guidance && missile.timeSinceSpawn > (double)guidanceDelay)
        {
            missile.DeployFins();
            guidance = true;
        }

        // Occasional target checking...
        if (now - lastEvaluated > 0.25f)
        {
            lastEvaluated = now;
            
            if (SeekerIsLocked())
            {
                // Check to see if seeker should lose lock.
                if (!SeekerCheck(seekerIRTarget, seekerIRTarget?.transform.GlobalPosition()))
                {
                    Plugin.Log?.LogDebug($"  IR {missile.persistentID} . . # . Lost lock.");
                    SeekerLockUnit(null);
                    SeekerLockIR(null);
                }
            }
            else
            {
                Unit? newTarget = FindNewTarget(ghostPos);

                if (newTarget != null)
                {
                    Plugin.Log?.LogDebug($"  IR {missile.persistentID} . # . . Acquired lock: {newTarget.unitName}");
                    SeekerLockUnit(newTarget);
                    SeekerLockIR(newTarget.GetIRSource());
                }
            }
        }

        // Only continue if guidance is enabled.
        if (!guidance) return;
        
        if (SeekerIsLocked())
        {
            driftError = Vector3.zero;

            Debug.Assert(seekerIRTarget != null, nameof(seekerIRTarget) + " != null");
            knownPos = seekerIRTarget.transform.GlobalPosition();
            knownVel = !(targetUnit != null) || !(targetUnit.rb != null) ? Vector3.zero : targetUnit.rb.velocity;
            knownAccel = (knownVel - knownVelPrev) / Time.fixedDeltaTime;
            knownVelPrev = knownVel;
            knownTime = now;

            ghostPos = knownPos;
            ghostVel = knownVel;
            ghostAccel = knownAccel;
        }
        else
        {
            driftError += Random.insideUnitSphere * (float)(driftRate * (double)Time.deltaTime / 2.0);
                
            ghostVel = knownVel + knownAccel * (now - knownTime);
            ghostPos = PredictGhost(knownPos, knownVel, knownAccel, now - knownTime);
            ghostAccel = Vector3.zero;
                
            // Plugin.Log?.LogDebug($"  IR {missile.persistentID} . . # . IR ghost accel: {ghostPos} {ghostVel} {ghostAccel}");
        }

        Guidance(ghostPos, ghostVel, ghostAccel);
    }

    private void IRSeeker_OnTargetFlare(IRSource source)
    {
        // TODO: Implement new flare CCM mechanics.
        if (missile.targetID.NotValid)
        {
            // If the mod crashes, remove next line. I'm not certain if needs to be here but if this is *really* an
            // issue Mitch felt needed addressing, my next line might help prevent a memory leak associated with it.
            if (targetUnit != null) targetUnit.onAddIRSource -= new Action<IRSource>(this.IRSeeker_OnTargetFlare);
            return;
        }
        var num1 = RangeCoef(FastMath.Distance(missile.transform.position, seekerIRTarget.transform.position));
        var vector3 = FastMath.NormalizedDirection(missile.transform.position, seekerIRTarget.transform.position);
        var rhs = FastMath.NormalizedDirection(source.transform.position, seekerIRTarget.transform.position);
        var num2 = Mathf.Clamp01(1f - Mathf.Abs(Vector3.Dot(vector3, rhs)));
        var num3 = AspectCoef(vector3);
        var num4 = Mathf.Clamp01(BackgroundBrightness(vector3)) * 2f;
        var num5 = (float)(seekerIRTarget.intensity * (1.0 + num3) / (num1 + (double)num4));
        dazzleAmount += (1f + num2) / flareRejection;
        if (dazzleAmount <= (double)num5)
            return;
        Plugin.Log?.LogDebug($"  IR {missile.persistentID} . . # . IR locked onto flares.");
        // SeekerLockUnit(null); // TODO: Change later.
        SeekerLockUnit(null);
        SeekerLockIR(source);
    }

    private float AspectCoef(Vector3 targetVector)
    {
        return (float)(Mathf.Clamp01(Vector3.Dot(-seekerIRTarget.transform.forward, targetVector)) * 0.5 +
                       Mathf.Clamp01(Vector3.Dot(seekerIRTarget.transform.forward, targetVector)) * 2.0);
    }

    private float RangeCoef(float targetDistance)
    {
        float maxRange = missile.GetWeaponInfo().targetRequirements.maxRange;
        return rangeFactor.Evaluate(targetDistance / maxRange);
    }

    private float BackgroundBrightness(Vector3 targetVector)
    {
        float cloudOcclusion = NetworkSceneSingleton<LevelInfo>.i.GetCloudOcclusion(missile.transform.position);
        float b = NetworkSceneSingleton<LevelInfo>.i.sun.color.b;
        return Mathf.Clamp01(Vector3.Dot(targetVector, -NetworkSceneSingleton<LevelInfo>.i.sun.transform.forward)) *
               (1f - cloudOcclusion) * b;
    }

    private void IRSeeker_OnMissileDestroyed(Unit unit)
    {
        SeekerLockUnit(null);
        SeekerLockIR(null);
        Plugin.Log?.LogDebug($"  IR {missile.persistentID} . . . # Destroyed!");
    }

    private Unit? FindNewTarget(GlobalPosition? slewPosition)
    {
        
        // If requested slew angle is too large, just skip.
        slewPosition ??= transform.GlobalPosition() + transform.forward * 1000f;
        if (seekerSlewMax < 180.0f &&
            Vector3.Angle((GlobalPosition) slewPosition - transform.GlobalPosition(), transform.forward) >
            seekerSlewMax)
            return null;

        List<FactionHQ> allHQs = FactionRegistry.GetAllHQs().ToList();
        ShuffleList(allHQs);
        
        // List<Unit> validUnits = new List<Unit>();

        GlobalPosition currentPos = missile.transform.GlobalPosition();

        foreach (FactionHQ hq in allHQs)
        {
            
            foreach (int i in RandomOrder(hq.factionRadarReturn.Count))
            {
                // Make sure unit is valid.
                if (!UnitRegistry.TryGetUnit(hq.factionRadarReturn[i], out Unit unit)) continue;
                // Make sure unit is not itself, not its owner, and has an IR source.
                if (unit.persistentID == missile.persistentID || unit.persistentID == missile.owner.persistentID ||
                    !unit.HasIRSignature()) continue;
                // Skip if not aircraft.
                if (unit is not Aircraft) continue;
                // if (!(FastMath.Distance(missile.transform.position, unit.transform.position) > 100f)) continue;
                // Check if unit is visible to seeker.
                if (!SeekerCheck(unit.GetIRSource(), slewPosition)) continue;
                
                // Stop on first valid unit and return.
                return unit;
                
                // validUnits.Add(unit);
            }
        }

        return null;
        
        // everything after is old

        // Unit? bestTarget = null;
        // float maxDesire = float.MinValue;
        // float desire = float.MinValue;
        //
        // foreach (var unit in validUnits)
        // {
        //     desire = unit.GetIRSource().intensity - Vector3.Angle(unit.transform.position - transform.position, transform.forward);
        //     // / FastMath.Distance(unit.transform.position, currentPos)
        //     Plugin.Log?.LogDebug(
        //         $"[IRSeeker] Missile {missile.persistentID} found target {unit.persistentID} with intensity '{desire}'.");
        //     if (!(desire > maxDesire)) continue;
        //     maxDesire = desire;
        //     bestTarget = unit;
        // }
        //
        // return bestTarget;
    }

    // Returns predicted global position of ghost.
    private static GlobalPosition PredictGhost(
        GlobalPosition targetPos,
        Vector3 targetVel,
        Vector3 targetAccel,
        float deltaTime)
    {
        Vector3 displacement = targetVel * deltaTime + targetAccel * deltaTime * deltaTime * 0.5f;
        return new GlobalPosition(targetPos.AsVector3() + displacement);
    }
    
    // Shuffles a list.
    private static void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
    
    // Random order.
    private static int[] RandomOrder(int count)
    {
        int[] values = [.. Enumerable.Range(0, count)];

        for (int i = values.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }

        return values;
    }
}