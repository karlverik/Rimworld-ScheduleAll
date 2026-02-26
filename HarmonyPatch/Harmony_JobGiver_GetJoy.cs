using HarmonyLib;
using RimWorld;
using Verse;

namespace ScheduleAllMod
{
    [HarmonyPatch(typeof(ThinkNode_Priority_GetJoy), "GetPriority")]
    public static class Patch_DisableJoyInWorkSlot
    {
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
                        // 判定 Label 匹配，确保逻辑锁定在当前日程格
                        if (currentAssignment.label == config.label)
                        {
                            __result = 0f;
                            return false; // 拦截原版
                        }
                    }
                }
            }
            return true;
        }
    }
}