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

        public ScheduleAllMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<ModSettingsData>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            ScheduleAllModInit.ApplySettingsToDefs();

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            Text.Font = GameFont.Medium;
            listing.Label("Schedule All!".Translate());
            Text.Font = GameFont.Small;
            listing.GapLine();

            for (int i = 0; i < ScheduleAllModInit.SlotCount; i++)
            {
                SlotConfig config = Settings.slotConfigs[i];
                Rect rowRect = listing.GetRect(30f);

                // 1. 颜色预览 (固定 24px)
                Rect colorRect = rowRect.LeftPartPixels(24f);
                Widgets.DrawBoxSolid(colorRect.ContractedBy(2f), config.color);

                // 2. 编辑名称 (占据约 25% 宽度)
                Rect labelRect = new Rect(colorRect.xMax + 5f, rowRect.y, rowRect.width * 0.25f, rowRect.height);
                config.label = Widgets.TextField(labelRect, config.label);

                // 3. 重置按钮 (放在最右侧，固定宽度)
                Rect resetRect = rowRect.RightPartPixels(30f);
                // 使用 "X" 或者 "×" 作为重置图标，并添加红色悬停提示
                if (Widgets.ButtonText(resetRect, "X", true, true, true))
                {
                    config.TargetWork = null;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
                TooltipHandler.TipRegion(resetRect, "Reset To None");

                // 4. 选择工作类型 (占据剩余中间全部空间)
                // 宽度计算：总宽 - 颜色 - 名称 - 重置 - 间距
                float workBtnWidth = rowRect.width - colorRect.width - labelRect.width - resetRect.width - 20f;
                Rect buttonRect = new Rect(labelRect.xMax + 5f, rowRect.y, workBtnWidth, rowRect.height);

                WorkTypeDef currentWork = config.TargetWork;
                string workLabel;
                if (currentWork == null)
                {
                    workLabel = "None".Translate();
                }
                else
                {
                    // 避开 LabelCap，直接处理 label 字符串
                    string translated = currentWork.labelShort.Translate(); // 尝试获取翻译

                    // 如果翻译失败（返回了原字符串）且原字符串为空，则用 defName
                    if (translated.NullOrEmpty())
                    {
                        translated = currentWork.defName;
                    }

                    workLabel = translated.CapitalizeFirst();
                }
                // 直接调用 Init 类中的静态方法
                if (Widgets.ButtonText(buttonRect, workLabel))
                {
                    ScheduleAllModInit.FloatMenu_ChooseWorkType(config);
                }

                listing.Gap(6f);
            }

            listing.GapLine();
            if (listing.ButtonText("Uninstall".Translate()))
            {
                CleanUpForUninstall();
            }

            listing.End();
            // 应用设置到 Def 实例
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