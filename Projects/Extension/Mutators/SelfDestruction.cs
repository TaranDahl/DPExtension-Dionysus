using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicPatcher;
using Extension.Decorators;
using Extension.Ext;
using Extension.Utilities;
using PatcherYRpp;

namespace Extension.Mutators
{
    [Serializable]
    public class SelfDestruction : BuffMutator
    {
        // Mutator
        public override string UIName => "自毁程序";
        public override string Description => "敌方单位死亡时发生爆炸，并对附近的玩家单位造成伤害。";
        public override bool IsAvailableInRPG => true;
        public override int Score => 3;
        public SelfDestruction(Pointer<HouseClass> owner) : base(owner) { }

        public override void Init(bool isInitial = true)
        {
            base.Init();
            foreach (var techno in TechnoClass.Array)
            {
                if (!IsTechnoValid(techno))
                    continue;

                BuffTechno(techno);
            }
        }

        public override void Uninit()
        {
            foreach (var techno in TechnoClass.Array)
            {
                if (!IsTechnoValid(techno))
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
            if (ext.Get(SelfDestructionBuff.ID) == null)
                ext.CreateDecorator<SelfDestructionBuff>(SelfDestructionBuff.ID, "SelfDestructionBuff", this);
        }
        protected override void UnbuffTechno(Pointer<TechnoClass> techno)
        {
            var ext = TechnoExt.ExtMap.Find(techno);
            ext.Remove(SelfDestructionBuff.ID);
        }

        public override void OnTechnoCTOR(Pointer<TechnoClass> techno)
        {
            if (!IsTechnoValid(techno))
                return;

            BuffTechno(techno);
        }
        public override void OnTechnoChangeOwner(Pointer<TechnoClass> techno, Pointer<HouseClass> toHouse)
        {
            if (!IsTechnoOwnerValid(toHouse))
                UnbuffTechno(techno);
            else
                BuffTechno(techno);
        }

        // SelfDestruction
        private bool IsTechnoValid(Pointer<TechnoClass> techno)
        {
            if (techno.Ref.Base.Base.WhatAmI() == AbstractType.Building && !techno.Ref.Base.IsStrange())
                return false;

            var technoOwner = techno.Ref.Owner;
            return IsTechnoOwnerValid(technoOwner);
        }

        [Serializable]
        private class SelfDestructionBuff : EventDecorator
        {
            // Decorator
            public new static DecoratorId ID => new DecoratorId((int)TechnoDecoratorIDs.UniqueDecorator + 100);

            // EventDecorator
            public override DamageState AfterReceiveDamage(Pointer<int> pDamage, int DistanceFromEpicenter, Pointer<WarheadTypeClass> pWH,
                Pointer<ObjectClass> pAttacker, bool IgnoreDefenses, bool PreventPassengerEscape, Pointer<HouseClass> pAttackingHouse, DamageState result, int damageTaken)
            {
                // 只在单位死亡时触发
                if (result != DamageState.NowDead)
                    return result;

                // 因子失效了则不部署
                if (!myMutator.IsActive())
                    return result;

                TechnoExt victimExt = Decorative as TechnoExt;
                var victim = victimExt.OwnerObject;
                var deathCrd = victim.Ref.Base.Base.GetCoords();

                // 计算部署数量：GetLevel 值，英雄 +8
                uint level = GetLevel(victim.Ref.GetTechnoType());
                int deployCount = Math.Max((int)level, 1);
                if (IsHero(victim.Ref.GetTechnoType()))
                    deployCount += 8;

                // 在单位死亡位置下方地面附近128lepton半径的圆形范围内随机部署
                for (int i = 0; i < deployCount; i++)
                {
                    // 在半径64lepton内随机生成坐标
                    var randomAngle = ScenarioClass.Instance.Random.RandomDouble() * 2.0 * Math.PI;
                    var randomRadius = ScenarioClass.Instance.Random.RandomDouble() * 128.0;
                    int offsetX = (int)(Math.Cos(randomAngle) * randomRadius);
                    int offsetY = (int)(Math.Sin(randomAngle) * randomRadius);

                    var deployXY = new Point2D(deathCrd.X + offsetX, deathCrd.Y + offsetY);
                    var deployCoord = new CoordStruct(deployXY.X, deployXY.Y, 0);
                    var height = MapClass.Instance.GetCellAt(deployCoord).Ref.GetFloorHeight(deployXY);
                    deployCoord.Z = height;

                    // 创建定时引爆对象
                    var scenarioExt = ScenarioExt.Global();
                    scenarioExt.CreateDecorator<SelfDestructionCharger>(
                        scenarioExt.FetchScenarioDecoratorID,
                        "SelfDestructionCharger",
                        deployCoord,
                        myMutator);
                }

                return result;
            }

            // SelfDestructionBuff
            public SelfDestruction myMutator { get; private set; }

            public SelfDestructionBuff(SelfDestruction mutator)
            {
                myMutator = mutator;
            }
        }

        [Serializable]
        private class SelfDestructionCharger : EventRenderDecorator
        {
            // 常量
            private static int ChargeDuration => Mutator.TimeToFrame(0, 2); // 2秒
            private static readonly float IndicatorRadius = 1.5f - 0.5f; // 1.5格半径
            private static readonly ColorStruct IndicatorColor = new ColorStruct(255, 0, 0); // 红色

            private static Pointer<WeaponTypeClass> DetonationWeapon =>
                WeaponTypeClass.ABSTRACTTYPE_ARRAY.Find("SelfDestructionWeapon");

            private CoordStruct deployCoord;
            private SelfDestruction myMutator;
            private int timer = 0;
            private bool detonated = false;

            public SelfDestructionCharger(CoordStruct deployCoord, SelfDestruction myMutator)
            {
                this.deployCoord = deployCoord;
                this.myMutator = myMutator;
            }

            public override void OnUpdate()
            {
                // 检查因子是否仍然活跃
                if (!myMutator.IsActive())
                {
                    Decorative?.Remove(this);
                    return;
                }

                timer++;

                // 2秒后引爆
                if (!detonated && timer >= ChargeDuration)
                {
                    Detonate();
                    detonated = true;
                    Decorative?.Remove(this);
                }
            }

            public override void OnRender()
            {
                var targetCoord = deployCoord;

                // 始终绘制最外圈
                TacticalClass.DrawRadialIndicator(false, false, targetCoord, IndicatorColor, IndicatorRadius, false);

                //// 2秒内逐渐绘制指示器
                //int framesUntilDetonation = ChargeDuration - timer;

                //// 距离引爆时间不足1秒时，开始加深显示
                //if (framesUntilDetonation < Mutator.TimeToFrame(0, 1))
                //{
                //    int fadeElapsed = Mutator.TimeToFrame(0, 1) - framesUntilDetonation;
                //    int fadeRingCount = fadeElapsed / Mutator.TimeToFrame(0, 0.1);

                //    for (int i = 0; i < fadeRingCount; i++)
                //    {
                //        float radius = IndicatorRadius - 0.2f * (i + 1);
                //        if (radius <= 0.0f)
                //            break;

                //        TacticalClass.DrawRadialIndicator(false, false, targetCoord, IndicatorColor, radius, false);
                //    }
                //}
            }

            private void Detonate()
            {
                var weapon = DetonationWeapon;
                if (weapon.IsNull)
                    return;

                if (myMutator == null)
                    return;

                var firer = myMutator.GetRandomHouseOnOurSide();
                if (firer.IsNull)
                    return;

                // 在部署位置使用武器引爆
                var pCell = MapClass.Instance.GetCellAt(deployCoord);
                WeaponTypeExt.Detonate(
                    weapon,
                    deployCoord,
                    pCell.Convert<AbstractClass>(),
                    Pointer<TechnoClass>.Zero,
                    firer);
            }
        }
    }
}
