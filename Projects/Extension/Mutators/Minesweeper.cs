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
    public class Minesweeper : TargetCellMutator
    {
        // 数值修改：
        // 雷区范围：8
        // Mutator
        public override string UIName => "扫雷专家";
        public override string Description => "数量庞大的寡妇雷和蜘蛛雷遍布整个战场。";
        public override bool IsAvailableInRPG => true;
        public override int Score => 6;
        public Minesweeper(Pointer<HouseClass> owner) : base(owner) { }
        public override void Init(bool isInitial = true)
        {
            base.Init(isInitial);
            // 初始范围是所有非安全区
            foreach (var cell in TargetCellMutator.NonSafeZoneCells)
                AvailableCellsCopy.Add(cell);
            // 生成雷区
            for (var i = 0; i != MineFieldCount && NonSafeZoneCells.Count > 0; ++i)
                SpawnMineField(ScenarioClass.GetRandomInList(AvailableCellsCopy).Ref.MapCoords);
        }

        // Minesweeper
        private static Pointer<TechnoTypeClass> WidowMine => TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find("WIDOWMINE");
        private static Pointer<TechnoTypeClass> SpiderMine => TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find("SPIDERMINE");
        private static int MineFieldRadius = 8;
        private static int MineFieldCapRange = 16;
        private static int WidowMineCountInOneField = 5;
        private static int SpiderMineCountInOneField = 7;
        private static int MineFieldCount = 25;
        private List<Pointer<CellClass>> AvailableCellsCopy = new List<Pointer<CellClass>>();
        private void SpawnMineField(CellStruct mapCrd)
        {
            var cellEnum = new CellSpreadEnumerator((uint)MineFieldRadius);
            var list = new List<CellStruct>();
            var house = GetRandomHouseOnOurSide();
            // 找到范围内可用的空地
            foreach (var offset in cellEnum)
            {
                var cellMapCrd = offset + mapCrd;
                var cell = MapClass.Instance.GetCellAt(CellClass.Cell2Coord(cellMapCrd));
                if (cell.Ref.GetBuilding().IsNull) // 没建筑
                    list.Add(cellMapCrd);
            }
            // 将周围16格标记为不可用
            cellEnum = new CellSpreadEnumerator((uint)MineFieldCapRange);
            foreach (var offset in cellEnum)
            {
                var cellMapCrd = offset + mapCrd;
                var cell = MapClass.Instance.GetCellAt(CellClass.Cell2Coord(cellMapCrd));
                AvailableCellsCopy.Remove(cell);
            }
            // 刷5个寡妇雷，7个蜘蛛雷
            var widowMineList = new List<Pointer<TechnoTypeClass>>();
            widowMineList.Add(WidowMine);
            for (var i = 0; i != WidowMineCountInOneField && list.Count > 0; ++i)
            {
                var index = ScenarioClass.Instance.Random.RandomRanged(0, list.Count - 1);
                CreateTeamAtCrd(Pointer<TeamTypeClass>.Zero, widowMineList, house, CellClass.Cell2Coord(list[index]), true, true);
                list.RemoveAt(index);
            }
            var spiderMineList = new List<Pointer<TechnoTypeClass>>();
            spiderMineList.Add(SpiderMine);
            for (var i = 0; i != SpiderMineCountInOneField && list.Count > 0; ++i)
            {
                var index = ScenarioClass.Instance.Random.RandomRanged(0, list.Count - 1);
                CreateTeamAtCrd(Pointer<TeamTypeClass>.Zero, spiderMineList, house, CellClass.Cell2Coord(list[index]), true, true);
                list.RemoveAt(index);
            }
        }
    }
}
