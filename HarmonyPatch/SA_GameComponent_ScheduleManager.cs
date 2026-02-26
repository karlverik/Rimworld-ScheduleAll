using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using System.Linq;

namespace ScheduleAllMod
{
    public class GameComponent_ScheduleManager : GameComponent
    {
        internal Dictionary<Pair<Pawn, WorkTypeDef>, int> originalPriorities = new Dictionary<Pair<Pawn, WorkTypeDef>, int>();

        // --- 新增：记录每个小人上一次逻辑处理时的日程定义 ---
        private int lastHour = -1;
        private int nextCheckTick = -1;
        private const int CheckInterval = 250;

        // 存档中转变量
        private List<Pawn> workingPawns;
        private List<WorkTypeDef> workingWorks;
        private List<int> workingPriorities;

        public GameComponent_ScheduleManager(Game game) : base() { }

        public override void GameComponentTick()
        {
            int currentTick = Find.TickManager.TicksGame;
            if (nextCheckTick == -1) nextCheckTick = currentTick + CheckInterval;

            if (currentTick >= nextCheckTick)
            {
                nextCheckTick = currentTick + CheckInterval;
                ExecuteHourlyLogic();
            }
        }

        private void ExecuteHourlyLogic()
        {
            Map map = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
            if (map == null) return;

            int currentHour = GenLocalDate.HourOfDay(map);

            if (currentHour != lastHour)
            {
                // 注意：这里不再判断 lastHour != -1，因为我们需要在读档后第一时间同步状态
                RefreshAllPawnPriorities();
                lastHour = currentHour;
            }
        }

        private void RefreshAllPawnPriorities()
        {
            Map map = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
            if (map == null) return;

            // 1. 获取当前小时 (0-23)
            int currentHour = GenLocalDate.HourOfDay(map);

            // 2. 计算前一小时 (考虑 0 点跨越到 23 点的情况)
            int lastHourIndex = (currentHour - 1 + 24) % 24;

            var pawns = PawnsFinder.AllMaps_FreeColonistsSpawned.ToList();
            var settings = ScheduleAllMod.Settings;
            if (settings == null) return;

            Patch_Pawn_WorkSettings_SetPriority.IsInternalOperation = true;

            try
            {
                foreach (var pawn in pawns)
                {
                    if (pawn.workSettings == null || !pawn.workSettings.EverWork || pawn.timetable == null) continue;
                    
                    TimeAssignmentDef currentDef = pawn.timetable.times[currentHour];
                    TimeAssignmentDef lastDef = pawn.timetable.times[lastHourIndex];

                    // 1. 基本判定：是否为空，以及是否属于本 Mod 的自定义日程
                    bool isCurrentCustom = currentDef != null && currentDef.defName.StartsWith(ScheduleAllModInit.Prefix);
                    bool isLastCustom = lastDef != null && lastDef.defName.StartsWith(ScheduleAllModInit.Prefix);
                    // 如果日程没有变化，直接跳过，不做任何修改，保护原始数据
                    // --- 1. 日程没有变化的情况 (自愈逻辑) ---
                    if (currentDef?.defName == lastDef?.defName)
                    {
                        if (isCurrentCustom)
                        {
                            // 虽然日程没变，但我们要解析出当前应该对应的 WorkType
                            if (int.TryParse(currentDef.defName.Replace(ScheduleAllModInit.Prefix, ""), out int index))
                            {
                                var config = settings.slotConfigs.ElementAtOrDefault(index);
                                if (config != null && config.TargetWork != null)
                                {
                                    // 核心检查：如果优先级被外界改了（不是 1），则重新应用劫持
                                    if (pawn.workSettings.GetPriority(config.TargetWork) != 1)
                                    {
                                        // 注意：此时 ApplyPriority 内部必须有 ContainsKey 判断，
                                        // 这样它就会直接 SetPriority(work, 1) 而不会覆盖字典里的原始备份。
                                        ApplyPriority(pawn, config.TargetWork);
                                    }
                                }
                            }
                        }
                        continue; // 状态正常或已自愈，跳过后续 Restore 逻辑
                    }

                    // --- 场景 A：从自定义日程 切换到 普通日程 ---
                    if (isLastCustom && !isCurrentCustom && currentDef != lastDef)
                    {
                        Log.Message($"[ScheduleAllMod] {pawn.LabelShort} changed from Custom to Normal schedule. Restoring.");
                        RestoreAllPrioritiesForPawn(pawn);
                    }
                    //--场景B:不同的自定义日程切换
                    else if(isLastCustom && isCurrentCustom && currentDef != lastDef)
                    {
                        Log.Message($"[ScheduleAllMod] {pawn.LabelShort} changed from Custom to Custom1 schedule. Restoring.");
                        RestoreAllPrioritiesForPawn(pawn);
                        if (int.TryParse(currentDef.defName.Replace(ScheduleAllModInit.Prefix, ""), out int index))
                        {
                            if (index >= 0 && index < settings.slotConfigs.Count)
                            {
                                var config = settings.slotConfigs[index];
                                if (config.label == currentDef.label && config.TargetWork != null)
                                {
                                    Log.Message($"[ScheduleAllMod] {pawn.LabelShort} entered Custom schedule. Applying {config.TargetWork.defName}.");
                                    ApplyPriority(pawn, config.TargetWork);
                                }
                            }
                        }
                    }
                    // 更新记录
                }
            }
            finally
            {
                Patch_Pawn_WorkSettings_SetPriority.IsInternalOperation = false;
            }

            CleanupOrphanedRecords();
        }

        private void ApplyPriority(Pawn pawn, WorkTypeDef work)
        {
            if (pawn.WorkTypeIsDisabled(work)) return;
            var key = new Pair<Pawn, WorkTypeDef>(pawn, work);

            if (!originalPriorities.ContainsKey(key))
            {
                int currentPriority = pawn.workSettings.GetPriority(work);

                // 双重保险：如果当前已经是 1 且没备份，说明数据已污染，不进行备份
                if (currentPriority == 1) return;

                originalPriorities[key] = currentPriority;
                Log.Message($"[ScheduleAllMod] BACKUP: {pawn.LabelShort} | {work.defName} = {currentPriority}");
            }

            pawn.workSettings.SetPriority(work, 1);
        }

        private void RestoreAllPrioritiesForPawn(Pawn pawn)
        {
            var keys = originalPriorities.Keys.Where(k => k.First == pawn).ToList();
            foreach (var key in keys)
            {
                if (originalPriorities.TryGetValue(key, out int oldPriority))
                {
                    pawn.workSettings.SetPriority(key.Second, oldPriority);
                    originalPriorities.Remove(key);
                }
            }
        }

        private void CleanupOrphanedRecords()
        {
            // 清理已死亡或销毁的小人引用，防止内存泄漏

            var orphanedKeys = originalPriorities.Keys.Where(k => k.First == null || k.First.Destroyed).ToList();
            foreach (var k in orphanedKeys) originalPriorities.Remove(k);
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