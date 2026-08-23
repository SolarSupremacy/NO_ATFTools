using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace missileoverhaul;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGUID = "vet.solar.missileoverhaul";
    public const string PluginName = "Solar Missile Overhaul";
    public const string PluginVersion = "0.1";

    private Harmony? _harmony;

    internal static ManualLogSource? Log { get; private set; }

    private void Awake()
    {
        Log = Logger;

        Log.LogInfo("Version " + PluginVersion + " loading...");

        _harmony = new Harmony(PluginGUID);
        _harmony.PatchAll();

        Log.LogInfo("Loaded.");
    }

    private void OnDestroy()
    {
        Log ??= Logger;

        Log.LogInfo("Unloading...");

        _harmony?.UnpatchSelf();

        Log.LogInfo("Unloaded.");
    }

    // public void ToggleUnits()
    // {
    //     PlayerSettings.unitSystem = PlayerSettings.UnitSystem.Metric;
    // }
}

// [HarmonyPatch(typeof(IRSeeker), "Initialize")]
// internal static class IRSeekerInitializePatch
// {
//     [HarmonyPrefix]
//     private static bool Prefix(
//         IRSeeker __instance,
//         ref Unit target,
//         ref GlobalPosition aimpoint)
//     {
//         if (target == null) Plugin.Log?.LogInfo("IRSeeker initialized without target!");
//
//         return true;
//     }
// }

// EVERYTHING BEYOND THIS IS TRASH

