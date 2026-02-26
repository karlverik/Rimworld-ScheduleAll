using System.Collections.Generic;
using RimWorld;
using Verse;
using System.Linq;

namespace ScheduleAllMod
{
    public static class PrioritySnapshotUtils
    {
        /// <summary>
        /// 捕获当前所有小人的优先级并存入设置
        /// </summary>
        public static void SaveAllPrioritiesToGlobal(ModSettingsData settings)
        {
            if (settings == null) return;

            // 1. 清空旧数据
            settings.globalSnapshots.Clear();

            // 2. 获取当前地图上所有属于玩家的小人
            var pawns = PawnsFinder.AllMaps_FreeColonistsSpawned;

            foreach (var pawn in pawns)
            {
                if (pawn.workSettings == null || !pawn.workSettings.EverWork) continue;

                var snap = new PawnPrioritySnapshot
                {
                    pawnName = pawn.Name.ToStringFull
                };

                // 3. 遍历所有工作类型并记录
                foreach (var work in DefDatabase<WorkTypeDef>.AllDefs)
                {
                    snap.workDefNames.Add(work.defName);
                    snap.priorities.Add(pawn.workSettings.GetPriority(work));
                }

                settings.globalSnapshots.Add(snap);
            }

            // 4. 持久化到 XML (Mod_xxx.xml)
            settings.Write();

            Messages.Message("ScheduleAll_Log_SavedGlobal".Translate(settings.globalSnapshots.Count),
                MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>
        /// 从设置中读取快照并应用到当前小人
        /// </summary>
        public static void LoadAllPrioritiesFromGlobal(ModSettingsData settings)
        {
            if (settings == null || settings.globalSnapshots == null || settings.globalSnapshots.Count == 0)
            {
                Messages.Message("ScheduleAll_Log_NoSnapshot".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            var currentPawns = PawnsFinder.AllMaps_FreeColonistsSpawned;
            int matchCount = 0;

            // 1. 开启内部操作标记，防止被自身的 GameComponent 或其他 Hook 拦截
            Patch_Pawn_WorkSettings_SetPriority.IsInternalOperation = true;

            try
            {
                foreach (var pawn in currentPawns)
                {
                    // 2. 按名字匹配小人
                    var snap = settings.globalSnapshots.FirstOrDefault(s => s.pawnName == pawn.Name.ToStringFull);
                    if (snap == null) continue;

                    for (int i = 0; i < snap.workDefNames.Count; i++)
                    {
                        var work = DefDatabase<WorkTypeDef>.GetNamedSilentFail(snap.workDefNames[i]);
                        if (work != null)
                        {
                            pawn.workSettings.SetPriority(work, snap.priorities[i]);
                        }
                    }
                    matchCount++;
                }
            }
            finally
            {
                // 3. 务必在 finally 中关闭标记，确保逻辑安全
                Patch_Pawn_WorkSettings_SetPriority.IsInternalOperation = false;
            }

            Messages.Message("ScheduleAll_Log_LoadedGlobal".Translate(matchCount),
                MessageTypeDefOf.PositiveEvent);
        }
    }
}