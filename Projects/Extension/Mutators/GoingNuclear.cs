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
    public class GoingNuclear : TargetCellMutator
    {
        // Mutator
        public override string UIName => "核弹打击";
        public override string Description => "核弹会随机在整张地图上进行发射。";
        public override bool IsAvailableInRPG => true;
        public override int Score => 3;
        public GoingNuclear(Pointer<HouseClass> owner) : base(owner) { }

        public override bool Update()
        {
            if (!base.Update())
                return false;

            counter++;
            counter %= TimeToFrame(0, 2);
            if (counter == 0)
            {
                var cell = SelectRandomCellOutOfSafeZone();
                if (cell.IsNotNull)
                {
                    var scenarioExt = ScenarioExt.Global();
                    scenarioExt.CreateDecorator<GoingNuclearShot>(
                        scenarioExt.FetchScenarioDecoratorID,
                        "GoingNuclearShot",
                        cell.Ref.MapCoords,
                        this);
                }
            }
            return true;
        }

        public override void Uninit()
        {
            base.Uninit();
        }

        // GoingNuclear
        private int counter = 0;

        [Serializable]
        private class GoingNuclearShot : EventRenderDecorator
        {
            // 常量
            private static int LaunchDelay => Mutator.TimeToFrame(0, 4);
            private static int RemoveDelay => Mutator.TimeToFrame(0, 5);
            private static int RingInterval => Mutator.TimeToFrame(0, 0.25);
            private static readonly float InitialRadius = 9.75f;
            private static readonly float RadiusStep = 0.5f;
            private static readonly ColorStruct IndicatorColor = new ColorStruct(255, 50, 50);
            private static readonly int SpawnHeight = 30 * 256; // 30格高度（leptons）

            private static Pointer<WeaponTypeClass> NukeWeapon =>
                WeaponTypeClass.ABSTRACTTYPE_ARRAY.Find("MutNuclear");

            private CellStruct targetCell;
            private GoingNuclear myMutator;
            private int timer = 0;
            private bool launched = false;

            public GoingNuclearShot(CellStruct targetCell, GoingNuclear myMutator)
            {
                this.targetCell = targetCell;
                this.myMutator = myMutator;
            }

            public override void OnUpdate()
            {
                if (!launched && timer >= LaunchDelay)
                {
                    Launch();
                    launched = true;
                }

                if (timer >= RemoveDelay)
                {
                    Decorative?.Remove(this);
                    return;
                }

                timer++;
            }

            public override void OnRender()
            {
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
                        radius);
                }
            }

            private void Launch()
            {
                var weapon = NukeWeapon;
                if (weapon.IsNull)
                {
                    Logger.Log("GoingNuclearShot: MutNuclear weapon not found!");
                    return;
                }

                var firer = myMutator.GetRandomHouseOnOurSide();
                if (firer.IsNull)
                {
                    Logger.Log("GoingNuclearShot: No valid house to fire!");
                    return;
                }

                var pTargetCell = MapClass.Instance.GetCellAt(targetCell);
                var targetCoord = pTargetCell.Ref.Base.GetCoords();
                var spawnCoord = new CoordStruct(targetCoord.X, targetCoord.Y, targetCoord.Z + SpawnHeight);

                WeaponTypeExt.SimpleFire(
                    weapon,
                    pTargetCell.Convert<AbstractClass>(),
                    Pointer<TechnoClass>.Zero,
                    firer,
                    spawnCoord);
            }
        }
    }
}
