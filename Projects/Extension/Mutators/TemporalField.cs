using DynamicPatcher;
using Extension.Decorators;
using Extension.Ext;
using Extension.Utilities;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Mutators
{
    [Serializable]
    public class TemporalField : TargetCellMutator
    {
        // Mutator
        public override string UIName => "时空力场";
        public override string Description => "地图上会周期性地部署敌人的时空力场。";
        public override bool IsAvailableInRPG => true;
        public override int Score => 1;
        public TemporalField(Pointer<HouseClass> owner) : base(owner) { }
        public override bool Update()
        {
            if (!base.Update())
                return false;

            counter++;
            counter %= TimeToFrame(0, 3);
            if (counter == 0)
            {
                var cell = SelectRandomCellOutOfSafeZone();
                if (cell.IsNotNull)
                {
                    var scenarioExt = ScenarioExt.Global();
                    scenarioExt.CreateDecorator<TemporalFieldShot>(
                        scenarioExt.FetchScenarioDecoratorID,
                        "TemporalFieldShot",
                        cell.Ref.MapCoords,
                        this);
                }
            }
            return true;
        }

        // TemporalField
        private int counter = 0;

        [Serializable]
        private class TemporalFieldShot : EventRenderDecorator
        {
            // 常量
            private static int ImpactDelay => Mutator.TimeToFrame(0, 1); // 1秒准备
            private static int Duration => Mutator.TimeToFrame(0, 20); // 持续20秒
            private static int DetonateInterval => Mutator.TimeToFrame(0, 0.125); // 每0.125秒引爆一次
            private static int RingInterval => Mutator.TimeToFrame(0, 0.2);
            private static readonly float InitialRadius = 60f;
            private static readonly float RadiusStep = 14f;
            private static readonly ColorStruct IndicatorColor = new ColorStruct(135, 206, 235); // 天蓝色

            private static Pointer<WeaponTypeClass> ShotWeapon =>
                WeaponTypeClass.ABSTRACTTYPE_ARRAY.Find("MutTemporalField");

            private static Pointer<AnimTypeClass> ImpactAnim =>
                AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("MutTemporalFieldAnim");

            private CellStruct targetCell;
            private TemporalField myMutator;
            private int timer = 0;

            public TemporalFieldShot(CellStruct targetCell, TemporalField myMutator)
            {
                this.targetCell = targetCell;
                this.myMutator = myMutator;
            }

            public override void OnUpdate()
            {
                if (timer == ImpactDelay)
                {
                    // 准备时间结束，创建动画
                    var pTargetCell = MapClass.Instance.GetCellAt(targetCell);
                    var targetCoord = pTargetCell.Ref.Base.GetCoords();
                    var animType = ImpactAnim;
                    if (animType.IsNotNull)
                    {
                        YRMemory.Create<AnimClass>(animType, targetCoord);
                    }
                }

                if (timer >= ImpactDelay)
                {
                    if (timer > ImpactDelay + Duration)
                    {
                        // 持续时间结束，移除装饰器
                        Decorative?.Remove(this);
                        return;
                    }

                    int activeTimer = timer - ImpactDelay;
                    if (activeTimer % DetonateInterval == 0)
                    {
                        DetonateWeapon();
                    }
                }

                timer++;
            }

            public override void OnRender()
            {
                // 仅在准备期间渲染指示器圈
                if (timer >= ImpactDelay)
                    return;

                int ringCount = RingInterval > 0 ? (timer / RingInterval + 1) : 1;
                var targetCoord = MapClass.Instance.GetCellAt(targetCell).Ref.Base.GetCoords();

                for (int i = 0; i < ringCount; i++)
                {
                    float radius = InitialRadius - i * RadiusStep;
                    if (radius <= 0.0f)
                        break;

                    TacticalClass.DrawRadialIndicator(
                        false,
                        false,
                        targetCoord,
                        IndicatorColor,
                        radius,
                        true);
                }
            }

            private void DetonateWeapon()
            {
                var pTargetCell = MapClass.Instance.GetCellAt(targetCell);
                var targetCoord = pTargetCell.Ref.Base.GetCoords();

                // 触发原地的武器引爆效果
                var weaponType = ShotWeapon;
                if (weaponType.IsNotNull)
                {
                    var firer = myMutator.GetRandomHouseOnOurSide();
                    WeaponTypeExt.Detonate(
                        weaponType, 
                        targetCoord, 
                        pTargetCell.Convert<AbstractClass>(), 
                        Pointer<TechnoClass>.Zero, 
                        firer);
                }
            }
        }
    }
}
