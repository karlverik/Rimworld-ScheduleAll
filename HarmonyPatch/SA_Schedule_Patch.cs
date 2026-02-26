using HarmonyLib;
using RimWorld;
using Verse;

namespace ScheduleAllMod
{
    [HarmonyPatch(typeof(JobGiver_Work), "GetPriority")]
    public static class Patch_JobGiver_Work_GetPriority
    {
        // 拦截前置：返回 false 表示拦截原版逻辑，返回 true 表示放行
        public static bool Prefix(Pawn pawn, ref float __result)
        {
            // 1. 基础安全检查
            if (pawn == null || pawn.timetable?.CurrentAssignment == null) return true;

            string currentDefName = pawn.timetable.CurrentAssignment.defName;

            // 2. 检查当前日程是否属于我们的 8 个自定义插槽
            if (currentDefName.StartsWith(ScheduleAllModInit.Prefix))
            {
                // 获取当前插槽对应的配置（从你的 Settings 中读取）
                var settings = ScheduleAllMod.Settings;
                if (settings == null) return true;

                // 提取索引 (SA_Slot_0 -> 0)
                if (int.TryParse(currentDefName.Substring(ScheduleAllModInit.Prefix.Length), out int index))
                {
                    if (index >= 0 && index < settings.slotConfigs.Count)
                    {
                        var config = settings.slotConfigs[index];
                        WorkTypeDef targetWork = config.TargetWork;

                        // 3. 核心 AI 引导逻辑
                        // 如果该插槽设置了对应的工作，且小人本身具备该项能力（优先级 > 0）
                        if (targetWork != null && pawn.workSettings != null && pawn.workSettings.GetPriority(targetWork) > 0)
                        {
                            // 强行给该“工作分配者”一个高评分（9f 是原版工作时段的标准优先级）
                            // 这样 AI 就会认为“我现在非常该干活”
                            __result = 9f;
                            return false; // 拦截原版逻辑，避免它因为不认识 Def 而抛出异常
                        }
                    }
                }

                // 如果该插槽没设工作，或者小人不会这项工作
                // 则返回 0 优先级，表示小人在此刻不想找工作活干（会去找其他事，如任意时间逻辑）
                __result = 0f;
                return false;
            }

            return true; // 其他原版日程（工作、休息等）放行
        }
    }
}