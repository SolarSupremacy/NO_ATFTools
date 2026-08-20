using HarmonyLib;
using UnityEngine;

namespace missileoverhaul.patchSubclass_Backup;

// IRSeekerOverhaul: Drop-in replacement for IRSeeker with better functionality.
public sealed class IRSeekerOverhaul_Backup : IRSeeker
{
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

    private IRSource IRTarget;
    private bool achievedLock;
    private float dazzleAmount;
    private Vector3 driftError;
    private Vector3 errorOffset;
    private bool guidance;
    private Vector3 knownAccel;
    private GlobalPosition knownPos;
    private Vector3 knownVel;
    private Vector3 knownVelPrev;
    private float lastEvaluated;
    private bool targetOnLaunch;
    private float topSpeed;

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

    public override void Initialize(Unit target, GlobalPosition aimpoint)
    {
        Plugin.Log?.LogInfo($"IRSeekerOverhaul Initialized for IR Missile! Target: {target?.unitName ?? "N/A"}");
        targetUnit = target;
        errorOffset = Random.insideUnitSphere;
        missile.NetworkseekerMode = Missile.SeekerMode.passive;
        topSpeed = missile.GetWeaponInfo().GetMaxSpeed();
        var nullable = targetUnit != null ? missile.NetworkHQ.GetKnownPosition(targetUnit) : new GlobalPosition?();
        knownPos = missile.GlobalPosition() + missile.transform.forward * 10000f;
        missile.SetAimpoint(knownPos, Vector3.zero);
        if (proximityFuse && target != null)
            missile.SetProxyFuse(target.GetRandomPart().transform, target.rb);
        if (targetUnit == null || !targetUnit.HasIRSignature() || !nullable.HasValue ||
            FastMath.OutOfRange(targetUnit.GlobalPosition(), nullable.Value, 500f) ||
            !targetUnit.LineOfSight(transform.position, 1000f))
        {
            LoseLock();
        }
        else
        {
            targetOnLaunch = true;
            IRTarget = targetUnit.GetIRSource();
            if (IRTarget == null || IRTarget.flare)
                LoseLock();
            else
                targetUnit.onAddIRSource += IRSeeker_OnTargetFlare;
        }

        lastEvaluated = Time.timeSinceLevelLoad;
        missile.onDisableUnit += IRSeeker_OnMissileDestroyed;
        this.StartSlowUpdateDelayed(0.5f, SlowChecks);
    }

    private void SlowChecks()
    {
        if (missile.disabled || missile.EngineOn() || (!missile.LosingGround() && !missile.MissedTarget() &&
                                                       missile.speed >= (double)selfDestructAtSpeed &&
                                                       (!targetOnLaunch || !(targetUnit == null))))
            return;
        missile.Detonate(missile.rb.velocity, false, false);
    }

    public override string GetSeekerType()
    {
        return "IR";
    }

    public override void Seek()
    {
        if (!missile.IsTangible() && missile.timeSinceSpawn > (double)tangibleDelay && (missile.owner == null ||
                (FastMath.OutOfRange(missile.owner.GlobalPosition(), missile.GlobalPosition(), 50f) &&
                 Vector3.Dot(missile.owner.GlobalPosition() - missile.GlobalPosition(), missile.rb.velocity) < 0.0)))
            missile.SetTangible(true);
        if (!guidance && missile.timeSinceSpawn > (double)guidanceDelay)
        {
            missile.DeployFins();
            guidance = true;
        }

        if (Time.timeSinceLevelLoad - (double)lastEvaluated > 0.25 && !IRLockCheck())
            IRTarget = null;
        if (guidance)
        {
            if (IRTarget != null && IRTarget.transform != null)
            {
                knownPos = IRTarget.transform.GlobalPosition();
                knownVel = !(targetUnit != null) || !(targetUnit.rb != null) ? Vector3.zero : targetUnit.rb.velocity;
                knownAccel = (knownVel - knownVelPrev) / Time.fixedDeltaTime;
                knownVelPrev = knownVel;
                driftError = Vector3.zero;
            }
            else
            {
                driftError += Random.insideUnitSphere * (float)(driftRate * (double)Time.deltaTime / 2.0);
            }
        }

        var maxLead = this.maxLead;
        var platformVel = FastMath.NormalizedDirection(missile.GlobalPosition(), knownPos) *
                          (missile.timeSinceSpawn < 3.0 ? topSpeed : missile.speed);
        var aimPoint = knownPos +
                       (TargetCalc.GetLeadVectorWithAccel(knownPos, missile.GlobalPosition(), knownVel, platformVel,
                           knownAccel, maxLead) + (driftError + errorOffset * (dazzleAmount + positionalError)));
        aimPoint.y = Mathf.Max(aimPoint.y, 0.0f);
        if (PlayerSettings.debugVis)
        {
            var gameObject = Instantiate(GameAssets.i.debugArrowGreen, missile.transform);
            gameObject.transform.rotation = Quaternion.LookRotation(aimPoint - missile.GlobalPosition());
            gameObject.transform.localScale = new Vector3(1f, 1f, (aimPoint - missile.GlobalPosition()).magnitude);
            Destroy(gameObject, 0.05f);
        }

        missile.SetAimpoint(aimPoint, knownVel);
    }

