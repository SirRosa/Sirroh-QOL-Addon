using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Sirroh.Qol
{
    /// <summary>
    /// Allow any equipment in water while preserving real swimming everywhere else.
    /// Replace the EXACT IsSwimming call sites used to stow, equip or restore items.
    /// No global patches of Character.IsSwimming and no configuration.
    /// </summary>
    internal static class SwimmingEquipment
    {
        private static readonly MethodInfo Swimming = AccessTools.Method(
            typeof(Character), "IsSwimming", Type.EmptyTypes);
        private static readonly MethodInfo EquipCheck = AccessTools.Method(
            typeof(SwimmingEquipment), nameof(IsSwimmingForEquipment));

        internal static void Install(Plugin host)
        {
            // Each target has exactly one Character.IsSwimming call in Valheim 1.0:
            // - UpdateEquipment hides weapon + shield automatically in water.
            // - EquipItem refuses any new item while swimming.
            // - Player.Update refuses to show previously hidden items while swimming.
            Patch(host, "Humanoid.UpdateEquipment", typeof(Humanoid), "UpdateEquipment");
            Patch(host, "Humanoid.EquipItem", typeof(Humanoid), "EquipItem");
            Patch(host, "Player.Update", typeof(Player), "Update");
        }

        private static void Patch(Plugin host, string label, Type owner, string method)
        {
            MethodInfo target = AccessTools.DeclaredMethod(owner, method);
            host.Patch("SwimmingEquipment." + label, target,
                transpiler: AccessTools.Method(typeof(SwimmingEquipment), nameof(Transpile)));
        }

        private static IEnumerable<CodeInstruction> Transpile(
            IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var code = new List<CodeInstruction>(instructions);
            int matches = 0;
            for (int i = 0; i < code.Count; i++)
            {
                if (!code[i].Calls(Swimming)) continue;
                matches++;
            }
            if (matches != 1)
                throw new InvalidOperationException("SwimmingEquipment: expected exactly one IsSwimming call in " +
                    original.DeclaringType?.Name + "." + original.Name + ", found " + matches);

            for (int i = 0; i < code.Count; i++)
            {
                if (!code[i].Calls(Swimming)) continue;
                // IL already has ldarg.0 (this) immediately before the call.
                // The original has signature bool Character.IsSwimming().
                // Replacement takes the same one instance on the evaluation stack.
                code[i].operand = EquipCheck;
                break;
            }
            Plugin.Instance?.Log.LogInfo("SwimmingEquipment: replaced one swimming equipment guard in " +
                original.DeclaringType?.Name + "." + original.Name);
            return code;
        }

        public static bool IsSwimmingForEquipment(Character character)
        {
            // Players may wield ALL equipment in water. NPCs and other characters
            // retain their original behavior. Do not alter physics/stamina/animation.
            return character == null || !character.IsPlayer() && character.IsSwimming();
        }
    }
}
