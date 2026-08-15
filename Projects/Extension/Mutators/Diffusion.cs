using DynamicPatcher;
using Extension.Decorators;
using Extension.Ext;
using Extension.Utilities;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Extension.Mutators
{
    [Serializable]
    public class Diffusion : BuffMutator
    {
        // Mutator
        public override string UIName => "伤害散射";
        public override string Description => "对敌人造成的伤害将平摊给所有附近的单位，包括你的单位。";
        public override bool IsAvailableInRPG => true;
        public override int Score => 3;

        public Diffusion(Pointer<HouseClass> owner) : base(owner) { }

        public override void Init(bool isInitial = true)
        {
            base.Init(isInitial);
            // 生效时，给所有友方单位贴buff
            foreach (var techno in TechnoClass.Array)
            {
                if (IsTechnoValid(techno))
                    BuffTechno(techno);
            }
        }

        public override void Uninit()
        {
            // 失效时移除所有友方单位身上的buff
            foreach (var techno in TechnoClass.Array)
            {
                if (IsTechnoValid(techno))
                    UnbuffTechno(techno);
            }
            base.Uninit();
        }

        // BuffMutator
        protected override bool IsBuffEnemy => false;

        protected override void BuffTechno(Pointer<TechnoClass> techno)
        {
            var ext = TechnoExt.ExtMap.Find(techno);
            if (ext.Get(DiffusionBuff.ID) == null)
                ext.CreateDecorator<DiffusionBuff>(DiffusionBuff.ID, "DiffusionBuff");
        }

        protected override void UnbuffTechno(Pointer<TechnoClass> techno)
        {
            var ext = TechnoExt.ExtMap.Find(techno);
            ext.Remove(DiffusionBuff.ID);
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

        // Diffusion
        private static Pointer<WarheadTypeClass> DiffusionWH => WarheadTypeClass.ABSTRACTTYPE_ARRAY.Find("DiffusionWH");

        private bool IsTechnoValid(Pointer<TechnoClass> techno)
        {
            if (!IsTechnoOwnerValid(techno.Ref.BaseAbstract.GetOwningHouse()))
                return false;

            return true;
        }

        [Serializable]
        private class DiffusionBuff : EventDecorator
        {
            // Decorator
            public static new DecoratorId ID => new DecoratorId((int)Mutator.TechnoDecoratorIDs.DiffusionBuff);

            // EventDecorator
            public override void OnUpdate()
            {
                TechnoExt ext = Decorative as TechnoExt;
                var pThis = ext.OwnerObject;

                // 如果有累积伤害，则进行散射处理
                if (damageCounter > 0)
                {
                    int damageToScatter = (int)Math.Ceiling(damageCounter);
                    damageCounter = 0;

                    // 搜寻5格范围内除自己外的所有单位
                    var victimCrd = pThis.Ref.BaseAbstract.GetCoords();
                    List<Pointer<TechnoClass>> targetedUnits = new List<Pointer<TechnoClass>>();

                    foreach (var techno in TechnoClass.Array)
                    {
                        // 跳过自己
                        if (techno == pThis)
                            continue;

                        if (techno.Ref.Base.InLimbo)
                            continue;

                        // 计算水平距离（设置Z相同以便比较水平距离）
                        var technoCrd = techno.Ref.BaseAbstract.GetCoords();
                        technoCrd.Z = victimCrd.Z;
                        
                        // 检查距离是否在5格范围内（5格 = 1280 Lepton）
                        if (victimCrd.DistanceFrom(technoCrd) > 1280.0)
                            continue;

                        targetedUnits.Add(techno);
                    }

                    // 如果搜寻到单位
                    if (targetedUnits.Count > 0)
                    {
                        // 先为自身恢复等量生命值
                        var maxHealth = pThis.Ref.GetTechnoType().Ref.Base.Strength;
                        var recoveredHealth = Math.Min(damageToScatter, maxHealth - pThis.Ref.Base.Health);
                        pThis.Ref.Base.Health += recoveredHealth;

                        // 进行伤害分配
                        int selectedCount = Math.Max(1, damageToScatter / 10);

                        // 如果单位数量大于需要的数量，则随机选取
                        List<Pointer<TechnoClass>> selectedUnits = new List<Pointer<TechnoClass>>();
                        if (targetedUnits.Count > selectedCount)
                        {
                            // 从中随机选取selectedCount个单位
                            for (int i = 0; i < selectedCount; i++)
                            {
                                int randomIdx = ScenarioClass.Instance.Random.RandomRanged(0, targetedUnits.Count - 1);
                                var selected = targetedUnits[randomIdx];
                                selectedUnits.Add(selected);
                                targetedUnits.RemoveAt(randomIdx);
                            }
                        }
                        else
                        {
                            selectedUnits = targetedUnits;
                        }

                        // 将伤害平均分配给选中的单位
                        int damagePerUnit = damageToScatter / selectedUnits.Count;
                        foreach (var targetTechno in selectedUnits)
                        {
                            targetTechno.Ref.Base.ReceiveDamage(damagePerUnit, 0, DiffusionWH, pThis.Convert<ObjectClass>(), false, false, pThis.Ref.Base.Base.GetOwningHouse());
                        }
                    }
                }
            }

            public override DamageState AfterReceiveDamage(Pointer<int> pDamage, int DistanceFromEpicenter, Pointer<WarheadTypeClass> pWH,
                Pointer<ObjectClass> pAttacker, bool IgnoreDefenses, bool PreventPassengerEscape, Pointer<HouseClass> pAttackingHouse, DamageState result, int damageTaken)
            {
                TechnoExt victimExt = Decorative as TechnoExt;
                var victim = victimExt.OwnerObject;

                // 检查条件：
                // 1. 结果不是NowDead或PostMortem
                if (result == DamageState.NowDead || result == DamageState.PostMortem)
                    return result;

                // 2. 伤害大于0
                if (damageTaken <= 0)
                    return result;

                // 3. 伤害不无视防御，且弹头不是DiffusionWH
                if (IgnoreDefenses)
                    return result;

                if (pWH.IsNotNull && pWH == DiffusionWH)
                    return result;

                // 累积计数器，数值为受伤数值的50%
                damageCounter += (double)damageTaken / 2;

                return result;
            }

            // DiffusionBuff
            private double damageCounter = 0;

            public DiffusionBuff() { }
        }
    }
}
