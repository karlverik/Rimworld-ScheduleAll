using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ScheduleAllMod
{
    // 这个补丁专门用来处理与 RealDining 等模组的兼容性问题
    // 某些模组会遍历 TimeAssignmentDef 库，如果遇到本模组动态注入的 Def 可能会导致 UI 布局溢出或逻辑报错
    [HarmonyPatch(typeof(TimeAssignmentSelector), "DrawTimeAssignmentSelectorGrid")]
    public static class RealDining_Compatibility_Interceptor
    {
        private static List<TimeAssignmentDef> hiddenDefs = new List<TimeAssignmentDef>();

        // 设置最高优先级，确保在其他模组的 Prefix 之前执行（暂时隐藏我们的 Def）
        [HarmonyPriority(Priority.First)]
        public static void Prefix()
        {
            // 1. 获取底层真正的 List 引用
            // 在 RimWorld 中，AllDefs 实际上指向的是数据库内部的私有 List
            var allDefsList = DefDatabase<TimeAssignmentDef>.AllDefs as List<TimeAssignmentDef>;

            if (allDefsList == null) return;

            // 2. 找出所有属于我们模组的 Def
            hiddenDefs = allDefsList.Where(d => d.defName.StartsWith(ScheduleAllModInit.Prefix)).ToList();

            if (hiddenDefs.Any())
            {
                // 3. 从数据库列表中暂时移除这些 Def
                // 这样后续执行的其他模组补丁（如 RealDining）就看不到这些自定义日程了
                foreach (var def in hiddenDefs)
                {
                    allDefsList.Remove(def);
                }
            }
        }

        // 设置最低优先级，确保在原方法和所有其他补丁运行完后，把 Def 还原回去
        [HarmonyPriority(Priority.Last)]
        public static void Postfix()
        {
            var allDefsList = DefDatabase<TimeAssignmentDef>.AllDefs as List<TimeAssignmentDef>;

            if (allDefsList == null || !hiddenDefs.Any()) return;

            foreach (var def in hiddenDefs)
            {
                // 4. 只有当数据库里还没有时才加回去，防止重复添加
                if (!allDefsList.Contains(def))
                {
                    allDefsList.Add(def);
                }
            }

            // 5. 清空临时列表，释放引用
            hiddenDefs.Clear();
        }
    }
}