using System;
using System.Collections;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using Rewired;

namespace ATFTools.Core;

public static class ATFInput
{
    /// <summary>
    /// Internal Rewired action name.
    ///
    /// Keep this unique so it cannot collide with the base game or another mod.
    /// This is what you pass to Rewired Player.GetButtonDown().
    /// </summary>
    public const string ToggleUnitsAction = "ATFTools::ToggleUnits";

    /// <summary>
    /// Name displayed in Nuclear Option's Controls binding menu.
    /// </summary>
    private const string ToggleUnitsDisplayName = "Toggle Metric / Imperial";

    /// <summary>
    /// Known values:
    /// "flight", "gameplay"
    /// </summary>
    private const string TargetCategory = "gameplay";

    /// <summary>
    /// Starting action ID for custom mod actions.
    ///
    /// The registration code will move upward from this value if the ID
    /// is already occupied.
    /// </summary>
    private const int StartingActionId = 880;

    private static ManualLogSource? _logger;

    /// <summary>
    /// True after the action has been injected into Rewired.
    /// </summary>
    public static bool Ready { get; private set; }

    /// <summary>
    /// Actual Rewired action ID assigned at runtime.
    /// </summary>
    public static int ToggleUnitsActionId { get; private set; } = -1;

    /// <summary>
    /// Call from your plugin's Awake().
    /// </summary>
    public static void Initialize(ManualLogSource logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Returns true only on the frame the configured button is pressed.
    ///
    /// Intended usage:
    ///
    /// if (Input.ToggleUnitsPressed())
    ///     ToggleUnits();
    /// </summary>
    public static bool ToggleUnitsPressed()
    {
        if (!Ready || !ReInput.isReady)
            return false;

        try
        {
            Player player = ReInput.players?.GetPlayer(0);

            return player != null &&
                   player.GetButtonDown(ToggleUnitsAction);
        }
        catch (Exception e)
        {
            _logger?.LogError(
                $"Failed reading '{ToggleUnitsAction}': {e}"
            );

            return false;
        }
    }

    /// <summary>
    /// Equivalent to Rewired's GetButton() for this action.
    /// Useful if you ever need to know whether the button is currently held.
    /// </summary>
    public static bool ToggleUnitsHeld()
    {
        if (!Ready || !ReInput.isReady)
            return false;

        try
        {
            Player player = ReInput.players?.GetPlayer(0);

            return player != null &&
                   player.GetButton(ToggleUnitsAction);
        }
        catch (Exception e)
        {
            _logger?.LogError(
                $"Failed reading '{ToggleUnitsAction}': {e}"
            );

            return false;
        }
    }

    /*
     * Rewired reads its InputActions during InputManager_Base.Awake().
     *
     * We need to modify userData BEFORE Rewired completes initialization,
     * hence the Prefix rather than Postfix.
     *
     * This follows the same mechanism used by YawOnMouse.
     */
    [HarmonyPatch(typeof(InputManager_Base), "Awake")]
    private static class InputManagerAwakePatch
    {
        [HarmonyPrefix]
        private static void Prefix(InputManager_Base __instance)
        {
            try
            {
                RegisterToggleUnitsAction(__instance);
            }
            catch (Exception e)
            {
                Ready = false;

                _logger?.LogError(
                    $"Failed registering custom Rewired actions: {e}"
                );
            }
        }
    }

    private static void RegisterToggleUnitsAction(
        InputManager_Base inputManager)
    {
        object userData = inputManager.userData;

        if (userData == null)
        {
            _logger?.LogWarning(
                "Rewired UserData was null. " +
                "Toggle Units binding was not registered."
            );

            InputActionCategory test;

            return;
        }

        /*
         * Rewired's actionCategories and actions collections aren't
         * publicly exposed in the version shipped with Nuclear Option,
         * so access them through Harmony reflection.
         */
        IList? categories =
            GetField<IList>(userData, "actionCategories");

        IList? actions =
            GetField<IList>(userData, "actions");

        if (categories == null)
        {
            _logger?.LogWarning(
                "Could not obtain Rewired actionCategories."
            );

            return;
        }

        if (actions == null)
        {
            _logger?.LogWarning(
                "Could not obtain Rewired actions."
            );

            return;
        }

        // -------------------------------------------------------------
        // Find the Nuclear Option category in which our control should
        // appear.
        // -------------------------------------------------------------

        object? targetCategory = null;

        foreach (object category in categories)
        {
            string categoryName =
                GetProperty<string>(category, "name");

            if (string.Equals(
                    categoryName,
                    TargetCategory,
                    StringComparison.OrdinalIgnoreCase))
            {
                targetCategory = category;
                break;
            }
        }

        if (targetCategory == null)
        {
            _logger?.LogWarning(
                $"Rewired category '{TargetCategory}' was not found. " +
                $"'{ToggleUnitsDisplayName}' was not registered."
            );

            return;
        }

        // -------------------------------------------------------------
        // See whether we've already registered the action.
        // Also find an unused action ID.
        // -------------------------------------------------------------

        int newActionId = StartingActionId;

        foreach (object existingAction in actions)
        {
            string actionName =
                GetProperty<string>(existingAction, "name");

            int actionId =
                GetProperty<int>(existingAction, "id");

            /*
             * This can happen if InputManager_Base.Awake() is called
             * more than once.
             */
            if (actionName == ToggleUnitsAction)
            {
                ToggleUnitsActionId = actionId;
                Ready = true;

                _logger?.LogDebug(
                    $"Rewired action '{ToggleUnitsAction}' " +
                    $"already exists with ID {actionId}."
                );

                return;
            }

            /*
             * Ensure our ID doesn't collide with any existing base-game
             * or mod-added action.
             */
            if (actionId >= newActionId)
                newActionId = actionId + 1;
        }

        // -------------------------------------------------------------
        // Create the actual Rewired InputAction.
        // -------------------------------------------------------------

        Type inputActionType = typeof(InputAction);

        /*
         * InputAction doesn't expose the constructor we need publicly,
         * so Activator is used to invoke the non-public constructor.
         */
        var action = (InputAction)Activator.CreateInstance(
            inputActionType,
            nonPublic: true
        )!;

        SetProperty(
            inputActionType,
            action,
            "id",
            newActionId
        );

        SetProperty(
            inputActionType,
            action,
            "name",
            ToggleUnitsAction
        );

        SetProperty(
            inputActionType,
            action,
            "type",
            InputActionType.Button
        );

        SetProperty(
            inputActionType,
            action,
            "descriptiveName",
            ToggleUnitsDisplayName
        );

        int targetCategoryId =
            GetProperty<int>(targetCategory, "id");

        SetProperty(
            inputActionType,
            action,
            "categoryId",
            targetCategoryId
        );

        /*
         * Critical:
         *
         * Without this, Rewired considers the action non-user-assignable
         * and Nuclear Option's control-binding UI will not allow the
         * player to configure it.
         */
        SetField(
            inputActionType,
            action,
            "_userAssignable",
            true
        );

        // Add it to Rewired's global list of actions.
        actions.Add(action);

        // -------------------------------------------------------------
        // Add the action to the category map.
        // -------------------------------------------------------------

        object? actionCategoryMap =
            GetField<object>(
                userData,
                "actionCategoryMap"
            );

        if (actionCategoryMap != null)
        {
            MethodInfo? addActionMethod = AccessTools.Method(
                actionCategoryMap.GetType(),
                "AddAction",
                new[]
                {
                    typeof(int),
                    typeof(int)
                }
            );

            if (addActionMethod == null)
            {
                _logger?.LogWarning(
                    "Could not find ActionCategoryMap.AddAction()."
                );
            }
            else
            {
                addActionMethod.Invoke(
                    actionCategoryMap,
                    new object[]
                    {
                        targetCategoryId,
                        newActionId
                    }
                );
            }
        }
        else
        {
            _logger?.LogWarning(
                "Could not obtain Rewired actionCategoryMap."
            );
        }

        ToggleUnitsActionId = newActionId;
        Ready = true;

        _logger?.LogInfo(
            $"Registered '{ToggleUnitsDisplayName}' " +
            $"as Rewired action {newActionId} " +
            $"in category '{TargetCategory}'."
        );
    }

    // =================================================================
    // Reflection helpers
    // =================================================================

    private static T GetProperty<T>(
        object instance,
        string propertyName)
    {
        PropertyInfo? property = AccessTools.Property(
            instance.GetType(),
            propertyName
        );

        if (property == null)
            return default!;

        object? value = property.GetValue(instance);

        return value is T typed
            ? typed
            : default!;
    }

    private static void SetProperty<T>(
        Type type,
        object instance,
        string propertyName,
        T value)
    {
        var property =
            AccessTools.Property(type, propertyName);

        if (property == null)
        {
            _logger?.LogWarning(
                $"Could not find property " +
                $"'{type.FullName}.{propertyName}'."
            );

            return;
        }

        property.SetValue(
            instance,
            value,
            null
        );
    }

    private static T GetField<T>(
        object instance,
        string fieldName)
    {
        var field = AccessTools.Field(
            instance.GetType(),
            fieldName
        );

        if (field == null)
            return default!;

        object? value = field.GetValue(instance);

        return value is T typed
            ? typed
            : default!;
    }

    private static void SetField<T>(
        Type type,
        object instance,
        string fieldName,
        T value)
    {
        var field =
            AccessTools.Field(type, fieldName);

        if (field == null)
        {
            _logger?.LogWarning(
                $"Could not find field " +
                $"'{type.FullName}.{fieldName}'."
            );

            return;
        }

        field.SetValue(instance, value);
    }
}