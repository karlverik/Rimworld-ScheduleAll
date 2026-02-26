using HarmonyLib;
using RimWorld;
using System.Linq;
using Verse;

namespace ScheduleAllMod
{
    // 禁止在自定义工作时间跑去玩耍
    // 存档保护：防止卸载 Mod 或 Def 丢失导致存档损坏
    [HarmonyPatch(typeof(Pawn_TimetableTracker), "ExposeData")]
    public static class Patch_Timetable_ExposeData_Fixer
    {
        public static void Postfix(Pawn_TimetableTracker __instance)
        {
            if (__instance.times == null) return;

            for (int i = 0; i < __instance.times.Count; i++)
            {
                // 如果日程 Def 不存在了，回退到 Anything
                if (__instance.times[i] == null || !DefDatabase<TimeAssignmentDef>.AllDefs.Contains(__instance.times[i]))
                {
                    __instance.times[i] = TimeAssignmentDefOf.Anything;
                }
            }
        }
    }
}