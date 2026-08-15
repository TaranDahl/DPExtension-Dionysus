using System;
using System.Collections.Generic;
using System.Diagnostics.PerformanceData;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DynamicPatcher;
using Extension.Decorators;
using Extension.Ext;
using Extension.Script;
using Extension.Utilities;
using PatcherYRpp;
using static System.Net.Mime.MediaTypeNames;

namespace Extension.Mutators
{
    // 数值改动
    // 传染范围：3格
    // 伤害：1秒1%
    [Serializable]
    public class BlackDeath : BuffMutator
    {
        // Mutator
        public override string UIName => "黑死病";
        public override string Description => "一些敌方单位携带着一种疫病，不仅会持续造成伤害，还会传染给附近的其它单位。\n此类敌人被消灭时，他们会把这种疫病传染给你的单位。";
        public override bool IsAvailableInRPG => false;
        public override int Score => 7;
        public BlackDeath(Pointer<HouseClass> owner) : base(owner) { }
        public override void Init(bool isInitial = true)
        {
            // 生效时，给所有友方单位概率贴buff
            base.Init();
            foreach (var techno in TechnoClass.Array)
            {
                if (!IsValidForInitialWithBuff(techno))
                    continue;

                if (ScenarioClass.Instance.Random.RandomRanged(0, 100) <= 33)
                    BuffTechno(techno);
            }
        }
        public override void Uninit()
        {
            // 失效时移除所有友方单位身上的buff
            foreach (var techno in TechnoClass.Array)
            {
                if (!IsValidForInitialWithBuff(techno))
                    continue;

                UnbuffTechno(techno);
            }
            base.Uninit();
        }

        // BuffMutator
        protected override bool IsBuffEnemy => false;
        protected override void BuffTechno(Pointer<TechnoClass> techno)
        {
            var ext = TechnoExt.ExtMap.Find(techno);
            if (ext.Get(BlackDeathBuff.ID) == null)
                ext.CreateDecorator<BlackDeathBuff>(BlackDeathBuff.ID, "BlackDeathBuff", this);
        }
        protected override void UnbuffTechno(Pointer<TechnoClass> techno)
        {
            var ext = TechnoExt.ExtMap.Find(techno);
            ext.Remove(BlackDeathBuff.ID);
        }
        public override void OnTechnoCTOR(Pointer<TechnoClass> techno)
        {
            if (!IsValidForInitialWithBuff(techno))
                return;

            if (ScenarioClass.Instance.Random.RandomRanged(0, 100) <= 33)
                BuffTechno(techno);
        }

        // BlackDeath
        public bool IsValidForInitialWithBuff(Pointer<TechnoClass> techno)
        {
            if (techno.Ref.Base.Base.WhatAmI() == AbstractType.Building && !techno.Ref.Base.IsStrange())
                return false;

            var technoOwner = techno.Ref.Owner;
            return IsTechnoOwnerValid(technoOwner);
        }

        [Serializable]
        private class BlackDeathBuff : EventDecorator
        {
            // static 
            private static SwizzleablePointer<AnimTypeClass> blackDeathAnim => new SwizzleablePointer<AnimTypeClass>(AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("VENOM"));
            private static SwizzleablePointer<AnimTypeClass> blackDeathDestroyAnim => new SwizzleablePointer<AnimTypeClass>(AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("VENOMKILL"));
            private static SwizzleablePointer<WarheadTypeClass> blackDeathWH => new SwizzleablePointer<WarheadTypeClass>(WarheadTypeClass.ABSTRACTTYPE_ARRAY.Find("BlackDeathDamageWH"));

            // Decorator
            public new static DecoratorId ID => new DecoratorId((int)TechnoDecoratorIDs.BlackDeathBuff);
            public override int Priority => -2;