    private bool IRLockCheck()
    {
        lastEvaluated = Time.timeSinceLevelLoad;
        if (IRTarget == null || IRTarget.transform == null)
            return false;
        if (Physics.Linecast(transform.position, IRTarget.transform.position, out _, PhysicsLayers.StaticsMask))
        {
            LoseLock();
            IRTarget = null;
            return false;
        }

        if (!achievedLock && this.targetUnit is Aircraft targetUnit)
        {
            targetUnit.RecordDamage(missile.ownerID, 1f / 1000f);
            achievedLock = true;
        }

        return true;
    }

    private void IRSeeker_OnTargetFlare(IRSource source)
    {
        if (missile.targetID.NotValid)
            return;
        var num1 = RangeCoef(FastMath.Distance(transform.position, IRTarget.transform.position));
        var vector3 = FastMath.NormalizedDirection(transform.position, IRTarget.transform.position);
        var rhs = FastMath.NormalizedDirection(source.transform.position, IRTarget.transform.position);
        var num2 = Mathf.Clamp01(1f - Mathf.Abs(Vector3.Dot(vector3, rhs)));
        var num3 = AspectCoef(vector3);
        var num4 = Mathf.Clamp01(BackgroundBrightness(vector3)) * 2f;
        var num5 = (float)(IRTarget.intensity * (1.0 + num3) / (num1 + (double)num4));
        dazzleAmount += (1f + num2) / flareRejection;
        if (dazzleAmount <= (double)num5)
            return;
        LoseLock();
        IRTarget = source;
    }

    private float AspectCoef(Vector3 targetVector)
    {
        return (float)(Mathf.Clamp01(Vector3.Dot(-IRTarget.transform.forward, targetVector)) * 0.5 +
                       Mathf.Clamp01(Vector3.Dot(IRTarget.transform.forward, targetVector)) * 2.0);
    }

    private float RangeCoef(float targetDistance)
    {
        var maxRange = missile.GetWeaponInfo().targetRequirements.maxRange;
        if (rangeFactor == null)
        {
            Plugin.Log?.LogError("[IRSeekerPatch] Range factor is null!");
            return 1f;
        }

        return rangeFactor.Evaluate(targetDistance / maxRange);
    }

    private float BackgroundBrightness(Vector3 targetVector)
    {
        var cloudOcclusion = NetworkSceneSingleton<LevelInfo>.i.GetCloudOcclusion(transform.position);
        var b = NetworkSceneSingleton<LevelInfo>.i.sun.color.b;
        return Mathf.Clamp01(Vector3.Dot(targetVector, -NetworkSceneSingleton<LevelInfo>.i.sun.transform.forward)) *
               (1f - cloudOcclusion) * b;
    }

    private void LoseLock()
    {
        if (targetUnit != null)
            targetUnit.onAddIRSource -= IRSeeker_OnTargetFlare;
        if (missile.disabled)
            return;
        missile.SetTarget(null);
    }

    private void IRSeeker_OnMissileDestroyed(Unit unit)
    {
        LoseLock();
    }
}