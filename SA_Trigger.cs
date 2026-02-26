using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ScheduleAllMod
{
    public class GameComponent_ScheduleAll : GameComponent
    {
        private int lastHour = -1;
        public GameComponent_ScheduleAll(Game game) : base() { }

        public override void GameComponentTick()
        {
            // 每小时检查一次
            if (Find.TickManager.TicksGame % 2500 == 0)
            {
                int currentHour = GenLocalDate.HourOfDay(Find.CurrentMap);
                if (currentHour != lastHour)
                {
                    lastHour = currentHour;

                    // .ToList() 解决 Collection was modified 报错
                    var pawns = PawnsFinder.AllMaps_FreeColonistsSpawned.ToList();
                    foreach (var pawn in pawns)
                    {
                        if (pawn?.timetable?.CurrentAssignment != null &&
                            pawn.timetable.CurrentAssignment.defName.StartsWith(ScheduleAllModInit.Prefix))
                        {
                            // 强制中断闲逛，让小人立即触发优先级重新评估
                            if (pawn.CurJob != null && pawn.CurJob.def == JobDefOf.Wait_Wander)
                            {
                                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                            }
                        }
                    }
                }
            }
        }
    }
}