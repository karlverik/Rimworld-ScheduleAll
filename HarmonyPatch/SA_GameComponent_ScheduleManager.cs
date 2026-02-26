using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using System.Linq;

namespace ScheduleAllMod
{
    public class GameComponent_ScheduleManager : GameComponent
    {
        // 存储原始优先级备份：(小人, 工作类型) -> 原始优先级
        internal Dictionary<Pair<Pawn, WorkTypeDef>, int> originalPriorities = new Dictionary<Pair<Pawn, WorkTypeDef>, int>();

        private int lastHour = -1;

        private List<Pawn> workingPawns;
        private List<WorkTypeDef> workingWorks;
        private List<int> workingPriorities;

        public GameComponent_ScheduleManager(Game game) : base() { }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 250 != 0) return;

            Map map = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
            if (map == null) return;

            int currentHour = GenLocalDate.HourOfDay(map);

            if (currentHour != lastHour)
            {
                if (lastHour != -1)
                {
                    Log.Message($"[ScheduleAllMod] Hour changed to {currentHour}, refreshing all pawn priorities.");
                    RefreshAllPawnPriorities();
                }
                lastHour = currentHour;
            }
        }

        private void RefreshAllPawnPriorities()
        {
            var pawns = PawnsFinder.AllMaps_FreeColonistsSpawned.ToList();
            var settings = ScheduleAllMod.Settings;
            if (settings == null) return;

            Patch_Pawn_WorkSettings_SetPriority.IsInternalOperation = true;

            try
            {
                foreach (var pawn in pawns)
                {
                    if (pawn.workSettings == null || !pawn.workSettings.EverWork || pawn.timetable == null) continue;

                    // --- 第一阶段：还原 (Reset) ---
                    RestoreAllPrioritiesForPawn(pawn);

                    // --- 第二阶段：判定与应用 (Apply) ---
                    TimeAssignmentDef currentDef = pawn.timetable.CurrentAssignment;
                    if (currentDef != null && currentDef.defName.StartsWith(ScheduleAllModInit.Prefix))
                    {
                        if (int.TryParse(currentDef.defName.Replace(ScheduleAllModInit.Prefix, ""), out int index))
                        {
                            if (index >= 0 && index < settings.slotConfigs.Count)
                            {
                                var config = settings.slotConfigs[index];
                                if (config.label == currentDef.label && config.TargetWork != null)
                                {
                                    ApplyPriority(pawn, config.TargetWork);
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                Patch_Pawn_WorkSettings_SetPriority.IsInternalOperation = false;
            }

            CleanupOrphanedRecords();
        }

        private void RestoreAllPrioritiesForPawn(Pawn pawn)
        {
            var keys = originalPriorities.Keys.Where(k => k.First == pawn).ToList();
            foreach (var key in keys)
            {
                if (originalPriorities.TryGetValue(key, out int oldPriority))
                {
                    // 添加日志：记录还原操作
                   // Log.Message($"[ScheduleAllMod] RESTORE: {pawn.NameShortColored} | Work: {key.Second.defName} | Resetting to Original Priority: {oldPriority}");

                    pawn.workSettings.SetPriority(key.Second, oldPriority);
                    originalPriorities.Remove(key);
                }
            }
        }

        private void ApplyPriority(Pawn pawn, WorkTypeDef work)
        {
            if (pawn.WorkTypeIsDisabled(work)) return;

            var key = new Pair<Pawn, WorkTypeDef>(pawn, work);

            if (!originalPriorities.ContainsKey(key))
            {
                int currentPriority = pawn.workSettings.GetPriority(work);
                originalPriorities[key] = currentPriority;

                // 添加日志：记录应用（劫持）操作
                //Log.Message($"[ScheduleAllMod] APPLY: {pawn.NameShortColored} | Work: {work.defName} | Original: {currentPriority} -> New: 1");
            }

            pawn.workSettings.SetPriority(work, 1);
        }

        private void CleanupOrphanedRecords()
        {
            // 建议：只清理绝对无法再访问的对象 (null 或 已销毁)
            // 暂时保留“非玩家控制”的记录，除非你确定永远不需要它们了
            var keysToRemove = originalPriorities.Keys.Where(k =>
                k.First == null ||
                k.First.Destroyed
            ).ToList();

            if (keysToRemove.Count > 0)
            {
                // Log.Message($"[ScheduleAllMod] Cleaning up {keysToRemove.Count} dead/null records.");
                foreach (var key in keysToRemove)
                {
                    originalPriorities.Remove(key);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastHour, "SA_lastHour", -1);

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // 存档时，填充中转列表
                workingPawns = originalPriorities.Keys.Select(k => k.First).ToList();
                workingWorks = originalPriorities.Keys.Select(k => k.Second).ToList();
                workingPriorities = originalPriorities.Values.ToList();
            }

            // --- 修改点 2：直接使用成员变量进行 Look 操作 ---
            Scribe_Collections.Look(ref workingPawns, "SA_Pawns", LookMode.Reference);
            Scribe_Collections.Look(ref workingWorks, "SA_Works", LookMode.Def);
            Scribe_Collections.Look(ref workingPriorities, "SA_Values", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                originalPriorities.Clear();
                if (workingPawns != null && workingWorks != null && workingPriorities != null)
                {
                    for (int i = 0; i < workingPawns.Count; i++)
                    {
                        if (workingPawns[i] != null && workingWorks[i] != null)
                        {
                            var key = new Pair<Pawn, WorkTypeDef>(workingPawns[i], workingWorks[i]);
                            originalPriorities[key] = workingPriorities[i];
                        }
                    }
                }

                // --- 新增：读档数据报告 ---
                Log.Message($"[ScheduleAllMod] === Load Game Data Report ===");
                if (originalPriorities.Count == 0)
                {
                    Log.Message("[ScheduleAllMod] No priority backups found in this save file.");
                }
                else
                {
                    foreach (var kvp in originalPriorities)
                    {
                        string pawnName = kvp.Key.First?.NameShortColored.ToString() ?? "Unknown Pawn";
                        string workName = kvp.Key.Second?.defName ?? "Unknown Work";
                        Log.Message($"[ScheduleAllMod] - Cached: {pawnName} | {workName} | Original Priority: {kvp.Value}");
                    }
                }
                Log.Message($"[ScheduleAllMod] ==============================");

                // 同步当前小时，确保不会在读档瞬间执行逻辑，而是等待下一个整点
                Map map = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
                if (map != null)
                {
                    this.lastHour = GenLocalDate.HourOfDay(map);
                }

                // --- 修改点 3：数据恢复完成后，清空列表释放内存，防止长时间占用 ---
                workingPawns = null;
                workingWorks = null;
                workingPriorities = null;
            }
        }
    }
}