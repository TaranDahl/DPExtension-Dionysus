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
    public class TimeWarp : TargetCellMutator
    {
        // Mutator
        public override string UIName => "时间扭曲";
        public override string Description => "地图上会周期性地部署敌人的时间扭曲。";
        public override bool IsAvailableInRPG => true;
        public override int Score => 1;
        public TimeWarp(Pointer<HouseClass> owner) : base(owner) { }
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
                    scenarioExt.CreateDecorator<TimeWarpShot>(
                        scenarioExt.FetchScenarioDecoratorID,
                        "TimeWarpShot",
                        cell.Ref.MapCoords,
                        this);
                }
            }
            return true;
        }

        // TimeWarp
        private int counter = 0;

        [Serializable]
        private class TimeWarpShot : EventRenderDecorator
        {
            // 常量
            private static int ImpactDelay => Mutator.TimeToFrame(0, 2.5);
            private static int Duration => Mutator.TimeToFrame(0, 30); // 持续30秒
            private static int DetonateInterval => Mutator.TimeToFrame(0, 0.125); // 每0.125秒引爆一次
            private static int RingInterval => Mutator.TimeToFrame(0, 0.5);
            private static readonly float InitialRadius = 84f;
            private static readonly float RadiusStep = 17f;
            private static readonly ColorStruct IndicatorColor = new ColorStruct(135, 206, 235); // 天蓝色

            private static Pointer<WeaponTypeClass> ShotWeapon =>
                WeaponTypeClass.ABSTRACTTYPE_ARRAY.Find("MutTimeWarp");

            private static Pointer<AnimTypeClass> ImpactAnim =>
                AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("MutTimeWarpAnim");

            private CellStruct targetCell;
            private TimeWarp myMutator;
            private int timer = 0;

            public TimeWarpShot(CellStruct targetCell, TimeWarp myMutator)
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
