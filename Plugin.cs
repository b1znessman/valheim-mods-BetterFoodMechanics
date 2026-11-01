using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace BetterFoodMechanics;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(Jotunn.Main.ModGuid)]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
public class BetterFoodMechanicsPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "biznessman.betterfoodmechanics";
    public const string PluginName = "BetterFoodMechanics";
    public const string PluginVersion = "2.0.0";

    internal static ConfigEntry<Toggle> Enabled;
    internal static ConfigEntry<int> Mode;
    internal static ConfigEntry<int> MinComfort;
    internal static ConfigEntry<float> DurationMultiplier;
    internal static ConfigEntry<Toggle> FoodBuffDegrades;

    internal static bool IsEnabled => Enabled.Value == Toggle.On;
    internal static bool IsBuffDegradeEnabled => FoodBuffDegrades.Value == Toggle.On;

    private void Awake()
    {
        var synced = new ConfigurationManagerAttributes { IsAdminOnly = true };

        Enabled = Config.Bind("1 - General", "Enabled", Toggle.On,
            new ConfigDescription("Turn the mod on or off. (Synced with server)", null, synced));

        Mode = Config.Bind("2 - Cozyness", "DegradationMode", 1,
            new ConfigDescription(
                "How food behaves while you rest in your base:\n" +
                "0 = Frozen: food timer and buffs stop completely.\n" +
                "1 = Slow: food lasts twice as long.\n" +
                "2 = Scaled: more comfort = slower decay (1/2 rate at 10 comfort, 1/3 at 15, 1/4 at 20+). Ignores MinimumComfort.",
                new AcceptableValueRange<int>(0, 2), synced));

        MinComfort = Config.Bind("2 - Cozyness", "MinimumComfort", 10,
            new ConfigDescription("Comfort level needed for modes 0 and 1 to apply. Not used in mode 2.",
                new AcceptableValueRange<int>(5, 25), synced));

        DurationMultiplier = Config.Bind("3 - Food", "FoodDurationMultiplier", 1f,
            new ConfigDescription("How much longer food lasts. 2 = twice as long (a 30 min food lasts 60).",
                new AcceptableValueRange<float>(1f, 5f), synced));

        FoodBuffDegrades = Config.Bind("3 - Food", "FoodBuffDegrades", Toggle.On,
            new ConfigDescription(
                "On: food buffs (health/stamina/eitr) fade gradually over the full duration.\n" +
                "Off: buffs stay at full strength for the entire duration.",
                null, synced));

        Enabled.SettingChanged += (s, e) => FoodDurationApplier.Apply(ObjectDB.instance);
        DurationMultiplier.SettingChanged += (s, e) => FoodDurationApplier.Apply(ObjectDB.instance);

        new Harmony(PluginGuid).PatchAll();
    }

    /// <summary>Fraction of normal food decay that still happens (0 = frozen, 1 = vanilla).</summary>
    internal static float GetDecayRate(int comfort)
    {
        switch (Mode.Value)
        {
            case 0: return comfort >= MinComfort.Value ? 0f : 1f;
            case 1: return comfort >= MinComfort.Value ? 0.5f : 1f;
            case 2:
                if (comfort >= 20) return 1f / 4f;
                if (comfort >= 15) return 1f / 3f;
                if (comfort >= 10) return 1f / 2f;
                return 1f;
            default: return 1f;
        }
    }
}

public enum Toggle { Off = 0, On = 1 }

// Cozy rate: slows the food tick itself, so regen and everything else stay vanilla.
[HarmonyPatch(typeof(Player), "UpdateFood")]
internal static class UpdateFoodRatePatch
{
    private static readonly AccessTools.FieldRef<Player, float> TimerRef =
        AccessTools.FieldRefAccess<Player, float>("m_foodUpdateTimer");
    private static readonly FieldInfo FoodRateField = AccessTools.Field(typeof(Game), "m_foodRate");

    // Comfort is rechecked twice a second instead of every physics frame.
    private static float _cachedRate = 1f;
    private static float _nextCheck;

