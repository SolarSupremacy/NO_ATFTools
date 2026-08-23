using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using missileoverhaul.lib;
using UnityEngine;

namespace missileoverhaul.patch;

[HarmonyPatch]
public static class MissileOverhaulPatches
{
    [HarmonyPatch(typeof(Missile), nameof(Missile.Awake))]
    [HarmonyPostfix]
    private static void MissileAwakePostfix(Missile __instance, ref MissileSeeker ___seeker)
    {
        __instance.GetOverhaulState();
        // TODO: Handle conversion of old seeker to new guidance and seeker(s)...

        // Set seeker back to null to prevent it from being used.
        ___seeker = null!;
    }

    [HarmonyPatch(typeof(Missile), "LocalStart")]
    [HarmonyPrefix]
    private static bool MissileLocalStartPrefix(Missile __instance, ref GlobalPosition ___aimPoint, ref Unit? ___target)
    {
        if (GameManager.gameState != GameState.SinglePlayer && GameManager.gameState != GameState.Multiplayer)
            return false;
        ___aimPoint = (__instance.owner != null ? __instance.owner.transform.GlobalPosition() : __instance.transform.GlobalPosition()) + (__instance.owner != null ? __instance.owner.transform.forward : __instance.transform.forward) * 100000f;
        __instance.GetGuidance().Initialize(___target, ___aimPoint);
        return false;
    }
    
    [HarmonyPatch(typeof(Missile), "ServerFixedUpdate")]
    [HarmonyPrefix]
    private static bool MissileServerFixedUpdatePrefix(Missile __instance)
    {
        // __instance.airDensity = GameAssets.i.airDensityAltitude.Evaluate(__instance.rb.transform.position.GlobalY() * (1f / 1000f));
        __instance.GetGuidance().UpdateAbstract();
        // Traverse trav = Traverse.Create(__instance);
        // trav.Method("Steering").GetValue();
        // trav.Method("ApplyAero").GetValue();
        // trav.Method("DetectCollisions").GetValue();

        // Just run GuideAbstract() as prefix and then continue.
        return true;
    }
    
    [HarmonyPatch(typeof(Missile), nameof(Missile.GetSeekerType))]
    [HarmonyPrefix]
    private static bool MissileGetSeekerTypePrefix(Missile __instance, ref string __result)
    {
        List<OverhaulSeeker> seekers = __instance.GetSeekers();
        __result = seekers.Aggregate("", (current, seeker) => current + seeker.GetSeekerType() + "|");
        __result = __result.TrimEnd('|');
        // What about __result = string.Join("|", seekers);?
        return false;
    }
    
    [HarmonyPatch(typeof(Missile), nameof(Missile.GetEvasionPoint))]
    [HarmonyPrefix]
    private static bool MissileGetEvasionPointPrefix(Missile __instance, ref GlobalPosition __result)
    {
        __result = __instance.GetSeeker()?.GetEvasionPoint() ?? __instance.GlobalPosition();
        return false;
    }
    
    /*
     * Vanilla ServerFixedUpdate() does:
     *
     *     seeker.Seek();
     *     Steering();
     *     ApplyAero();
     *     DetectCollisions();
     *
     * Patching Steering with a Prefix means:
     *
     *     seeker.Seek();
     *     OUR GUIDANCE;
     *     Steering();
     *     ApplyAero();
     *
     * so we can overwrite whatever aimpoint the seeker produced before
     * vanilla Missile.Steering() consumes it.
     */
    // [HarmonyPatch(typeof(Missile), "Steering")]
    // [HarmonyPrefix]
    // private static void MissileSteeringPrefix(Missile __instance)
    // {
    //     OverhaulGuidance guidance = __instance.GetGuidance();
    //
    //     if (guidance == null)
    //         return;
    //
    //     guidance.GuideAbstract();
    // }
}