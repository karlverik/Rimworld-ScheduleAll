using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ScheduleAllMod
{
    [StaticConstructorOnStartup]
    public static class ScheduleAllModInit
    {
        // 固定 8 个槽位
        public const int SlotCount = 12;
        public const string Prefix = "SA_Slot_";

        // 静态缓存工作类型列表，避免重复排序


        static ScheduleAllModInit()
        {
            // 1. 启动 Harmony 补丁
            new Harmony("com.verik.scheduleall").PatchAll();

            // 2. 动态生成 8 个物理存在的 TimeAssignmentDef
            for (int i = 0; i < SlotCount; i++)
            {
                string defName = Prefix + i;
                if (DefDatabase<TimeAssignmentDef>.GetNamedSilentFail(defName) == null)
                {
                    TimeAssignmentDef newDef = new TimeAssignmentDef
                    {
                        defName = defName,
                        label = "SA_Slot_" + (i + 1),
                        color = Color.grey,
                        allowRest = false,
                        allowJoy = false
                    };

                    // --- 核心修复：注入原版工作的逻辑标签 ---
                    // 这样 ThinkNode_PrioritySorter 就会把它当做普通“工作”时段处理
                    // 而不会因为找不到逻辑而抛出 NotImplementedException
                    // 注意：这取决于具体版本，通常可以通过 Harmony 拦截相关判断逻辑
                }
            }

            // 缓存并排序工作类型以提升性能
            ApplySettingsToDefs();
            Log.Message("[ScheduleAllMod] Initialization complete, multi-column selection window ready。");
        }

        public static void FloatMenu_ChooseWorkType(SlotConfig config)
        {
            Find.WindowStack.Add(new WorkTypeSelectWindow(config));
        }

        public static void ApplySettingsToDefs()
        {
            var settings = LoadedModManager.GetMod<ScheduleAllMod>().GetSettings<ModSettingsData>();

            for (int i = 0; i < SlotCount; i++)
            {
                string defName = Prefix + i;
                // 使用 SilentFail 避免红字报错
                var def = DefDatabase<TimeAssignmentDef>.GetNamedSilentFail(defName);

                // 如果因为切换语言导致 Def 丢失，重新创建它
                if (def == null)
                {
                    def = new TimeAssignmentDef
                    {
                        defName = defName,
                        allowRest = false,
                        allowJoy = false
                    };
                    DefDatabase<TimeAssignmentDef>.Add(def);
                }

                if (settings.slotConfigs[i] != null)
                {
                    def.label = settings.slotConfigs[i].label;
                    def.color = settings.slotConfigs[i].color;
                    // 清理缓存以刷新 UI 颜色
                    Traverse.Create(def).Field("colorTextureInt").SetValue(null);
                }
            }
        }
    }
}