using HarmonyLib;
using RimWorld;
using Verse;

namespace ScheduleAllMod
{
    /// <summary>
    /// 核心逻辑：拦截玩家对优先级的物理修改。
    /// 当小人处于模组的“专属日程”时，SetPriority 的原始值会被劫持，
    /// 如果此时玩家手动改了数值，我们需要同步更新备份字典。
    /// </summary>
    [HarmonyPatch(typeof(Pawn_WorkSettings), "SetPriority")]
    public static class Patch_Pawn_WorkSettings_SetPriority
    {
        // 重入锁：防止 Manager 在自动修改/还原优先级时触发此补丁，形成死循环或逻辑混乱
        public static bool IsInternalOperation = false;

        [HarmonyPrefix]
        public static void Prefix(Pawn ___pawn, WorkTypeDef w, int priority)
        {
            // 1. 如果是模组自己的 Manager 在执行修改，直接放行，不需要更新备份
            if (IsInternalOperation) return;

            // 2. 检查小人是否有效
            if (___pawn == null || !___pawn.IsColonist) return;

            // 3. 获取管理组件
            var manager = Current.Game?.GetComponent<GameComponent_ScheduleManager>();
            if (manager == null) return;

            // 4. 构建唯一键 (小人 + 工作类型)
            var key = new Pair<Pawn, WorkTypeDef>(___pawn, w);

            // 5. 关键判断：
            // 如果 originalPriorities 字典里包含这个 Key，
            // 说明该工作目前正被模组“劫持”（即强行设为了 1）。
            // 此时玩家在界面上手动点击修改（例如把 3 改成了 0），
            // 我们必须把“打算还原回去的备份值”也更新为 0。
            if (manager.originalPriorities.ContainsKey(key))
            {
                // 更新备份账本
                manager.originalPriorities[key] = priority;

                // 调试输出（开发完成后可删除）
                // Log.Message($"[ScheduleAll] 检测到玩家手动修改：已更新 {___pawn.LabelShort} 的 {w.label} 备份值为 {priority}");
            }
        }
    }
}