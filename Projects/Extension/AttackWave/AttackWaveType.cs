using DynamicPatcher;
using Extension.Ext;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Extension.AttackWave.AttackWave;

namespace Extension.AttackWave
{
    [Serializable]
    public class AttackWaveType
    {
        private static List<int> CostTable = new List<int> { 300, 600, 1050, 1600, 2500, 3600, 4800 };
        public static List<AttackWaveType> Array => AttackWaveManager.Instance.TypeArray;

        [Serializable]
        public struct TypeRecordData
        {
            public String TypeName;
            public int Weight;
            public int MaxCount;
            public AttackWaveType.GetCostFunc GetCostFunc;
            public AttackWaveType.OnSpawnFunc OnSpawnFunc;

            public TypeRecordData(
                String typeName,
                int weight = 1,
                int maxCount = -1,
                AttackWaveType.GetCostFunc getCostFunc = null,
                AttackWaveType.OnSpawnFunc onSpawnFunc = null
                )
            {
                TypeName = typeName;
                Weight = weight;
                MaxCount = maxCount;
                GetCostFunc = getCostFunc;
                OnSpawnFunc = onSpawnFunc;
            }
        }

        [Serializable]
        private struct TypeData
        {
            public Pointer<TechnoTypeClass> Type;
            public int Weight;
            public int MaxCount;
            public int Cost;
            public AttackWaveType.OnSpawnFunc OnSpawnFunc;

            public TypeData(TypeRecordData record)
            {
                Type = TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find(record.TypeName);
                Weight = record.Weight;
                MaxCount = record.MaxCount;
                Cost = record.GetCostFunc == null ? Type.Ref.Cost : record.GetCostFunc(Type);
                OnSpawnFunc = record.OnSpawnFunc;
            }
        }

        public delegate int GetCostFunc(Pointer<TechnoTypeClass> type);
        public delegate void OnSpawnFunc(Pointer<TechnoClass> pThis);

        private static int GetPassengerCost(List<(String name, int count)> passengers)
        {
            var result = 0;
            foreach (var (name, count) in passengers)
            {
                var passengerType = TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find(name);
                result += passengerType.Ref.Cost * count;
            }
            return result;
        }
        private static void SpawnPassengers(Pointer<TechnoClass> pThis, List<(String name, int count)> passengers)
        {
            var pType = pThis.Ref.GetTechnoType();
            foreach (var (name, count) in passengers)
            {
                var passengerType = TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find(name);
                for (int i = 0; i < count; i++)
                {
                    var passenger = passengerType.Ref.Base.CreateObject(pThis.Ref.BaseAbstract.GetOwningHouse()).Convert<FootClass>();
                    pThis.Ref.AddPassenger(passenger);
                    passenger.Ref.Base.transporter = pThis;

                    if (pType.Ref.OpenTopped)
                        passenger.Ref.Base.EnteredOpenTopped(pThis);
                }
            }
        }

        // 大兵IFV
        public static int E1PassengerCost(Pointer<TechnoTypeClass> type)
        {
            var passengerList = new List<(String name, int count)>()
            {
                ("E1", 1),
            };
            return type.Ref.Cost + GetPassengerCost(passengerList);
        }
        public static void E1PassengerOnSpawn(Pointer<TechnoClass> pThis)
        {
            var passengerList = new List<(String name, int count)>()
            {
                ("E1", 1),
            };
            SpawnPassengers(pThis, passengerList);
        }

        // GGIIFV、GGI长剑
        public static int GGIPassengerCost(Pointer<TechnoTypeClass> type)
        {
            var passengerList = new List<(String name, int count)>()
            {
                ("GGI", 1),
            };
            return type.Ref.Cost + GetPassengerCost(passengerList);
        }
        public static void GGIPassengerOnSpawn(Pointer<TechnoClass> pThis)
        {
            var passengerList = new List<(String name, int count)>()
            {
                ("GGI", 1),
            };
            SpawnPassengers(pThis, passengerList);
        }

