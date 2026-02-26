using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ScheduleAllMod
{
    public class WorkTypeSelectWindow : Window
    {
        private SlotConfig config;
        private List<WorkTypeDef> allWorks;
        private Vector2 scrollPosition;

        // 窗口大小
        public override Vector2 InitialSize => new Vector2(520f, 480f);

        public WorkTypeSelectWindow(SlotConfig config)
        {
            this.config = config;

            // 1. 调用统一的列表刷新逻辑（包含排序）
            RefreshWorkList();

            this.doCloseButton = true;
            this.doCloseX = true;
            this.closeOnClickedOutside = true;
            this.absorbInputAroundWindow = true;
        }

        private void RefreshWorkList()
        {
            // 2. 获取所有工作类型并过滤空值
            var rawList = DefDatabase<WorkTypeDef>.AllDefsListForReading;

            this.allWorks = rawList
                .Where(x => x != null)
                // 3. 修改点：首先按照 naturalPriority 降序排列（优先级数值越大越靠前）
                .OrderByDescending(x => x.naturalPriority)
                // 4. 修改点：如果优先级相同，则按照 Label 或 defName 升序排列
                .ThenBy(x =>
                {
                    string label = x.LabelCap;
                    return !string.IsNullOrEmpty(label) ? label : x.defName;
                })
                .ToList();

            // 调试输出
            /*if (this.allWorks.Count > 0)
            {
                Log.Message($"[ScheduleAll] 工作列表刷新成功（按优先级排序）。第一项: '{this.allWorks[0].defName}' (优先级: {this.allWorks[0].naturalPriority})");
            }*/
        }

        public override void DoWindowContents(Rect inRect)
        {
            // 解决切换语言消失的关键：如果列表为空，在此强制重抓
            if (allWorks == null || allWorks.Count == 0)
            {
                RefreshWorkList();
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(inRect.TopPartPixels(35f), "SelectWorkType".Translate());
            Text.Font = GameFont.Small;

            // 留出标题和底部关闭按钮的空间
            Rect outRect = new Rect(inRect.x, inRect.y + 45f, inRect.width, inRect.height - 110f);

            // 计算滚动区域高度：(None + 总数) / 2列
            float itemHeight = 32f;
            float viewHeight = Mathf.Ceil((allWorks.Count + 1) / 2f) * itemHeight;
            Rect viewRect = new Rect(0, 0, outRect.width - 16f, viewHeight);

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            // 设置 2 列显示
            listing.ColumnWidth = (viewRect.width - 10f) / 2f;

            // 1. None 选项
            if (listing.ButtonText("None".Translate()))
            {
                config.TargetWork = null;
                this.Close();
            }

            // 2. 遍历缓存的工作列表
            foreach (var w in allWorks)
            {
                // 安全获取显示名称：优先翻译后的 label，其次 defName
                string displayName = w.labelShort.Translate();

                // 如果翻译失败（返回了原字符串）且原字符串为空，则用 defName
                if (displayName.NullOrEmpty())
                {
                    displayName = w.defName;
                }

                if (listing.ButtonText(displayName))
                {
                    config.TargetWork = w;
                    this.Close();
                }
            }

            listing.End();
            Widgets.EndScrollView();
        }
    }
}