            // EventDecorator
            public override unsafe void OnUpdate()
            {
                TechnoExt victimExt = Decorative as TechnoExt;
                var victim = victimExt.OwnerObject;
                var crd = victimExt.OwnerObject.Ref.Base.Base.GetCoords();

                if (counter == 0)
                {
                    Pointer<AnimClass> anim = YRMemory.Create<AnimClass>(blackDeathAnim.Pointer, crd);
                    anim.Ref.SetOwnerObject((Pointer<ObjectClass>)(ObjectClass*)victim);
                }

                counter++;
                var counterMod = counter % 15;

                if (!CanDamage(victim))
                    return;
                if (counterMod == 0 || counterMod == 8)
                {
                    // 数值 : 0.5秒0.5%
                    // 为了避免舍入误差 , 累计伤害
                    damageAccumulated += ((double)victim.Ref.GetTechnoType().Ref.Base.Strength) * 0.005;
                    if (damageAccumulated >= 10)
                    {
                        int damage = (int)damageAccumulated;
                        damageAccumulated -= damage;
                        if (damage > 0)
                            victim.Ref.Base.ReceiveDamage(damage, 0, blackDeathWH, (Pointer<ObjectClass>)(ObjectClass*)victim, false, false, victim.Ref.Base.Base.GetOwningHouse());
                    }
                }
            }

            public override unsafe DamageState AfterReceiveDamage(Pointer<int> pDamage, int DistanceFromEpicenter, Pointer<WarheadTypeClass> pWH,
            Pointer<ObjectClass> pAttacker, bool IgnoreDefenses, bool PreventPassengerEscape, Pointer<HouseClass> pAttackingHouse, DamageState result, int damageTaken)
            {
                // 死亡时才传染
                if (result != DamageState.NowDead)
                    return result;

                // 因子失效了则不传染
                if (!myMutator.IsActive())
                    return result;

                TechnoExt victimExt = Decorative as TechnoExt;
                var crd = victimExt.OwnerObject.Ref.Base.Base.GetCoords();

                // 死亡动画
                YRMemory.Create<AnimClass>(blackDeathDestroyAnim.Pointer, crd);

                // 查找
                List<Pointer<TechnoClass>> canBeInfested = new List<Pointer<TechnoClass>>();
                foreach (var techno in TechnoClass.Array)
                {
                    // 水平距离
                    crd.Z = techno.Ref.Base.GetZ();
                    if (crd.DistanceFrom(techno.Ref.Base.Location) > 768)
                        continue;
                    if (CanInfest(techno))
                        canBeInfested.Add(techno);
                }

                // 随机选择
                List<Pointer<TechnoClass>> unluckys = new List<Pointer<TechnoClass>>();
                if (canBeInfested.Count <= 3)
                    unluckys = canBeInfested;
                else
                {
                    var max = canBeInfested.Count;
                    while (unluckys.Count < 3)
                    {
                        var selected = canBeInfested[ScenarioClass.Instance.Random.RandomRanged(0, max - 1)];
                        if (unluckys.Contains(selected))
                            continue;
                        unluckys.Add(selected);
                    }
                }

                // 传染
                foreach (var unlucky in unluckys)
                {
                    myMutator.BuffTechno(unlucky);
                }

                return result;
            }

            // BlackDeathBuff
            public BlackDeath myMutator { get; private set; }
            private SwizzleablePointer<HouseClass> mutatorOwner { get; set; }
            private int counter = 0;
            private double damageAccumulated = 0.0;
            public BlackDeathBuff(BlackDeath mutator)
            {  
                myMutator = mutator;
                mutatorOwner = new SwizzleablePointer<HouseClass>(myMutator.Owner);
            }
            private bool CanInfest(Pointer<TechnoClass> techno)
            {
                var pTechno = techno.Ref;

                // 如果因子失效了那就不能传染了
                if (!myMutator.IsActive())
                    return false;

                // 不传染尸体
                if (!pTechno.Base.IsAlive)
                    return false;

                // 只传染对手
                var pMutator = myMutator;
                if (!myMutator.IsOnTheirSide(pTechno.Base.Base.GetOwningHouse()))
                    return false;

                // 不传染建筑
                if (pTechno.Base.Base.WhatAmI() == AbstractType.Building)
                    return false;

                // 不重复传染
                var ext = TechnoExt.ExtMap.Find(techno);
                if (ext.Get(BlackDeathBuff.ID) != null) return false;

                return true;
            }

            private bool CanDamage(Pointer<TechnoClass> techno)
            {
                var pTechno = techno.Ref;

                // 不伤害尸体
                if (!pTechno.Base.IsAlive)
                    return false;

                // 不伤害不在地图上的单位
                if (pTechno.Base.InLimbo)
                    return false;

                // 只伤害对手
                if (!myMutator.IsOnTheirSide(pTechno.Base.Base.GetOwningHouse()))
                    return false;

                return true;
            }

        }
    }
}
