using DynamicPatcher;
using PatcherYRpp;
using System;

namespace Extension.Mutators
{
    [Serializable]
    public class Darkness : Mutator
    {
        public override string UIName => "暗无天日";
        public override string Description => "先前探索过的区域若离开了玩家的视野范围将会重新变成一片黑色。";
        public override bool IsAvailableInRPG => true;
        public override int Score => 2;

        private int counter = 0;
        private static int ReshroudInterval => Math.Max(1, TimeToFrame(0, 1));

        public Darkness(Pointer<HouseClass> owner) : base(owner) { }

        public override bool Update()
        {
            if (!base.Update())
                return false;

            counter++;
            counter %= ReshroudInterval;
            if (counter == 0)
            {
                var player = HouseClass.Player;
                if (player.IsNotNull && IsOnTheirSide(player))
                    MapClass.Instance.Reshroud(player);
            }

            return true;
        }

        public static bool ShouldHideMapEvent(CellStruct mapCrd)
        {
            var darkness = FindActiveDarkness();
            if (darkness == null)
                return false;

            var player = HouseClass.Player;
            if (player.IsNull || !darkness.IsOnTheirSide(player))
                return false;

            return MapClass.Instance.IsLocationShrouded(MapClass.Instance.GetCellAt(mapCrd).Ref.Base.GetCoords());
        }

        private static Darkness FindActiveDarkness()
        {
            foreach (var mutator in Array)
            {
                if (mutator is Darkness darkness)
                    return darkness;
            }

            return null;
        }
    }
}