        // 2GGI2光棱兵要塞
        public static int BFRTCost(Pointer<TechnoTypeClass> type)
        {
            var passengerList = new List<(String name, int count)>()
            {
                ("GGI", 2),
                ("ENFO", 2),
            };
            return type.Ref.Cost + GetPassengerCost(passengerList);
        }
        public static void BFRTOnSpawn(Pointer<TechnoClass> pThis)
        {
            var passengerList = new List<(String name, int count)>()
            {
                ("GGI", 2),
                ("ENFO", 2),
            };
            SpawnPassengers(pThis, passengerList);
        }

        // 喷火灾厄
        public static int FLAMERPassengerCost(Pointer<TechnoTypeClass> type)
        {
            var passengerList = new List<(String name, int count)>()
            {
                ("FLAMER", 1),
            };
            return type.Ref.Cost + GetPassengerCost(passengerList);
        }
        public static void FLAMERPassengerOnSpawn(Pointer<TechnoClass> pThis)
        {
            var passengerList = new List<(String name, int count)>()
            {
                ("FLAMER", 1),
            };
            SpawnPassengers(pThis, passengerList);
        }

        public string UIName;
        public string UIDescription;
        public int SideIdx;
        public List<List<TypeRecordData>> TypeDataTable;

        public static int GetBudgetCost(int amountLevel, int houseCount, double amountMultiplier = 1.0)
        {
            houseCount = Math.Max(houseCount, AttackWaveManager.Instance.HouseCountMax);
            return ScenarioExt.ExchangeCurrency((int)(amountMultiplier * CostTable[amountLevel - 1] * houseCount));
        }

        public List<Pointer<TechnoTypeClass>> GetStandardList(int techLevel, int costTotal, int houseCount)
        {
            var result = new List<Pointer<TechnoTypeClass>>();
            var records = TypeDataTable[techLevel - 1];
            var unlimitedDataList = new List<TypeData>();
            var limitedDataList = new List<TypeData>();

            foreach (var record in records)
            {
                var data = new TypeData(record);

                if (data.MaxCount > 0)
                    limitedDataList.Add(data);
                else
                    unlimitedDataList.Add(data);
            }

            var weightTotal = records.Sum(td => td.Weight);
            var costPerWeight = (double)costTotal / weightTotal;

            var costRemain = costTotal;
            var weightRemain = weightTotal;

            foreach (var typeData in limitedDataList)
            {
                var budget = typeData.Weight * costPerWeight;
                costRemain -= (int)budget;
                weightRemain -= typeData.Weight;
                var limitCount = typeData.MaxCount * houseCount;
                var cost = typeData.Cost;

                for (int i = 0; i < limitCount && budget >= cost; i++, budget -= cost)
                    result.Add(typeData.Type);

                costRemain += (int)budget;
            }

            costPerWeight = weightRemain > 0 ? (double)costRemain / weightRemain : 0;

            foreach (var typeData in unlimitedDataList)
            {
                var budget = typeData.Weight * costPerWeight;
                var cost = typeData.Cost;

                for (; budget > 0; budget -= cost)
                    result.Add(typeData.Type);
            }

            return result;
        }

        public AttackWave SpawnAt(Pointer<HouseClass> spawner, List<AttackWaveScriptNode> script, int houseCount, double amountMultiplier, CoordStruct crd, int techLevel, int amountLevel)
        {
            var budget = GetBudgetCost(amountLevel, houseCount, amountMultiplier);
            var standardList = GetStandardList(techLevel, budget, houseCount);
            var members = new List<Pointer<TechnoClass>>();

            foreach (var type in standardList)
            {
                var techno = type.Ref.Base.CreateObject(spawner).Convert<TechnoClass>();
                members.Add(techno);
                techno.Ref.Base.Remove();
            }

            var result = new AttackWave(crd, script, members, SideIdx);
            return result;
        }
    } //OK

}
