using DynamicPatcher;
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
    public class MineralShields : TargetCellMutator
    {
        // Mutator
        public override string UIName => "晶矿护盾";
        public override string Description => "玩家基地中的晶体矿簇会被周期性包覆一层护盾，必须将其摧毁才能继续采集资源。";
        public override bool IsAvailableInRPG => false;
        public override int Score => 2;
        public MineralShields(Pointer<HouseClass> owner) : base(owner) { }
        public override void Init(bool isInitial = true)
        {
            base.Init(isInitial);
            chance = isInitial ? 15 : 100;
            chance = 100;
        }
        public override bool Update()
        {
            if (!base.Update())
                return false;

            if (Game.CurrentFrame >= TimeToFrame(2, 40))
            {
                if (counter == 0)
                {
                    Pointer<HouseClass> owner = Pointer<HouseClass>.Zero;
                    foreach (var house in HouseClass.Array)
                    {
                        if (IsOnOurSide(house))
                        {
                            owner = house;
                            break;
                        }
                    }
                    if (owner.IsNotNull)
                    {
                        foreach (var tibTreeCell in TiberTreeCells)
                        {
                            // 一定概率
                            if (!ScenarioClass.Instance.Random.RandomChance(chance))
                                continue;

                            // 刷矿盾
                            var crd = tibTreeCell.Ref.Base.GetCoords();
                            var shield = Shield.Ref.BaseObjectType.CreateObject(owner);
                            Game.IKnowWhatImDoing++;
                            shield.Ref.Put(crd, DirType.N);
                            Game.IKnowWhatImDoing--;
                            var anim = YRMemory.Create<AnimClass>(ShieldAnim, crd);
                            anim.Ref.SetOwnerObject(shield);
                        }
                    }
                    // 每一波概率+5%
                    chance += 5;
                }
                counter++;
                counter %= TimeToFrame(2, 0);
            }
            return true;
        }

        // MineralShields
        private static Pointer<BuildingTypeClass> Shield => new Pointer<BuildingTypeClass>(BuildingTypeClass.ABSTRACTTYPE_ARRAY.Find("MINESHLD"));
        private static Pointer<AnimTypeClass> ShieldAnim => new Pointer<AnimTypeClass>(AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("MINESHLDANIM"));
        private int counter = 0;
        private int chance = 0;
    }
}
