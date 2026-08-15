using DynamicPatcher;
using Extension.Decorators;
using Extension.Ext;
using Extension.Script;
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
    public class Magnificent : TargetCellMutator
    {
        // Mutator
        public override string UIName => "强磁雷场";
        public override string Description => "麦格天雷会在任务一开始布满整个地图。";
        public override bool IsAvailableInRPG => true;
        public override int Score => 4;
        public Magnificent(Pointer<HouseClass> owner) : base(owner) { }
        public override void Init(bool isInitial = true)
        {
            base.Init(isInitial);
            // 生成雷区
            for (var i = 0; i != MagMineCount; ++i)
            {
                var house = GetRandomHouseOnOurSide();
                var crd = SelectRandomCellOutOfSafeZone().Ref.Base.GetCoords();
                var mine = TechnoExt.CreateObjectWithDecorator<MagMineScript>(MagMine.Convert<ObjectTypeClass>(), house, MagMineScript.ID);
                var dir = (DirType)(ScenarioClass.Instance.Random.RandomRanged(0, 7) * 32);
                Game.IKnowWhatImDoing++;
                mine.Ref.Put(crd, dir);
                Game.IKnowWhatImDoing--;
                mine.Convert<TechnoClass>().Ref.Base.Scatter(crd, false, false);
            }
        }

        // Minesweeper
        private static Pointer<TechnoTypeClass> MagMine => TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find("MAGMINE");
        private static int MagMineCount = 200;


        [Serializable]
        public class MagMineScript : EventDecorator
        {
            // Decorator
            public static DecoratorId ID => new DecoratorId((int)TechnoDecoratorIDs.UniqueDecorator);

            // MagMineScript
            private static int MineHeight = 800;
            private static int ScanRange = 5;
            private static int FireRange = 15;
            private static int FireDelay => Mutator.TimeToFrame(0, 2.5);
            private static ColorStruct LineColor = new ColorStruct(255, 0, 0);
            private Pointer<AnimTypeClass> MineTimerAnim => AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("MAGTIMER");
            private Pointer<WeaponTypeClass> MineWeapon => WeaponTypeClass.ABSTRACTTYPE_ARRAY.Find("MagMineStrike");
            private SwizzleablePointer<CellClass> Target = new SwizzleablePointer<CellClass>(Pointer<CellClass>.Zero);
            private int FireCounter = 0;

            private bool TrySetTargetToFire(Pointer<AbstractClass> pTarget)
            {
                var crd = pTarget.Ref.GetCoords();
                var crdThis = (Decorative as TechnoExt).OwnerObject.Ref.BaseAbstract.GetCoords();
                var deltaCrd = crd - crdThis;
                deltaCrd.Z = 0;
                var crdTarget = crdThis + deltaCrd * ((double)(FireRange * 256) / Math.Max(deltaCrd.Magnitude(), 1)); // 防止除0
                var cell = MapClass.Instance.GetCellAt(crdTarget);
                if (cell.Ref.MapCoords != CellStruct.Empty)
                {

                    Target = new SwizzleablePointer<CellClass>(cell.Convert<CellClass>());
                    return true;
                }
                Logger.Log("Invalid target cell!");
                return false;
            }

            public override void OnUpdate()
            {
                if (Target.Pointer.IsNotNull)
                {
                    // 更新激光
                    var pThis = (Decorative as TechnoExt).OwnerObject;
                    var crd = pThis.Ref.BaseAbstract.GetCoords();
                    var targetCrd = Target.Ref.Base.GetCoords();
                    targetCrd.Z += MineHeight;
                    // 无效，绘制时机不对
                    // Pointer<TechnoClass>.Zero.Ref.DrawLine(crd, targetCrd, LineColor, false); 
                    // 瞄准完毕，发射
                    if (FireCounter >= FireDelay)
                    {
                       //发射弹药
                       var bullet = BulletExt.CreateBulletWithDecorator<MagMineBullet>(MineWeapon.Ref.Projectile,
                           Target.Pointer.Convert<AbstractClass>(), pThis, MineWeapon,
                           MagMineBullet.ID,
                           Target);
                        bullet.Ref.Base.Remove();
                        bullet.Ref.MoveTo(crd);
                        VocClass.PlayAt(ScenarioClass.GetRandomInDVC(MineWeapon.Ref.Report), crd);
                        //var bullet = pThis.Ref.Fire(Target.Pointer.Convert<AbstractClass>(), 1);
                        //var bulletExt = BulletExt.ExtMap.Find(bullet);
                        // 自己消失
                        pThis.Ref.Base.Vanish(Pointer<TechnoClass>.Zero);
                    }
                    FireCounter++;
                }
            }

            public override void OnFire(Pointer<AbstractClass> pTarget, int weaponIndex)
            {
                if (Target.IsNull)
                {
                    if (TrySetTargetToFire(pTarget))
                    {
                        // 设置目标成功
                        var crd = (Decorative as TechnoExt).OwnerObject.Ref.BaseAbstract.GetCoords();
                        // 创建动画
                        var anim = YRMemory.Create<AnimClass>(MineTimerAnim, crd);
                        anim.Ref.SetOwnerObject((Decorative as TechnoExt).OwnerObject.Convert<ObjectClass>());
                    }
                }
            }
        }

        [Serializable]
        public class MagMineBullet : EventDecorator
        {
            // Decorator
            public static DecoratorId ID => new DecoratorId((int)TechnoDecoratorIDs.UniqueDecorator);
            
            // MagMineBulletScript
            private static int MineHeight = 800;
            private static ColorStruct LineColor = new ColorStruct(255, 0, 0);
            // 均于发射时由本体设置
            public SwizzleablePointer<CellClass> Target = new SwizzleablePointer<CellClass>(Pointer<CellClass>.Zero);
            //public SwizzleablePointer<TechnoClass> MyMine = new SwizzleablePointer<TechnoClass>(Pointer<TechnoClass>.Zero);

            public MagMineBullet(SwizzleablePointer<CellClass> target)
            {
                Target = target;
                //MyMine = myMine;
            }

            public override void OnUpdate()
            {
                // 更新激光
                var crd = (Decorative as BulletExt).OwnerObject.Ref.Base.Base.GetCoords();
                var targetCrd = Target.Ref.Base.GetCoords();
                targetCrd.Z += MineHeight;
                // 无效，绘制时机不对
                // Pointer<TechnoClass>.Zero.Ref.DrawLine(crd, targetCrd, LineColor, false);
            }
        }
    }
}
