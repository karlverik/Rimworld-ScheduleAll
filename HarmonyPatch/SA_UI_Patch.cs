using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ScheduleAllMod
{
    [HarmonyPatch(typeof(TimeAssignmentSelector), "DrawTimeAssignmentSelectorGrid")]
    public static class Patch_DrawTimeAssignmentSelectorGrid
    {
        // 缓存一个默认显示的图标（可以是第一个槽位的颜色）
        private static Texture2D cachedIcon;
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Rect rect)
        {
            var settings = ScheduleAllMod.Settings;
            if (Current.ProgramState != ProgramState.Playing || settings == null || settings.slotConfigs == null) return;

            var allDefs = DefDatabase<TimeAssignmentDef>.AllDefsListForReading;
            int existingIconsCount = 0;
            foreach (var def in allDefs)
            {
                if (def != null && !def.defName.StartsWith(ScheduleAllModInit.Prefix))
                {
                    existingIconsCount++;
                }
            }
            // 1. 计算位置：排在原版之后
            int vanillaCount = existingIconsCount;
            Rect baseRect = rect;
            baseRect.width /= 2f;
            baseRect.height /= 2f;
            baseRect.x += baseRect.width * vanillaCount;

            Rect drawRect = baseRect.ContractedBy(2f);

            // 2. 绘制入口图标
            // 如果当前选中了 SA 槽位，显示对应的颜色；否则显示灰色
            Color boxColor = Color.gray;
            string currentLabel = "CustomSchedule";

            for (int i = 0; i < ScheduleAllModInit.SlotCount; i++)
            {
                string dName = ScheduleAllModInit.Prefix + i;
                if (TimeAssignmentSelector.selectedAssignment?.defName == dName)
                {
                    boxColor = settings.slotConfigs[i].color;
                    currentLabel = settings.slotConfigs[i].label;
                    break;
                }
            }

            Widgets.DrawBoxSolid(drawRect, boxColor);

            // 绘制一个简单的 "+" 号或文字提示
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            Widgets.Label(drawRect, "+");
            Text.Anchor = TextAnchor.UpperLeft;

            // 3. 悬停与选中框
            if (Mouse.IsOver(drawRect))
            {
                Widgets.DrawHighlight(drawRect);
                TooltipHandler.TipRegion(drawRect, currentLabel);
            }

            // 如果当前选中的就是我们的自定义 Def，画个白框
            if (TimeAssignmentSelector.selectedAssignment?.defName.StartsWith(ScheduleAllModInit.Prefix) ?? false)
            {
                Widgets.DrawBox(drawRect, 2);
            }

            // 4. 关键：交互逻辑 - 点击弹出 8 个槽位的下拉菜单
            // 4. 关键：交互逻辑 - 弹出 6 行 x 2 列的紧凑菜单
            if (Widgets.ButtonInvisible(drawRect))
            {
                // 基础安全检查：确保配置文件存在
                if (settings?.slotConfigs == null) return;

                List<FloatMenuOption> options = new List<FloatMenuOption>();

                for (int i = 0; i < ScheduleAllModInit.SlotCount; i++)
                {
                    // 获取当前索引配置
                    var config = settings.slotConfigs[i];
                    string defName = ScheduleAllModInit.Prefix + i;

                    // 从数据库安全获取对应的日程定义
                    TimeAssignmentDef def = DefDatabase<TimeAssignmentDef>.GetNamedSilentFail(defName);

                    // 如果该槽位没有对应的定义或未设置目标工作，则跳过
                    if (def == null || config.TargetWork == null) continue;

                    // 创建菜单项
                    FloatMenuOption option = new FloatMenuOption(
                        config.label,
                        () =>
                        {
                            // 点击逻辑：切换当前选中的日程类型
                            TimeAssignmentSelector.selectedAssignment = def;
                        },
                        MenuOptionPriority.Default
                    );

                    // --- 在菜单项左侧注入颜色块预览 ---
                    // 设定额外绘图区域的宽度
                    option.extraPartWidth = 24f;
                    option.extraPartOnGUI = (Rect r) =>
                    {
                        // 在预留区域内绘制代表该槽位颜色的实心方块
                        // ContractedBy(4f) 用于在方块边缘留出边距，使其不至于紧贴边界
                        Widgets.DrawBoxSolid(r.ContractedBy(4f), config.color);
                        return false; // 返回 false 表示点击色块不会拦截菜单主体的点击事件
                    };

                    options.Add(option);
                }

                // 如果生成了有效的选项，则显示下拉菜单
                if (options.Count > 0)
                {
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            }
        }
    }
}