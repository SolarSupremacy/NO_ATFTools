using ATFTools.Core;
using HarmonyLib;
using NuclearOption.Networking;

namespace ATFTools.Patch;



[HarmonyPatch(typeof(Player))]
public class PlayerPatch
{
    [HarmonyPatch(nameof(Player.GetDisplayName))]
    [HarmonyPrefix]
    public static bool GetDisplayNamePrefix(Player __instance, ref string __result)
    {
        Plugin.Log?.LogInfo($"Get display name for {__instance.SteamID}: {PlayerNames.GetName(__instance.SteamID)}");
        string name = PlayerNames.GetName(__instance.SteamID);
        if (name == "") return true;

        __result = name;
        return false;
    }
}