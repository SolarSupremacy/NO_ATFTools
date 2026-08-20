using System;
using System.Reflection;
using HarmonyLib;
using missileoverhaul.patchSubclass;
using UnityEngine;

namespace missileoverhaul.patch;

[HarmonyPatch(typeof(Missile), "Awake")]
internal static class PatchMissileAwake
{
    [HarmonyPostfix]
    private static void Postfix(
        Missile __instance,
        ref MissileSeeker ___seeker)
    {
        // If old seeker is recognized, make a new one to replace it.
        var oldSeeker = ___seeker;
        MissileSeeker? newSeeker = null;
        if (oldSeeker is IRSeeker && oldSeeker is not IRSeekerOverhaul)
        {
            newSeeker = oldSeeker.gameObject.AddComponent<IRSeekerOverhaul>();
            CopyPrefabValues((IRSeeker) oldSeeker, (IRSeekerOverhaul) newSeeker);
        }
        else if (oldSeeker is ARHSeeker && oldSeeker is not ARHSeekerPatch)
        {
            newSeeker = oldSeeker.gameObject.AddComponent<ARHSeekerPatch>();
            CopyPrefabValues((ARHSeeker) oldSeeker, (ARHSeekerPatch) newSeeker);
        }

        // If newSeeker is null, we're not doing anything so just return.
        if (newSeeker == null) return;
        
        Plugin.Log?.LogDebug($"[MissilePatch] Replaced {oldSeeker.GetType().Name} with {newSeeker.GetType().Name} on '{__instance.unitName}'.");

        SetField(
            typeof(MissileSeeker),
            "missile",
            newSeeker,
            __instance);

        ___seeker = newSeeker;

        oldSeeker.enabled = false;

        // Plugin.Log?.LogDebug(
        //     $"[MissilePatch] Replaced {oldSeeker.GetType().Name} with " +
        //     $"{newSeeker.GetType().Name} on '{__instance.gameObject.name}'.");
    }

    private static void CopyPrefabValues<TSource, TDestination>(
        TSource source,
        TDestination destination)
        where TDestination : TSource
    {
        var type = typeof(TSource);

        while (type != null && type != typeof(MonoBehaviour))
        {
            var fields = type.GetFields(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly);

            foreach (var field in fields)
            {
                if (!IsUnitySerializedField(field))
                    continue;

                var value = field.GetValue(source);
                // Plugin.Log?.LogDebug($"Field: {type.Name}.{field.Name}: {value}");
                field.SetValue(destination, value);

            }

            type = type.BaseType;
        }
    }

    private static bool IsUnitySerializedField(FieldInfo field)
    {
        if (field.IsStatic || field.IsLiteral || field.IsInitOnly)
            return false;

        if (field.IsDefined(typeof(NonSerializedAttribute), false))
            return false;

        if (field.IsPublic)
            return true;

        return field.IsDefined(typeof(SerializeField), false);
    }

    private static void SetField(
        Type declaringType,
        string fieldName,
        object instance,
        object value)
    {
        var field = AccessTools.Field(declaringType, fieldName);

        if (field == null)
        {
            Plugin.Log?.LogError(
                $"[MissilePatch] Could not find field " +
                $"{declaringType.FullName}.{fieldName}");
            return;
        }

        field.SetValue(instance, value);
    }
}