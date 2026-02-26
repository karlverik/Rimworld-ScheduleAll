using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ScheduleAllMod
{

    public class ScheduleAllMod : Mod
    {

        public static ModSettingsData Settings;

        private Vector2 scrollPosition = Vector2.zero;

        public ScheduleAllMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<ModSettingsData>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            ScheduleAllModInit.ApplySettingsToDefs();

            // 1. 计算视图总高度：槽位数量 * (高度+间距) + 顶部标题 + 底部操作区高度
            float totalContentHeight = ScheduleAllModInit.SlotCount * 36f + 250f;
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, totalContentHeight);

            // 2. 开启滚动视图 (注意：inRect 是可见窗口，viewRect 是内容全长)
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);

            Listing_Standard listing = new Listing_Standard();

            // 【关键修正】：这里必须使用 viewRect 而不是 inRect
            listing.Begin(viewRect);

            Text.Font = GameFont.Medium;
            listing.Label("Schedule All!".Translate());
            Text.Font = GameFont.Small;
            listing.GapLine();

            // --- 循环绘制槽位 ---
            for (int i = 0; i < ScheduleAllModInit.SlotCount; i++)
            {
                SlotConfig config = Settings.slotConfigs[i];
                Rect rowRect = listing.GetRect(30f);

                // 颜色预览
                Rect colorRect = rowRect.LeftPartPixels(24f);
                Widgets.DrawBoxSolid(colorRect.ContractedBy(2f), config.color);

                // 编辑名称
                Rect labelRect = new Rect(colorRect.xMax + 5f, rowRect.y, rowRect.width * 0.25f, rowRect.height);
                config.label = Widgets.TextField(labelRect, config.label);

                // 重置按钮
                Rect resetRect = rowRect.RightPartPixels(30f);
                if (Widgets.ButtonText(resetRect, "X", true, true, true))
                {
                    config.TargetWork = null;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }

                // 选择工作类型按钮
                float workBtnWidth = rowRect.width - colorRect.width - labelRect.width - resetRect.width - 20f;
                Rect buttonRect = new Rect(labelRect.xMax + 5f, rowRect.y, workBtnWidth, rowRect.height);

                WorkTypeDef currentWork = config.TargetWork;
                string workLabel = (currentWork == null) ? "None".Translate() : currentWork.labelShort.Translate().CapitalizeFirst();

                if (Widgets.ButtonText(buttonRect, workLabel))
                {
                    ScheduleAllModInit.FloatMenu_ChooseWorkType(config);
                }

                listing.Gap(6f);
            }

            // --- 全局模板操作区 ---
            listing.Gap(10f);
            listing.GapLine();
            listing.Label("Save To XML".Translate());

            Rect templateBtnRect = listing.GetRect(30f);
            if (Widgets.ButtonText(templateBtnRect.LeftHalf().ContractedBy(2f), "Save".Translate()))
            {
                PrioritySnapshotUtils.SaveAllPrioritiesToGlobal(Settings);
            }

            if (Widgets.ButtonText(templateBtnRect.RightHalf().ContractedBy(2f), "Load".Translate()))
            {
                PrioritySnapshotUtils.LoadAllPrioritiesFromGlobal(Settings);
            }

            // --- 卸载按钮 ---
            listing.GapLine();
            if (listing.ButtonText("Uninstall".Translate()))
            {
                CleanUpForUninstall();
            }

            listing.End();
            Widgets.EndScrollView(); // 必须在 listing.End() 之后

            ScheduleAllModInit.ApplySettingsToDefs();
        }

        private void CleanUpForUninstall()
        {
            // 1. 还原所有被劫持的工作优先级
            var manager = Current.Game?.GetComponent<GameComponent_ScheduleManager>();
            if (manager != null)
            {
                // 必须暂时放行内部操作锁，确保还原能写进原版数据
                Patch_Pawn_WorkSettings_SetPriority.IsInternalOperation = true;
                try
                {
                    foreach (var kvp in manager.originalPriorities)
                    {
                        Pawn pawn = kvp.Key.First;
                        WorkTypeDef work = kvp.Key.Second;
                        int oldPriority = kvp.Value;

                        if (pawn != null && !pawn.Destroyed && pawn.workSettings != null)
                        {
                            pawn.workSettings.SetPriority(work, oldPriority);
                        }
                    }
                    manager.originalPriorities.Clear();
                }
                finally
                {
                    Patch_Pawn_WorkSettings_SetPriority.IsInternalOperation = false;
                }
            }

            // 2. 将所有小人的自定义日程洗白，恢复为“任意(Anything)”
            var pawns = PawnsFinder.AllMaps_FreeColonistsSpawned;
            int fixedCount = 0;
            foreach (var pawn in pawns)
            {
                if (pawn.timetable?.times != null)
                {
                    for (int i = 0; i < pawn.timetable.times.Count; i++)
                    {
                        var currentDef = pawn.timetable.times[i];
                        if (currentDef != null && currentDef.defName.StartsWith(ScheduleAllModInit.Prefix))
                        {
                            // 替换回原版的 Anything
                            pawn.timetable.times[i] = TimeAssignmentDefOf.Anything;
                            fixedCount++;
                        }
                    }
                }
            }

            // 3. 弹窗提示玩家立即保存
            Messages.Message($"Cleanup complete! Fixed {fixedCount} schedule slots. Please SAVE your game now and you can safely remove the mod.", MessageTypeDefOf.PositiveEvent);
        }


        public override string SettingsCategory() => "ScheduelAll".Translate();
    }
}