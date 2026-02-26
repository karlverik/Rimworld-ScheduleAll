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

    public class ModSettingsData : ModSettings
    {
        public List<SlotConfig> slotConfigs;

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

            // 确保在读取后如果列表为空则初始化
            if (Scribe.mode == LoadSaveMode.PostLoadInit && slotConfigs == null)
            {
                InitializeDefaults();
            }
        }
    }
}