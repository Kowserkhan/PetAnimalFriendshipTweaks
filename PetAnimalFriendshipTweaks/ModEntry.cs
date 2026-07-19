using HarmonyLib;
using StardewModdingAPI;

namespace PetAnimalFriendshipTweaks
{
    public partial class ModEntry : Mod
    {
        public static IMonitor SMonitor = null!;
        public static IModHelper SHelper = null!;
        public static ModConfig Config = null!;

        public override void Entry(IModHelper helper)
        {
            Config = Helper.ReadConfig<ModConfig>();
            SMonitor = Monitor;
            SHelper = helper;

            helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;

            var harmony = new Harmony(ModManifest.UniqueID);
            harmony.PatchAll();
        }

        private void GameLoop_GameLaunched(object? sender, StardewModdingAPI.Events.GameLaunchedEventArgs e)
        {
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            configMenu.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => Helper.WriteConfig(Config)
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Mod Enabled",
                getValue: () => Config.ModEnabled,
                setValue: value => Config.ModEnabled = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Max Friendship Points",
                tooltip: () => "Vanilla max is 1000 (5 hearts). Each heart = 200 points, so 2000 = 10 hearts.",
                getValue: () => Config.MaxFriendshipPoints,
                setValue: value => Config.MaxFriendshipPoints = value,
                min: 1000,
                max: 3000,
                interval: 200
            );
        }
    }
}