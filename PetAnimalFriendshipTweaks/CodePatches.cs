using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Menus;
using StardewValley.Tools;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace PetAnimalFriendshipTweaks
{
    public partial class ModEntry
    {
        public static int CustomMaxFriendshipPoints()
        {
            if (!Config.ModEnabled)
                return 1000;
            return Config.MaxFriendshipPoints;
        }

        private static readonly MethodInfo CapMethod =
            SymbolExtensions.GetMethodInfo(() => ModEntry.CustomMaxFriendshipPoints());

        // Shared transpiler: swap every literal 1000 for a call to CustomMaxFriendshipPoints().
        // Only used on methods that are NOT part of a tool's begin/finish animation state machine,
        // to avoid interfering with UsingTool/canReleaseTool (see MilkPail/Shears below for why).
        private static IEnumerable<CodeInstruction> ReplaceCapConstant(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.LoadsConstant(1000))
                {
                    yield return new CodeInstruction(OpCodes.Call, CapMethod);
                    continue;
                }
                yield return instruction;
            }
        }

        [HarmonyPatch(typeof(FarmAnimal), nameof(FarmAnimal.pet))]
        public class FarmAnimal_pet_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
                => ReplaceCapConstant(instructions);
        }

        [HarmonyPatch(typeof(FarmAnimal), nameof(FarmAnimal.Eat))]
        public class FarmAnimal_Eat_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
                => ReplaceCapConstant(instructions);
        }

        // MilkPail.DoFunction is part of the tool's begin->animate->finish state machine
        // (UsingTool / canReleaseTool). Transpiling inside it risked leaving those stuck true,
        // locking the hotbar. Instead: read the animal's friendship before/after vanilla runs,
        // and top it up afterward if our cap is higher. Never touches the tool's own IL.
        [HarmonyPatch(typeof(MilkPail), nameof(MilkPail.DoFunction))]
        public class MilkPail_DoFunction_Patch
        {
            private static int preValue = -1;

            public static void Prefix(MilkPail __instance)
            {
                preValue = __instance.animal?.friendshipTowardFarmer.Value ?? -1;
            }

            public static void Postfix(MilkPail __instance)
            {
                if (__instance.animal == null || preValue < 0)
                    return;

                int vanillaResult = __instance.animal.friendshipTowardFarmer.Value;
                int wanted = Math.Min(CustomMaxFriendshipPoints(), preValue + 5); // vanilla milking gain is +5
                if (wanted > vanillaResult)
                    __instance.animal.friendshipTowardFarmer.Value = wanted;
            }
        }

        // Same reasoning as MilkPail above.
        [HarmonyPatch(typeof(Shears), nameof(Shears.DoFunction))]
        public class Shears_DoFunction_Patch
        {
            private static int preValue = -1;

            public static void Prefix(Shears __instance)
            {
                preValue = __instance.animal?.friendshipTowardFarmer.Value ?? -1;
            }

            public static void Postfix(Shears __instance)
            {
                if (__instance.animal == null || preValue < 0)
                    return;

                int vanillaResult = __instance.animal.friendshipTowardFarmer.Value;
                int wanted = Math.Min(CustomMaxFriendshipPoints(), preValue + 5); // vanilla shearing gain is +5
                if (wanted > vanillaResult)
                    __instance.animal.friendshipTowardFarmer.Value = wanted;
            }
        }

        [HarmonyPatch(typeof(Pet), nameof(Pet.dayUpdate))]
        public class Pet_dayUpdate_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
                => ReplaceCapConstant(instructions);
        }

        [HarmonyPatch(typeof(Pet), nameof(Pet.checkAction))]
        public class Pet_checkAction_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
                => ReplaceCapConstant(instructions);
        }

        [HarmonyPatch(typeof(Pet), nameof(Pet.GrantLoveMailIfNecessary))]
        public class Pet_GrantLoveMailIfNecessary_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
                => ReplaceCapConstant(instructions);
        }

        // drawNPCSlot is private, so target it by string name.
        [HarmonyPatch(typeof(AnimalPage), "drawNPCSlot")]
        public class AnimalPage_drawNPCSlot_Patch
        {
            public static bool Prefix(AnimalPage __instance, SpriteBatch b, int i)
            {
                if (!Config.ModEnabled)
                    return true;

                var socialEntry = __instance.GetSocialEntry(i);
                if (socialEntry == null || i < 0)
                    return false;

                __instance.sprites[i].draw(b);

                float y = Game1.smallFont.MeasureString("W").Y;
                float num = ((LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.ru
                    || LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.ko) ? ((0f - y) / 2f) : 0f);
                int num2 = ((socialEntry.TextureSourceRect.Height <= 16) ? (-40) : 8);

                b.DrawString(Game1.dialogueFont, socialEntry.DisplayName,
                    new Vector2(__instance.xPositionOnScreen + IClickableMenu.borderWidth * 3 / 2 + 192 - 20 + 96
                        - (int)(Game1.dialogueFont.MeasureString(socialEntry.DisplayName).X / 2f),
                        (float)(__instance.sprites[i].bounds.Y + 48 + num2) + num - 20f),
                    Game1.textColor);

                if (socialEntry.FriendshipLevel != -1)
                {
                    const int pointsPerHeart = 200;
                    int heartCount = Math.Max(5, CustomMaxFriendshipPoints() / pointsPerHeart);
                    int rowsNeeded = (heartCount + 4) / 5;          // number of rows required
                    int baseYOffset = 64 - (rowsNeeded - 1) * 16;   // 64 (1 row), 48 (2 rows), 32 (3 rows)

                    double currentPoints = socialEntry.FriendshipLevel;
                    int partialHeartIndex = (int)((currentPoints % pointsPerHeart >= pointsPerHeart / 2)
                        ? (currentPoints / pointsPerHeart) : -100.0);
                    int num5 = (socialEntry.ReceivedAnimalCracker ? (-24) : 0);

                    for (int j = 0; j < heartCount; j++)
                    {
                        //rounds up the rows every 5 hearts so that they don't run off-screen
                        int row = j / 5;
                        int col = j % 5;
                        Vector2 pos = new Vector2(
                            __instance.xPositionOnScreen + 512 - 4 + col * 32,
                            __instance.sprites[i].bounds.Y + num5 + num2 + baseYOffset - 24 + row * 34);

                        bool filled = currentPoints <= (double)((j + 1) * (pointsPerHeart - 5));
                        b.Draw(Game1.mouseCursors, pos,
                            new Rectangle(211 + (filled ? 7 : 0), 428, 7, 6),
                            Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.89f);

                        if (partialHeartIndex == j)
                        {
                            b.Draw(Game1.mouseCursors, pos,
                                new Rectangle(211, 428, 4, 6),
                                Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.891f);
                        }
                    }
                }

                if (socialEntry.WasPetYet != -1)
                {
                    b.Draw(Game1.mouseCursors, new Vector2(__instance.xPositionOnScreen + 704 - 4, __instance.sprites[i].bounds.Y + num2 + 64 - 52), new Rectangle(32, 0, 10, 10), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.8f);
                    b.Draw(Game1.mouseCursors_1_6, new Vector2(__instance.xPositionOnScreen + 704 - 4, __instance.sprites[i].bounds.Y + num2 + 64 - 8), new Rectangle(273 + socialEntry.WasPetYet * 9, 253, 9, 9), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.8f);
                }
                if (socialEntry.special == 1)
                {
                    Utility.drawWithShadow(b, Game1.objectSpriteSheet_2, new Vector2(__instance.xPositionOnScreen + 704 - 16, __instance.sprites[i].bounds.Y + num2 + 64 - 52), new Rectangle(0, 160, 16, 16), Color.White, 0f, Vector2.Zero, 4f, flipped: false, 0.8f, 0, 8);
                }
                if (socialEntry.ReceivedAnimalCracker)
                {
                    Utility.drawWithShadow(b, Game1.objectSpriteSheet_2, new Vector2(__instance.xPositionOnScreen + 576 - 20, __instance.sprites[i].bounds.Y + num2 + 64 - 16), new Rectangle(16, 242, 15, 11), Color.White, 0f, Vector2.Zero, 4f, flipped: false, 0.8f);
                }

                return false;
            }
        }
    }
}