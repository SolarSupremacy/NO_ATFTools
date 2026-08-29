using ATFTools.Core;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine.Windows;

namespace ATFTools;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGUID = "vet.solar.ATFTools";
    public const string PluginName = "Solar's ATF Tools";
    public const string PluginVersion = "0.1";

    private Harmony? _harmony;

    internal static ManualLogSource? Log { get; private set; }

    private void Awake()
    {
        Log = Logger;

        Log.LogInfo("Version " + PluginVersion + " loading...");

        PlayerNames.Initialize();

        ATFInput.Initialize(Log);

        _harmony = new Harmony(PluginGUID);
        _harmony.PatchAll();

        Log.LogInfo("Loaded.");
    }

    private void Update()
    {
        if (ATFInput.ToggleUnitsPressed())
        {
            ToggleUnits();
        }
    }

    private void OnDestroy()
    {
        Log ??= Logger;

        Log.LogInfo("Unloading...");

        _harmony?.UnpatchSelf();

        Log.LogInfo("Unloaded.");
    }

    private void ToggleUnits()
    {
        // Replace this with whatever Nuclear Option setting/property
        // you're already modifying.

        bool currentlyMetric = PlayerSettings.unitSystem == PlayerSettings.UnitSystem.Metric;

        PlayerSettings.unitSystem = currentlyMetric ? PlayerSettings.UnitSystem.Imperial : PlayerSettings.UnitSystem.Metric;
    }
}
