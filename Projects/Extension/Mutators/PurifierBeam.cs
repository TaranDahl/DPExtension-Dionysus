using DynamicPatcher;
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
    public class PurifierBeam : TargetCellMutator
    {
        // Mutator
        public override string UIName => "净化光束";
        public override string Description => "地图上会出现一道敌人的净化光束并朝附近的玩家单位移动。";
        public override bool IsAvailableInRPG => false;
        public override int Score => 2;
        public PurifierBeam(Pointer<HouseClass> owner) : base(owner) { }
        public override bool Update()
        {
            if (!base.Update())
                return false;

            if (counter == 0)
            {
                List<Pointer<HouseClass>> houses = new List<Pointer<HouseClass>>();
                foreach (var house in HouseClass.Array)
                {
                    if (IsOnOurSide(house))
                        houses.Add(house);
                }
                var houseToSpawn = houses[ScenarioClass.Instance.Random.RandomRanged(0, houses.Count - 1)];
                var cell = houseToSpawn.Ref.GetBaseCenter();
                var list = new List<Pointer<TechnoTypeClass>>();
                list.Add(Beam);
                var team = CreateTeamAtCrd(MutatorSpawnTeam, list, houseToSpawn, CellClass.Cell2Coord(cell), true);
                if (team.IsNotNull)
                {
                    var beam = team.Ref.FirstUnit;
                    var ext = TechnoExt.ExtMap.Find(beam);
                    var beamRef = new ExtensionReference<TechnoExt>(ext);
                    Beams.Add(beamRef);
                }
                else
                {
                    Logger.Log("Purifier beam creation failed!");
                }
            }

            counter++;
            counter %= TimeToFrame(10, 0);
            return true;
        }
        public override void Uninit()
        {
            base.Uninit();
            List<Pointer<TechnoClass>> beams = new List<Pointer<TechnoClass>>();
            foreach (var techno in TechnoClass.Array)
            {
                if (!IsOnOurSide(techno.Ref.BaseAbstract.GetOwningHouse()))
                    continue;

                if (techno.Ref.GetTechnoType() != Beam.Pointer)
                    continue;

                beams.Add(techno);
            }
            foreach (var techno in beams)
                techno.Ref.Base.KillSelfByDamage(false);
        }

        // PurifierBeam
        private static SwizzleablePointer<TechnoTypeClass> Beam => new SwizzleablePointer<TechnoTypeClass>(TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find("PRFBEAM"));
        private int counter = 0;
        public List<ExtensionReference<TechnoExt>> Beams = new List<ExtensionReference<TechnoExt>>();
    }
}
