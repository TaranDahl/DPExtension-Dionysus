using Extension.Decorators;
using Extension.Ext;
using Extension.Utilities;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Mutators
{
    [Serializable]
    public class MutatorCacheManager
    {
        public static MutatorCacheManager Instance => ScenarioExt.Global().MutatorCacheManager;

        public List<Mutator> MutatorArray = new List<Mutator>();
        public List<Mutator> PreMutatorArray = new List<Mutator>();
        public List<Mutator> PostMutatorArray = new List<Mutator>();
        public List<SwizzleablePointer<CellClass>> TiberTreeCells = new List<SwizzleablePointer<CellClass>>();
        public List<SwizzleablePointer<CellClass>> UsableCells = new List<SwizzleablePointer<CellClass>>();
        public List<SwizzleablePointer<CellClass>> NonSafeZoneCells = new List<SwizzleablePointer<CellClass>>();
        public List<SwizzleablePointer<CellClass>> EdgeCells = new List<SwizzleablePointer<CellClass>>();
        public List<SwizzleablePointer<CellClass>> LeftEdgeCells = new List<SwizzleablePointer<CellClass>>();
        public List<SwizzleablePointer<CellClass>> UpEdgeCells = new List<SwizzleablePointer<CellClass>>();
        public List<SwizzleablePointer<CellClass>> RightEdgeCells = new List<SwizzleablePointer<CellClass>>();
        public List<SwizzleablePointer<CellClass>> DownEdgeCells = new List<SwizzleablePointer<CellClass>>();
        public List<List<List<string>>> TechnoLevelTable = new List<List<List<string>>>();
        public static List<string> NormalInfHeros = new List<string>
        {
            "TANY", "SIEG", "ARMR",
            "MORALES", "AIMORA", "VOLKOV", "CHITZ", "YUNRU",
            "ASSN", "UNDER", "LIBRA",
            "SIBFIN", "SICALI", "EUREKA", "URAGAN"
        };
        public static List<string> NormalVehHeros = new List<string>
        {
            "CNTR",
            "GOTTER",
            "MADAI", "BOID"
        };
        private void Clear()
        {
            MutatorArray.Clear();
            TiberTreeCells.Clear();
            UsableCells.Clear();
            NonSafeZoneCells.Clear();
            EdgeCells.Clear();
            LeftEdgeCells.Clear();
            UpEdgeCells.Clear();
            RightEdgeCells.Clear();
            DownEdgeCells.Clear();
            TechnoLevelTable.Clear();
        }
        public static void OnGameStart()
        {
            Instance.InitTechnoLevelTable();
        }
        public static void OnGameReload(IStream stream = null)
        {
            Instance.Clear();
            // TODO : 反序列化
        }
        public static void OnGameSave(IStream stream = null)
        {
            // TODO : 序列化
        }
        private void InitTechnoLevelTable()
        {
            // 先构建空表
            for (var rttiIdx = 0; rttiIdx < 3; rttiIdx++)// 建筑不统计
            {
                var rttiList = new List<List<string>>();
                for (var costIdx = 0; costIdx < 11; costIdx++)// 最后一个等级是英雄
                {
                    var costList = new List<string>();
                    rttiList.Add(costList);
                }
                Instance.TechnoLevelTable.Add(rttiList);
            }
            // 遍历Array并加入表中
            foreach (var type in TechnoTypeClass.ABSTRACTTYPE_ARRAY)
            {
                // 不可建造单位不在升格之链里
                if (!Mutator.IsBuildable(type))
                    continue;
                // 英雄单位手动添加，在此排除
                if (Mutator.IsHero(type))
                    continue;
                var rttiIdx = Mutator.GetRttiIdx(type);
                // 必须是FootTypeClass
                if (rttiIdx < 0 || rttiIdx > 2)
                    continue;
                var costIdx = Mutator.GetLevel(type);
                Instance.TechnoLevelTable[rttiIdx][(int)costIdx].Add(type.Ref.BaseAbstractType.ID);
            }
            // 加入英雄
            foreach (var name in NormalInfHeros)
                Instance.TechnoLevelTable[1][10].Add(name);
            foreach (var name in NormalVehHeros)
                Instance.TechnoLevelTable[2][10].Add(name);
        }
    }
}