    internal static float GetRate(Player player)
    {
        if (!BetterFoodMechanicsPlugin.IsEnabled) return 1f;
        if (player != Player.m_localPlayer) return 1f; // food ticks run on the owning client

        if (UnityEngine.Time.time >= _nextCheck)
        {
            _nextCheck = UnityEngine.Time.time + 0.5f;
            _cachedRate = BetterFoodMechanicsPlugin.GetDecayRate(player.GetComfortLevel());
        }
        return _cachedRate;
    }

    private static void Prefix(Player __instance, float dt)
    {
        if (dt <= 0f) return; // forced updates (eating) pass dt = 0
        float rate = GetRate(__instance);
        if (rate >= 1f) return;

        float foodRate = (float)FoodRateField.GetValue(null);
        // Vanilla adds dt * foodRate afterwards, so net progress is dt * foodRate * rate.
        TimerRef(__instance) -= dt * foodRate * (1f - rate);
    }

    // HUD food timer: show time / (foodRate * rate), so the countdown ticks in real seconds while slowed.
    [HarmonyPatch(typeof(Hud), "UpdateFood")]
    internal static class HudFoodTimePatch
    {
        private static readonly FieldInfo FoodRateField = AccessTools.Field(typeof(Game), "m_foodRate");
        private static readonly MethodInfo DisplayRateMethod =
            AccessTools.Method(typeof(HudFoodTimePatch), nameof(DisplayRate));

        public static float DisplayRate()
        {
            var player = Player.m_localPlayer;
            if (player == null) return 1f;
            float rate = UpdateFoodRatePatch.GetRate(player);
            return rate > 0f ? rate : 1f; // frozen (mode 0): show the real held time, not infinity
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
        {
            foreach (var ins in code)
            {
                yield return ins;
                if (ins.LoadsField(FoodRateField))
                {
                    yield return new CodeInstruction(OpCodes.Call, DisplayRateMethod);
                    yield return new CodeInstruction(OpCodes.Mul);
                }
            }
        }
    }
}

internal static class FoodDurationApplier
{
    private static readonly Dictionary<ItemDrop.ItemData.SharedData, float> Originals =
        new Dictionary<ItemDrop.ItemData.SharedData, float>();

    public static void Apply(ObjectDB db)
    {
        if (db == null) return;
        float mult = BetterFoodMechanicsPlugin.IsEnabled
            ? BetterFoodMechanicsPlugin.DurationMultiplier.Value
            : 1f;

        foreach (var go in db.m_items)
        {
            if (go == null) continue;
            var drop = go.GetComponent<ItemDrop>();
            if (drop == null) continue;

            var shared = drop.m_itemData.m_shared;
            if (!Originals.TryGetValue(shared, out float original))
            {
                if (shared.m_foodBurnTime <= 0f) continue; // not a food
                original = shared.m_foodBurnTime;
                Originals[shared] = original;
            }
            shared.m_foodBurnTime = original * mult;
        }
    }
}

[HarmonyPatch(typeof(ObjectDB), "Awake")]
internal static class ObjectDBAwakePatch
{
    private static void Postfix(ObjectDB __instance) => FoodDurationApplier.Apply(__instance);
}

[HarmonyPatch(typeof(ObjectDB), "CopyOtherDB")]
internal static class ObjectDBCopyPatch
{
    private static void Postfix(ObjectDB __instance) => FoodDurationApplier.Apply(__instance);
}

// FoodDegrade = Off: keep buffs at full strength until the food runs out.
[HarmonyPatch(typeof(Player), "GetTotalFoodValue")]
internal static class NoDegradePatch
{
    private static readonly AccessTools.FieldRef<Player, List<Player.Food>> FoodsRef =
        AccessTools.FieldRefAccess<Player, List<Player.Food>>("m_foods");

    private static void Postfix(Player __instance, ref float hp, ref float stamina, ref float eitr)
    {
        if (!BetterFoodMechanicsPlugin.IsEnabled || BetterFoodMechanicsPlugin.IsBuffDegradeEnabled) return;

        foreach (var food in FoodsRef(__instance))
        {
            var s = food.m_item.m_shared;
            hp += s.m_food - food.m_health;
            stamina += s.m_foodStamina - food.m_stamina;
            eitr += s.m_foodEitr - food.m_eitr;
        }
    }
}