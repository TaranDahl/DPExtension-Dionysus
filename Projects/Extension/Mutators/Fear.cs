using DynamicPatcher;
using Extension.Decorators;
using Extension.Ext;
using Extension.Utilities;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Mutators
{
    [Serializable]
    public class Fear : BuffMutator
    {
        // Mutator
        public override string UIName => "无边恐惧";
        public override string Description => "玩家的单位在受到伤害时会不时地停止攻击，并且害怕地到处乱跑。";
        public override bool IsAvailableInRPG => true;
        public override int Score => 3;
        public Fear(Pointer<HouseClass> owner) : base(owner) { }
        public override void Init(bool isInitial = true)
        {
            base.Init();
            foreach (var techno in TechnoClass.Array)
            {
                if (IsTechnoValid(techno))
                    BuffTechno(techno);
            }
        }
        public override void Uninit()
        {
            foreach (var techno in TechnoClass.Array)
            {
                if (IsTechnoValid(techno))
                    UnbuffTechno(techno);
            }
            base.Uninit();
        }

        // BuffMutator
        protected override bool IsBuffEnemy => true;
        protected override void BuffTechno(Pointer<TechnoClass> techno)
        {
            var ext = TechnoExt.ExtMap.Find(techno);
            if (ext.Get(FearfulBuff.ID) == null)
                ext.CreateDecorator<FearfulBuff>(FearfulBuff.ID, "FearfulBuff");
        }
        protected override void UnbuffTechno(Pointer<TechnoClass> techno)
        {
            var ext = TechnoExt.ExtMap.Find(techno);
            ext.Remove(FearfulBuff.ID);
        }
        public override void OnTechnoCTOR(Pointer<TechnoClass> techno)
        {
            if (IsTechnoValid(techno))
                BuffTechno(techno);
        }
        public override void OnTechnoChangeOwner(Pointer<TechnoClass> techno, Pointer<HouseClass> toHouse)
        {
            if (!IsTechnoOwnerValid(toHouse))
                UnbuffTechno(techno);
            else
                BuffTechno(techno);
        }

        // Fear
        private bool IsTechnoValid(Pointer<TechnoClass> techno)
        {
            if (!IsTechnoOwnerValid(techno.Ref.BaseAbstract.GetOwningHouse()))
                return false;

            // 建筑不能被恐惧
            if ((techno.Ref.BaseAbstract.AbstractFlags & AbstractFlags.Foot) == AbstractFlags.None)
                return false;
            return true;
        }

        [Serializable]
        private class FearfulBuff : EventDecorator
        {
            // Decorator
            public static new DecoratorId ID => new DecoratorId((int)TechnoDecoratorIDs.FearfulBuff);

            // EventDecorator
            public override DamageState AfterReceiveDamage(Pointer<int> pDamage, int DistanceFromEpicenter, Pointer<WarheadTypeClass> pWH, Pointer<ObjectClass> pAttacker, bool IgnoreDefenses, bool PreventPassengerEscape, Pointer<HouseClass> pAttackingHouse, DamageState result, int damageTaken)
            {
                // 必须有伤害
                if (result == DamageState.Unaffected)
                    return result;
                if (damageTaken <= 0)
                    return result;
                // 不恐惧建筑
                var pThis = (Decorative as TechnoExt).OwnerObject;
                if ((pThis.Ref.BaseAbstract.AbstractFlags & AbstractFlags.Foot) == AbstractFlags.None)
                    return result;
                // 死了就别恐惧了
                if (result == DamageState.NowDead)
                    return result;
                if (ScenarioClass.Instance.Random.RandomChance(20, pThis.Ref.GetTechnoType().Ref.Base.Strength))
                    GoFear(pAttacker.Convert<TechnoClass>());
                return result;
            }

            // FearBuff
            private void GoFear(Pointer<TechnoClass> pAttacker)
            {
                var victimExt = Decorative as TechnoExt;
                var victim = victimExt.OwnerObject;
                var duration = IsHero(victim.Ref.GetTechnoType()) ? TimeToFrame(0, 5) : TimeToFrame(0, 10);
                var fearBuff = victimExt.Get(FearBuff.ID);
                // 有buff就重置时间
                if (fearBuff != null)
                    (fearBuff as FearBuff).Restart(duration);
                else
                {
                    // 没buff就创建个新buff
                    int facing8 = -1;
                    if (pAttacker.IsNotNull)
                        facing8 = pAttacker.Ref.Base.Direction(victim.Convert<AbstractClass>()).value8();
                    victimExt.CreateDecorator<FearBuff>(FearBuff.ID, "FearBuff", duration, facing8);
                    // 概率发出恐惧声音
                    var feedbackList = victim.Ref.GetTechnoType().Ref.VoiceFeedback;
                    if (feedbackList.Count() > 0 && ScenarioClass.Instance.Random.RandomChance(30))
                        VocClass.PlayAt(ScenarioClass.GetRandomInDVC(feedbackList), victim.Ref.BaseAbstract.GetCoords());
                }
            }
        }

        [Serializable]
        private class FearBuff : EventDecorator
        {
            // static 
            private static Pointer<AnimTypeClass> FearAnim => AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("FearAnim");

            // Decorator
            public static DecoratorId ID => new DecoratorId((int)TechnoDecoratorIDs.FearBuff);

            public FearBuff(int time, int facing = -1)
            {
                NextDir = facing != -1 ? facing : ScenarioClass.Instance.Random.RandomRanged(0, 7);
                Duration = time;
            }

            // EventDecorator
            public override void OnUpdate()
            {
                var ext = Decorative as TechnoExt;
                var pThis = ext.OwnerObject;
                // 动画
                if (MyAnim.IsNull)
                {
                    MyAnim = new SwizzleablePointer<AnimClass>(YRMemory.Create<AnimClass>(FearAnim, pThis.Ref.BaseAbstract.GetCoords()));
                    MyAnim.Ref.SetOwnerObject(pThis.Convert<ObjectClass>());
                }
                // 检查超时
                if (Counter >= Duration)
                {
                    MyAnim.Ref.Base.UnInit();
                    ext.Remove(this);
                    return;
                }
                // 更新方向
                if (ChangeDirCounter == 0)
                    NextDir = ScenarioClass.Instance.Random.RandomRanged(0, 7);
                // 恐惧效果
                // 不能开火
                if (pThis.Ref.ROFTimer.GetTimeLeft() <= 2)
                    pThis.Ref.ROFTimer.Start(2);
                // 乱跑
                if (!pThis.Ref.Deactivated)
                {
                    var cellThis = MapClass.Instance.GetCellAt(pThis);
                    var cellDest = cellThis.Ref.GetNeighbourCell(NextDir);
                    pThis.Ref.SetTarget(Pointer<AbstractClass>.Zero);
                    pThis.Ref.SetDestination(cellDest);
                    pThis.Ref.BaseMission.QueueMission(Mission.Move, true);
                }

                ++Counter;
                ++ChangeDirCounter;
                ChangeDirCounter %= ChangeDirDelay;
            }

            // FearBuff
            private int Counter = 0;
            private int Duration = 0;
            private int NextDir = -1;
            private int ChangeDirCounter = 1;
            private static int ChangeDirDelay => TimeToFrame(0, 2);
            private SwizzleablePointer<AnimClass> MyAnim = new SwizzleablePointer<AnimClass>(Pointer<AnimClass>.Zero);

            public void Restart(int duration)
            {
                Duration = duration;
                Counter = 0;
            }
        }
    }
}
