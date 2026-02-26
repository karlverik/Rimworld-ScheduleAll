using HarmonyLib;
using RimWorld;
using Verse;

namespace ScheduleAllMod
{
    // 使用 HarmonyOptional 配合目标判定，防止无 DLC 时报错
    [HarmonyPatch(typeof(JobGiver_Meditate), "GetPriority")]
    public static class Patch_DisableMeditateInWorkSlot
    {
        // 关键：如果 DLC 没开，Harmony 会自动跳过这个补丁
        public static bool Prepare() => ModsConfig.RoyaltyActive;

        public static bool Prefix(Pawn pawn, ref float __result)
        {
            var currentAssignment = pawn?.timetable?.CurrentAssignment;
            if (currentAssignment == null) return true;

            if (currentAssignment.defName.StartsWith(ScheduleAllModInit.Prefix))
            {
                var settings = ScheduleAllMod.Settings;
                if (int.TryParse(currentAssignment.defName.Replace(ScheduleAllModInit.Prefix, ""), out int index))
                {
                    if (index >= 0 && index < settings.slotConfigs.Count)
                    {
                        var config = settings.slotConfigs[index];
                        if (currentAssignment.label == config.label)
                        {
                            __result = 0f;
                            return false;
                        }
                    }
                }
            }
            return true;
        }
    }
}