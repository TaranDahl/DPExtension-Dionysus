using DynamicPatcher;
using Extension.Ext;
using Extension.Script;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Mutators
{
    [Serializable]
    public class MineShield : TechnoScriptable
    {
        public MineShield(TechnoExt owner) : base(owner) { }
        private TimerStruct lifeTimer = new TimerStruct();

        public override void OnUpdate()
        {
            if (!lifeTimer.IsTicking())
                lifeTimer.Start(Mutator.TimeToFrame(1, 0));
                
            if (lifeTimer.Completed())
                Owner.OwnerObject.Ref.Base.KillSelfByDamage(false);
        }

        public override void OnPut(CoordStruct coord, Direction faceDir)
        {
            var cell = MapClass.Instance.GetCellAt(coord);
            var terrain = cell.Ref.GetTerrain(false);
            if (terrain.IsNotNull && terrain.Ref.Type.Ref.SpawnsTiberium)
            {
                LogicClass.Instance.RemoveObject(terrain.Convert<ObjectClass>());
            }
            else
            {
                Logger.Log("Placing shield on cell with no tiber tree.");
            }
        }

        public override void OnRemove()
        {
            var coord = Owner.OwnerObject.Ref.BaseAbstract.GetCoords();
            var cell = MapClass.Instance.GetCellAt(coord);
            var terrain = cell.Ref.GetTerrain(false);
            if (terrain.IsNotNull && terrain.Ref.Type.Ref.SpawnsTiberium)
            {
                LogicClass.Instance.AddObject(terrain.Convert<ObjectClass>());
            }
            else
            {
                Logger.Log("Removing shield on cell with no tiber tree.");
            }
        }
    }
}
