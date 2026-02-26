using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ScheduleAllMod
{
    public class SlotConfig : IExposable
    {
        public string label = "Undefined Schedule";
        public Color color = Color.green;

        // 修改点 1：不再直接存储 WorkTypeDef 对象，改用 string 存储 defName
        public string targetWorkDefName;

        // 修改点 2：提供一个方便的属性来获取/设置真正的 WorkTypeDef
        public WorkTypeDef TargetWork
        {
            get
            {
                if (string.IsNullOrEmpty(targetWorkDefName)) return null;
                return DefDatabase<WorkTypeDef>.GetNamedSilentFail(targetWorkDefName);
            }
            set
            {
                targetWorkDefName = value?.defName;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref label, "label", "Undefined Schedule");
            Scribe_Values.Look(ref color, "color", Color.green);
            // 修改点 3：改用 Scribe_Values 保存字符串，避免跨 Def 加载错误
            Scribe_Values.Look(ref targetWorkDefName, "targetWorkDefName");
        }
    }

    // --- 新增辅助类：用于保存单个小人的所有工作优先级快照 ---
    public class PawnPrioritySnapshot : IExposable
    {
        public string pawnName; // 记录名字，用于跨存档匹配
        public List<string> workDefNames = new List<string>();
        public List<int> priorities = new List<int>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref pawnName, "pawnName");
            Scribe_Collections.Look(ref workDefNames, "workDefNames", LookMode.Value);
            Scribe_Collections.Look(ref priorities, "priorities", LookMode.Value);
        }
    }

    public class ModSettingsData : ModSettings
    {
        public List<SlotConfig> slotConfigs;

        // --- 新增：全局优先级模板存储 ---
        public List<PawnPrioritySnapshot> globalSnapshots = new List<PawnPrioritySnapshot>();

        private static readonly List<Color> PresetColors = new List<Color>
        {
            new Color(0.2f, 0.6f, 1.0f),
            new Color(1.0f, 0.4f, 0.4f),
            new Color(0.0f, 0.8f, 0.4f),
            new Color(0.9f, 0.8f, 0.2f),
            new Color(0.7f, 0.3f, 0.9f),
            new Color(1.0f, 0.6f, 0.1f),
            new Color(0.2f, 0.8f, 0.8f),
            new Color(0.6f, 0.4f, 0.2f),
            new Color(0.95f, 0.4f, 0.7f), // 粉紫色 (Pinkish Purple)
            new Color(0.1f, 0.5f, 0.5f), // 深青色 (Dark Teal)
            new Color(0.7f, 1.0f, 0.1f), // 亮酸橙 (Bright Lime)
            new Color(1.0f, 0.75f, 0.8f) // 淡粉色 (Soft Pink)
        };

        public ModSettingsData()
        {
            InitializeDefaults();
        }

        private void InitializeDefaults()
        {
            if (slotConfigs == null)
            {
                slotConfigs = new List<SlotConfig>();
                for (int i = 0; i < ScheduleAllModInit.SlotCount; i++)
                {
                    slotConfigs.Add(new SlotConfig
                    {
                        label = "Custom Schedule" + (i + 1),
                        color = PresetColors[i]
                    });
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref slotConfigs, "slotConfigs", LookMode.Deep);

            Scribe_Collections.Look(ref globalSnapshots, "globalSnapshots", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (slotConfigs == null) InitializeDefaults();

                // 【建议添加】确保读档后列表不是 null，防止之后代码报错
                if (globalSnapshots == null) globalSnapshots = new List<PawnPrioritySnapshot>();
            }
        }
    }
}