// [HarmonyPatch(typeof(IRSeeker), "Seek")]
// internal static class IRSeekerSeekPatch
// {
//     internal static ManualLogSource? Log { get; private set; }
//     
//     private static readonly MethodInfo IRLockCheckMethod =
//         AccessTools.Method(typeof(IRSeeker), "IRLockCheck");
//     
//     private static readonly MethodInfo AspectCoef =
//         AccessTools.Method(typeof(IRSeeker), "AspectCoef");
//     
//     private static readonly MethodInfo RangeCoef =
//         AccessTools.Method(typeof(IRSeeker), "RangeCoef");
//     
//     private static readonly MethodInfo BackgroundBrightness =
//         AccessTools.Method(typeof(IRSeeker), "BackgroundBrightness");
//
//     [HarmonyPrefix]
//     private static bool Prefix(
//         IRSeeker __instance,
//
//         // MissileSeeker fields
//         Missile ___missile,
//         ref Unit ___targetUnit,
//
//         // IRSeeker fields
//         ref IRSource ___IRTarget,
//         ref GlobalPosition ___knownPos,
//         ref Vector3 ___knownVel,
//         ref Vector3 ___knownVelPrev,
//         ref Vector3 ___knownAccel,
//         ref Vector3 ___driftError,
//         Vector3 ___errorOffset,
//         float ___positionalError,
//         float ___driftRate,
//         float ___guidanceDelay,
//         float ___tangibleDelay,
//         float ___maxLead,
//         float ___dazzleAmount,
//         float ___lastEvaluated,
//         float ___topSpeed,
//         ref bool ___guidance)
//     {
//         // -------------------------------------------------------------
//         // VANILLA IRSeeker.Seek() — copied into the Harmony Prefix.
//         // Modify this implementation directly.
//         // -------------------------------------------------------------
//         
//         if (!___missile.IsTangible()
//             && ___missile.timeSinceSpawn > ___tangibleDelay
//             && (___missile.owner == null
//                 || (FastMath.OutOfRange(
//                         ___missile.owner.GlobalPosition(),
//                         ___missile.GlobalPosition(),
//                         50f)
//                     && Vector3.Dot(
//                         ___missile.owner.GlobalPosition() - ___missile.GlobalPosition(),
//                         ___missile.rb.velocity) < 0f)
//             ))
//         {
//             ___missile.SetTangible(true);
//         }
//
//         if (!___guidance
//             && ___missile.timeSinceSpawn > ___guidanceDelay)
//         {
//             ___missile.DeployFins();
//             ___guidance = true;
//         }
//
//         // Check for a new target if the missile currently has no target.
//         if (___missile.targetID.NotValid && ___IRTarget == null)
//         {
//             Unit newTarget = CheckForNewTarget(
//                 __instance,
//                 ___missile,
//                 ___targetUnit);
//
//             if (newTarget != null)
//             {
//                 Plugin.Log?.LogInfo(
//                     $"[IRSeeker] Missile {___missile.persistentID} " +
//                     $"acquired new target: {newTarget.unitName}");
//
//                 Retarget(
//                     __instance,
//                     ___missile,
//                     ref ___targetUnit,
//                     ref ___IRTarget,
//                     ref ___knownPos,
//                     ref ___knownVel,
//                     ref ___knownVelPrev,
//                     ref ___knownAccel,
//                     ref ___driftError,
//                     newTarget);
//             }
//         }
//         
//         
//
//         if (Time.timeSinceLevelLoad - ___lastEvaluated > 0.25f
//             && !InvokeIRLockCheck(__instance))
//         {
//             ___IRTarget = null;
//         }
//
//         if (___guidance)
//         {
//             if (___IRTarget != null && ___IRTarget.transform != null)
//             {
//                 ___knownPos = ___IRTarget.transform.GlobalPosition();
//
//                 ___knownVel =
//                     ___targetUnit == null || ___targetUnit.rb == null
//                         ? Vector3.zero
//                         : ___targetUnit.rb.velocity;
//
//                 ___knownAccel =
//                     (___knownVel - ___knownVelPrev)
//                     / Time.fixedDeltaTime;
//
//                 ___knownVelPrev = ___knownVel;
//                 ___driftError = Vector3.zero;
//             }
//             else
//             {
//                 ___driftError +=
//                     Random.insideUnitSphere
//                     * (___driftRate * Time.deltaTime / 2f);
//             }
//         }
//
//         float maxLead = ___maxLead;
//
//         Vector3 platformVel =
//             FastMath.NormalizedDirection(
//                 ___missile.GlobalPosition(),
//                 ___knownPos)
//             * (___missile.timeSinceSpawn < 3f
//                 ? ___topSpeed
//                 : ___missile.speed);
//
//         GlobalPosition aimPoint =
//             ___knownPos
//             + (
//                 TargetCalc.GetLeadVectorWithAccel(
//                     ___knownPos,
//                     ___missile.GlobalPosition(),
//                     ___knownVel,
//                     platformVel,
//                     ___knownAccel,
//                     maxLead)
//                 + (
//                     ___driftError
//                     + ___errorOffset
//                         * (___dazzleAmount + ___positionalError)
//                 )
//             );
//
//         aimPoint.y = Mathf.Max(aimPoint.y, 0f);
//
//         if (PlayerSettings.debugVis)
//         {
//             GameObject gameObject =
//                 Object.Instantiate<GameObject>(
//                     GameAssets.i.debugArrowGreen,
//                     ___missile.transform);
//
//             gameObject.transform.rotation =
//                 Quaternion.LookRotation(
//                     aimPoint - ___missile.GlobalPosition());
//
//             gameObject.transform.localScale =
//                 new Vector3(
//                     1f,
//                     1f,
//                     (aimPoint
//                         - ___missile.GlobalPosition()).magnitude);
//
//             Object.Destroy(gameObject, 0.05f);
//         }
//
//         ___missile.SetAimpoint(aimPoint, ___knownVel);
//         
//         // This method of logging either doesn't work or is too laggy in this specific case and breaks everything.
//         // Plugin.Log?.LogInfo(
//         //     $"IRSeeker.Seek(): missile={___missile.unitName}, " +
//         //     $"target={___targetUnit?.unitName ?? "null"}, " +
//         //     $"IRTarget is flare={(___IRTarget != null ? ___IRTarget.flare: "null")}, " +
//         //     $"time={___missile.timeSinceSpawn:F2}"
//         //     );
//        
//         // Skip Nuclear Option's original IRSeeker.Seek()
//         return false;
//     }
//
//     private static bool InvokeIRLockCheck(IRSeeker seeker)
//     {
//         return (bool)IRLockCheckMethod.Invoke(seeker, null);
//     }
//
//     /// <summary>
//     /// Helper for a true mid-flight target transfer.
//     ///
//     /// Call this after your own target-selection logic chooses a new Unit.
//     /// This updates both Missile's network target and IRSeeker's internal
//     /// target state.
//     ///
//     /// Note: this is deliberately separate from FindNewTarget() because
//     /// the target-selection rules are mod-specific.
//     /// </summary>
//     private static void Retarget(
//         IRSeeker seeker,
//         Missile missile,
//         ref Unit targetUnit,
//         ref IRSource irTarget,
//         ref GlobalPosition knownPos,
//         ref Vector3 knownVel,
//         ref Vector3 knownVelPrev,
//         ref Vector3 knownAccel,
//         ref Vector3 driftError,
//         Unit newTarget)
//     {
//         if (newTarget == null || newTarget == targetUnit)
//             return;
//
//         // Remove the flare-listener subscription from the old target.
//         if (targetUnit != null)
//         {
//             var flareHandlerMethod =
//                 AccessTools.Method(
//                     typeof(IRSeeker),
//                     "IRSeeker_OnTargetFlare");
//
//             if (flareHandlerMethod != null)
//             {
//                 var oldHandler =
//                     (System.Action<IRSource>)System.Delegate.CreateDelegate(
//                         typeof(System.Action<IRSource>),
//                         seeker,
//                         flareHandlerMethod);
//
//                 targetUnit.onAddIRSource -= oldHandler;
//             }
//         }
//
//         // Keep Missile's network-visible target ID in sync.
//         //
//         // On a host, Network_targetID invokes TargetIDChanged immediately.
//         // On a dedicated server, the generated SyncVar setter does not
//         // invoke the hook locally, so explicitly invoke it below.
//         PersistentID oldTargetId = missile.targetID;
//         missile.SetTarget(newTarget);
//
//         if (!missile.IsHost)
//         {
//             MethodInfo targetChanged =
//                 AccessTools.Method(
//                     typeof(Missile),
//                     "TargetIDChanged");
//
//             targetChanged?.Invoke(
//                 missile,
//                 new object[]
//                 {
//                     oldTargetId,
//                     missile.targetID
//                 });
//         }
//
//         targetUnit = newTarget;
//         irTarget = newTarget.GetIRSource();
//
//         // Reset the kinematic estimate so the old target's velocity does
//         // not create a one-frame bogus acceleration spike.
//         knownPos =
//             irTarget != null && irTarget.transform != null
//                 ? irTarget.transform.GlobalPosition()
//                 : newTarget.GlobalPosition();
//
//         knownVel =
//             newTarget.rb != null
//                 ? newTarget.rb.velocity
//                 : Vector3.zero;
//
//         knownVelPrev = knownVel;
//         knownAccel = Vector3.zero;
//         driftError = Vector3.zero;
//
//         // Re-target the proximity fuse if this seeker uses one.
//         //
//         // `proximityFuse` is public on MissileSeeker.
//         if (seeker.proximityFuse)
//         {
//             missile.SetProxyFuse(
//                 newTarget.GetRandomPart().transform,
//                 newTarget.rb);
//         }
//
//         // Subscribe the seeker to flares emitted by the new target.
//         if (irTarget != null && !irTarget.flare)
//         {
//             var flareHandlerMethod =
//                 AccessTools.Method(
//                     typeof(IRSeeker),
//                     "IRSeeker_OnTargetFlare");
//
//             if (flareHandlerMethod != null)
//             {
//                 var newHandler =
//                     (System.Action<IRSource>)System.Delegate.CreateDelegate(
//                         typeof(System.Action<IRSource>),
//                         seeker,
//                         flareHandlerMethod);
//
//                 newTarget.onAddIRSource += newHandler;
//             }
//         }
//     }
//     
//     private static Unit CheckForNewTarget(
//         IRSeeker seeker,
//         Missile missile,
//         Unit previousTarget)
//     {
//         Plugin.Log?.LogDebug(
//             $"[IRSeeker] Missile {missile.persistentID} is searching for a new target.");
//         
//         // Temp Constants
//         float maxRange = 10000f;
//         double seekerCone = 60f;
//
//         // New test
//         List<Unit> validUnits = new List<Unit>();
//         
//         GlobalPosition currentPos = missile.GlobalPosition();
//         foreach (FactionHQ allHq in FactionRegistry.GetAllHQs())
//         {
//             for (int i = 0; i < allHq.factionRadarReturn.Count; ++i)
//             {
//                 // Make sure unit is valid.
//                 Unit unit;
//                 if (UnitRegistry.TryGetUnit(new PersistentID?(allHq.factionRadarReturn[i]), out unit))
//                 {
//                     // Check if unit has an IR source.
//                     if (unit.HasIRSignature())
//                     {
//                         // Check if unit is in range and within seeker cone.
//                         if (FastMath.InRange(unit.GlobalPosition(), currentPos, maxRange) &&
//                             ((double)seekerCone <= 0.0 || (double)Vector3.Angle(
//                                 unit.transform.position - missile.transform.position,
//                                 missile.transform.forward) <= (double)seekerCone)
//                            )
//                         {
//                             validUnits.Add(unit);
//                         }
//                     }
//                 }
//             }
//         }
//
//         Unit? bestTarget = null;
//         float maxIntensity = float.MinValue;
//         float intensity = float.MinValue;
//         
//         foreach (Unit unit in validUnits)
//         {
//             intensity = unit.GetIRSource().intensity;
//         }
//         
//         // End New
//         
//         // Define search radius for finding nearby units
//         float searchRadius = 5000f;
//
//         // Get missile's current position
//         GlobalPosition missilePos = missile.GlobalPosition();
//
//         // Find all units within search radius
//         var nearbyUnits = UnitManager.GetUnitsInRadius(missilePos, searchRadius);
//
//         Unit closestTarget = null;
//         float closestDistance = float.MaxValue;
//
//         // Iterate through nearby units to find valid IR targets
//         foreach (var unit in nearbyUnits)
//         {
//             // Skip if unit is null or invalid
//             if (unit == null || unit.dead)
//                 continue;
//
//             // Skip if unit is on the same team as the missile owner
//             if (missile.owner != null && unit.team == missile.owner.team)
//                 continue;
//
//             // Check if unit has an IR source
//             IRSource irSource = unit.GetIRSource();
//             if (irSource == null)
//                 continue;
//
//             // Skip flares
//             if (irSource.flare)
//                 continue;
//
//             // Calculate distance to potential target
//             float distance = FastMath.Distance(missilePos, unit.GlobalPosition());
//
//             // Track the closest valid target
//             if (distance < closestDistance)
//             {
//                 closestDistance = distance;
//                 closestTarget = unit;
//             }
//         }
//
//         return closestTarget;
//     }
// }