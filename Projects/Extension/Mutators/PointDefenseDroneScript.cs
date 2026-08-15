using System;
using Extension.Script;
using Extension.Ext;
using Extension.Utilities;
using PatcherYRpp;

namespace Scripts
{
    [Serializable]
    public class PointDefenseDroneScript : TechnoScriptable
    {
        private int Energy = 200;
        private int UpdateCounter = 0;
        private readonly ColorStruct interceptLaserInnerColor = new ColorStruct(127, 127, 0);
        private readonly ColorStruct interceptLaserOuterColor = new ColorStruct(127, 127, 0);
        private readonly ColorStruct interceptLaserOuterSpread = new ColorStruct(0, 0, 0);
        private const int InterceptRange = 2048; // 8格

        public PointDefenseDroneScript(TechnoExt owner) : base(owner) { }

        public override void OnPut(CoordStruct coord, Direction faceDir)
        {
            Energy = 200;
            UpdateCounter = 0;
        }

        public override void OnUpdate()
        {
            // 每三帧检查一次拦截抛射体
            if (UpdateCounter == 0 && Energy >= 10)
            {
                var pThis = Owner.OwnerObject;
                foreach (var bullet in BulletClass.Array)
                {
                    if (bullet.IsNull)
                        continue;

                    // 距离、非隐身、且只有拦截敌方发射物
                    if (pThis.Ref.BaseAbstract.DistanceFrom(bullet.Convert<AbstractClass>()) <= InterceptRange
                        && bullet.Ref.Owner.IsNotNull
                        && !bullet.Ref.Type.Ref.Inviso)
                    {
                        var droneOwnerHouse = pThis.Ref.BaseAbstract.GetOwningHouse();
                        var bulletOwnerHouse = bullet.Ref.Owner.Ref.BaseAbstract.GetOwningHouse();

                        if (droneOwnerHouse.IsNotNull && !droneOwnerHouse.Ref.IsAlliedWith(bulletOwnerHouse))
                        {
                            var source = pThis.Ref.BaseAbstract.GetCoords();
                            var target = bullet.Ref.Base.Base.GetCoords();
                            YRMemory.Create<LaserDrawClass>(source, target, interceptLaserInnerColor, interceptLaserOuterColor, interceptLaserOuterSpread, 5);

                            Energy -= 10;
                            bullet.Ref.Explode();
                            bullet.Ref.Base.UnInit();

                            if (Energy < 10)
                                break;
                        }
                    }
                }
            }

            UpdateCounter++;
            UpdateCounter %= 3;
        }

        public override void OnRemove()
        {
            // 无需特殊清理
        }
    }
}
