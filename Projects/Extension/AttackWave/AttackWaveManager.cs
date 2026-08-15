using DynamicPatcher;
using Extension.Ext;
using Extension.Mutators;
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
    public class AttackWaveManager // : ScenarioDecorator
    {
        public static AttackWaveManager Instance => ScenarioExt.Global().AttackWaveManager;

        [Serializable]
        public struct AttackWaveScheduleNode
        {
            // 时间
            public int SpawningFrame;
            public int RandomDelayFrame;
            public int LoopCount;
            public int LoopDelay;

            // 单位
            public int TechLevel;
            public int AmountLevel;
            public List<AttackWaveScriptNode> Script;
            public List<AttackWaveType.TypeRecordData> AdditionalUnits;
            public AttackWaveType ForecedType;

            // 地点
            public SpawningCrdGetter SelectSpawningCrdFunc;

            // 所属方
            public int ForcedHouseIdx;

            [Serializable]
            public delegate CoordStruct SpawningCrdGetter(Pointer<HouseClass> owner, Pointer<AbstractClass> target);

            // 在所属方基地附近生成
            private static CoordStruct DefaultSpawningCrdGetter(Pointer<HouseClass> owner, Pointer<AbstractClass> target)
            {
                return CellClass.Cell2Coord(owner.Ref.GetBaseCenter());
            }

            private static int GetDefaultLevelByTime(int frame)
            {
                return Math.Min(Math.Max(frame / ScenarioExt.TimeToFrame(3, 0), 1), 7);
            }

            public AttackWaveScheduleNode
                (
                int spawningFrame,
                int loopCount = 1,
                int loopDelay = 15,
                int techLevel = 0,
                int amountLevel = 1,
                int randomDelayFrame = 1,
                List<AttackWaveScriptNode> script = null,
                List<AttackWaveType.TypeRecordData> additionalUnits = null,
                AttackWaveType forcedType = null,
                SpawningCrdGetter selectSpawningCrdFunc = null,
                int houseIdx = -1
                )
            {
                SpawningFrame = spawningFrame;
                RandomDelayFrame = randomDelayFrame;
                SelectSpawningCrdFunc = selectSpawningCrdFunc;
                TechLevel = techLevel >= 0 ? techLevel : GetDefaultLevelByTime(spawningFrame);
                AmountLevel = amountLevel >= 0 ? amountLevel : GetDefaultLevelByTime(spawningFrame);
                Script = script ?? new List<AttackWaveScriptNode>();
                AdditionalUnits = additionalUnits ?? new List<AttackWaveType.TypeRecordData>();
                LoopCount = loopCount;
                LoopDelay = loopDelay;
                ForecedType = forcedType;
                SelectSpawningCrdFunc = selectSpawningCrdFunc ?? DefaultSpawningCrdGetter;
                ForcedHouseIdx = houseIdx;
            }
        }

        public /*override*/ void LoadFromINI()
        {
            // 读取进攻波次时间表
            // TODO : LoadFromINI
            if (false)
            {

            }
            else
            {
                Schedule = new List<AttackWaveScheduleNode>()
                {
                    new AttackWaveScheduleNode(
                        spawningFrame: ScenarioExt.TimeToFrame(3, 0),
                        loopCount: -1,
                        loopDelay: ScenarioExt.TimeToFrame(3, 0),
                        script: AttackWaveScriptNode.DefaultScript(),
                        techLevel: 1,
                        amountLevel: 1
                    ),
                };
            }

            // 读取进攻波次类型
            // TODO : LoadFromINI
            if (false)
            {

            }
            else
            {
                TypeArray = new List<AttackWaveType>()
                {
                    new AttackWaveType()
                    {
                        UIName = "美国 空军",
                        UIDescription = "这支美军擅长从空中发动奇袭。",
                        SideIdx = 0,
                        TypeDataTable = new List<List<AttackWaveType.TypeRecordData>>()
                        {
                            // Tech Level 1
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("E1"),
                            },
                            // Tech Level 2
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("E1"),
                                new AttackWaveType.TypeRecordData("ENFO"),
                            },
                            // Tech Level 3
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("E1"),
                                new AttackWaveType.TypeRecordData("JUMPJET"),
                            },
                            // Tech Level 4
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("E1"),
                                new AttackWaveType.TypeRecordData("JUMPJET"),
                                new AttackWaveType.TypeRecordData("ENFO"),
                            },
                            // Tech Level 5
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("E1"),
                                new AttackWaveType.TypeRecordData("JUMPJET"),
                                new AttackWaveType.TypeRecordData("ENFO"),
                                new AttackWaveType.TypeRecordData("COMA"),
                            },
                            // Tech Level 6
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("JUMPJET"),
                                new AttackWaveType.TypeRecordData("ENFO"),
                                new AttackWaveType.TypeRecordData("COMA"),
                                new AttackWaveType.TypeRecordData("STORM"),
                            },
                            // Tech Level 7
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("JUMPJET"),
                                new AttackWaveType.TypeRecordData("ENFO"),
                                new AttackWaveType.TypeRecordData("COMA"),
                                new AttackWaveType.TypeRecordData("STORM"),
                                new AttackWaveType.TypeRecordData("FORTRESS"),
                            },
                        }
                    }, // 美国 空军
                    new AttackWaveType()
                    {
                        UIName = "欧洲联盟 空军",
                        UIDescription = "欧洲联盟的工程学和超时空科技最终孕育出雷神炮艇。",
                        SideIdx = 0,
                        TypeDataTable = new List<List<AttackWaveType.TypeRecordData>>()
                        {
                            // Tech Level 1
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("MTNK"),
                            },
                            // Tech Level 2
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("MTNK"),
                                new AttackWaveType.TypeRecordData("E1"),
                            },
                            // Tech Level 3
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("MTNK"),
                                new AttackWaveType.TypeRecordData("E1"),
                                new AttackWaveType.TypeRecordData("JUMPJET"),
                            },
                            // Tech Level 4
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("MTNK"),
                                new AttackWaveType.TypeRecordData("JUMPJET"),
                            },
                            // Tech Level 5
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("JUMPJET"),
                                new AttackWaveType.TypeRecordData("THOR"),
                            },
                            // Tech Level 6
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("THOR"),
                                new AttackWaveType.TypeRecordData("HBIRD", maxCount: 2),
                                new AttackWaveType.TypeRecordData("FORTRESS"),
                            },
                            // Tech Level 7
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("JUMPJET"),
                                new AttackWaveType.TypeRecordData("THOR"),
                                new AttackWaveType.TypeRecordData("HBIRD", maxCount: 2),
                                new AttackWaveType.TypeRecordData("FORTRESS"),
                            },
                        }
                    }, // 欧洲联盟 空军
                    new AttackWaveType()
                    {
                        UIName = "美国 陆军",
                        UIDescription = "美国的陆军兼具火力、速度与装甲。",
                        SideIdx = 0,
                        TypeDataTable = new List<List<AttackWaveType.TypeRecordData>>()
                        {
                            // Tech Level 1
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("ETNK"),
                            },
                            // Tech Level 2
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("ETNK"),
                                new AttackWaveType.TypeRecordData("FV", getCostFunc : AttackWaveType.E1PassengerCost, onSpawnFunc : AttackWaveType.E1PassengerOnSpawn),
                            },
                            // Tech Level 3
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("FV", getCostFunc : AttackWaveType.E1PassengerCost, onSpawnFunc : AttackWaveType.E1PassengerOnSpawn),
                                new AttackWaveType.TypeRecordData("FV", getCostFunc : AttackWaveType.GGIPassengerCost, onSpawnFunc : AttackWaveType.GGIPassengerOnSpawn),
                            },
                            // Tech Level 4
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("ETNK"),
                                new AttackWaveType.TypeRecordData("FV", getCostFunc : AttackWaveType.E1PassengerCost, onSpawnFunc : AttackWaveType.E1PassengerOnSpawn),
                                new AttackWaveType.TypeRecordData("FV", getCostFunc : AttackWaveType.GGIPassengerCost, onSpawnFunc : AttackWaveType.GGIPassengerOnSpawn),
                            },
                            // Tech Level 5
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("ABRM"),
                            },
                            // Tech Level 6
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("ABRM"),
                                new AttackWaveType.TypeRecordData("FV", getCostFunc : AttackWaveType.GGIPassengerCost, onSpawnFunc : AttackWaveType.GGIPassengerOnSpawn),
                            },
                            // Tech Level 7
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("AERO"),
                                new AttackWaveType.TypeRecordData("ABRM"),
                                new AttackWaveType.TypeRecordData("BASS"),
                            },
                        }
                    }, // 美国 陆军
                    new AttackWaveType()
                    {
                        UIName = "太平洋阵线 陆军",
                        UIDescription = "太平洋阵线的陆军由坚固的钢铁要塞和冷冻火炮构成。",
                        SideIdx = 0,
                        TypeDataTable = new List<List<AttackWaveType.TypeRecordData>>()
                        {
                            // Tech Level 1
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("TENGU", getCostFunc : AttackWaveType.GGIPassengerCost, onSpawnFunc : AttackWaveType.GGIPassengerOnSpawn),
                            },
                            // Tech Level 2
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("TENGU", getCostFunc : AttackWaveType.GGIPassengerCost, onSpawnFunc : AttackWaveType.GGIPassengerOnSpawn),
                                new AttackWaveType.TypeRecordData("E1"),
                            },
                            // Tech Level 3
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("TENGU", getCostFunc : AttackWaveType.GGIPassengerCost, onSpawnFunc : AttackWaveType.GGIPassengerOnSpawn),
                                new AttackWaveType.TypeRecordData("ENFO"),
                            },
                            // Tech Level 4
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("TENGU", getCostFunc : AttackWaveType.GGIPassengerCost, onSpawnFunc : AttackWaveType.GGIPassengerOnSpawn),
                                new AttackWaveType.TypeRecordData("ENFO"),
                                new AttackWaveType.TypeRecordData("HOWI"),
                            },
                            // Tech Level 5
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("BFRT", getCostFunc : AttackWaveType.BFRTCost, onSpawnFunc : AttackWaveType.BFRTOnSpawn),
                                new AttackWaveType.TypeRecordData("HOWI"),
                            },
                            // Tech Level 6
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("BFRT", getCostFunc : AttackWaveType.BFRTCost, onSpawnFunc : AttackWaveType.BFRTOnSpawn),
                                new AttackWaveType.TypeRecordData("HOWI"),
                                new AttackWaveType.TypeRecordData("BLZZ"),
                            },
                            // Tech Level 7
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("BFRT", getCostFunc : AttackWaveType.BFRTCost, onSpawnFunc : AttackWaveType.BFRTOnSpawn),
                                new AttackWaveType.TypeRecordData("BLZZ"),
                                new AttackWaveType.TypeRecordData("VCARR"),
                            },
                        }
                    }, // 太平洋阵线 陆军
                    new AttackWaveType()
                    {
                        UIName = "苏俄 陆军",
                        UIDescription = "苏俄使用磁暴科技武装步兵载具混合的陆军。",
                        SideIdx = 1,
                        TypeDataTable = new List<List<AttackWaveType.TypeRecordData>>()
                        {
                            // Tech Level 1
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("E2"),
                            },
                            // Tech Level 2
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("E2"),
                                new AttackWaveType.TypeRecordData("SHOCK"),
                            },
                            // Tech Level 3
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("SHOCK"),
                                new AttackWaveType.TypeRecordData("SCAR"),
                            },
                            // Tech Level 4
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("SCAR"),
                                new AttackWaveType.TypeRecordData("TTNK"),
                            },
                            // Tech Level 5
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("E2"),
                                new AttackWaveType.TypeRecordData("SHOCK"),
                                new AttackWaveType.TypeRecordData("TTNK"),
                            },
                            // Tech Level 6
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("SHOCK"),
                                new AttackWaveType.TypeRecordData("SCAR"),
                                new AttackWaveType.TypeRecordData("TTNK"),
                            },
                            // Tech Level 7
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("SHOCK"),
                                new AttackWaveType.TypeRecordData("SCAR"),
                                new AttackWaveType.TypeRecordData("TTNK"),
                                new AttackWaveType.TypeRecordData("WOLF"),
                            },
                        }
                    }, // 苏俄 陆军
                    new AttackWaveType()
                    {
                        UIName = "拉丁同盟 陆军",
                        UIDescription = "拉丁同盟的喷火器和高速坦克简陋但有效。",
                        SideIdx = 1,
                        TypeDataTable = new List<List<AttackWaveType.TypeRecordData>>()
                        {
                            // Tech Level 1
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("FLAKT"),
                            },
                            // Tech Level 2
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("FLAKT"),
                                new AttackWaveType.TypeRecordData("FLAMER"),
                            },
                            // Tech Level 3
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("FLAKT"),
                                new AttackWaveType.TypeRecordData("FLAMER"),
                                new AttackWaveType.TypeRecordData("JTNK"),
                            },
                            // Tech Level 4
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("HTK"),
                                new AttackWaveType.TypeRecordData("BOREK"),
                                new AttackWaveType.TypeRecordData("JTNK"),
                            },
                            // Tech Level 5
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("HTK"),
                                new AttackWaveType.TypeRecordData("BOREK"),
                                new AttackWaveType.TypeRecordData("APOC", getCostFunc : AttackWaveType.FLAMERPassengerCost, onSpawnFunc : AttackWaveType.FLAMERPassengerOnSpawn),
                            },
                            // Tech Level 6
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("BOREK"),
                                new AttackWaveType.TypeRecordData("APOC", getCostFunc : AttackWaveType.FLAMERPassengerCost, onSpawnFunc : AttackWaveType.FLAMERPassengerOnSpawn),
                            },
                            // Tech Level 7
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("BOREK"),
                                new AttackWaveType.TypeRecordData("APOC", getCostFunc : AttackWaveType.FLAMERPassengerCost, onSpawnFunc : AttackWaveType.FLAMERPassengerOnSpawn),
                                new AttackWaveType.TypeRecordData("BURA"),
                            },
                        }
                    }, // 拉丁同盟 陆军
                    new AttackWaveType()
                    {
                        UIName = "中国 步兵",
                        UIDescription = "中国的步兵团在前线修建野战工事辅助进攻。",
                        SideIdx = 1,
                        TypeDataTable = new List<List<AttackWaveType.TypeRecordData>>()
                        {
                            // Tech Level 1
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("E2"),
                            },
                            // Tech Level 2
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("E2"),
                                new AttackWaveType.TypeRecordData("FLAKT"),
                            },
                            // Tech Level 3
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("E2"),
                                new AttackWaveType.TypeRecordData("FLAKT"),
                                new AttackWaveType.TypeRecordData("SENGINEER", maxCount : 4),
                            },
                            // Tech Level 4
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("FLAKT"),
                                new AttackWaveType.TypeRecordData("SENGINEER", maxCount : 4),
                                new AttackWaveType.TypeRecordData("HARV"),
                            },
                            // Tech Level 5
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("FLAKT"),
                                new AttackWaveType.TypeRecordData("SENGINEER", maxCount : 4),
                                new AttackWaveType.TypeRecordData("HARV"),
                                new AttackWaveType.TypeRecordData("CTNK"),
                            },
                            // Tech Level 6
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("HARV"),
                                new AttackWaveType.TypeRecordData("CTNK"),
                                new AttackWaveType.TypeRecordData("SENT"),
                            },
                            // Tech Level 7
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("HARV"),
                                new AttackWaveType.TypeRecordData("CTNK"),
                                new AttackWaveType.TypeRecordData("SENT"),
                                new AttackWaveType.TypeRecordData("DESOR"),
                            },
                        }
                    }, // 中国 步兵
                    new AttackWaveType()
                    {
                        UIName = "拉丁同盟 混合",
                        UIDescription = "小心！这支拉丁同盟部队装满了炸药！",
                        SideIdx = 1,
                        TypeDataTable = new List<List<AttackWaveType.TypeRecordData>>()
                        {
                            // Tech Level 1
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("E2"),
                            },
                            // Tech Level 2
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("E2"),
                                new AttackWaveType.TypeRecordData("BGGY"),
                            },
                            // Tech Level 3
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("E2"),
                                new AttackWaveType.TypeRecordData("BGGY"),
                                new AttackWaveType.TypeRecordData("HTK"),
                            },
                            // Tech Level 4
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("E2"),
                                new AttackWaveType.TypeRecordData("HTK"),
                                new AttackWaveType.TypeRecordData("MOTOR"),
                                new AttackWaveType.TypeRecordData("DUST", maxCount : 2),
                            },
                            // Tech Level 5
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("E2"),
                                new AttackWaveType.TypeRecordData("BGGY"),
                                new AttackWaveType.TypeRecordData("MOTOR"),
                                new AttackWaveType.TypeRecordData("FOX"),
                            },
                            // Tech Level 6
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("BGGY"),
                                new AttackWaveType.TypeRecordData("MOTOR"),
                                new AttackWaveType.TypeRecordData("FOX"),
                                new AttackWaveType.TypeRecordData("DUST", maxCount : 2),
                            },
                            // Tech Level 7
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("BGGY"),
                                new AttackWaveType.TypeRecordData("MOTOR"),
                                new AttackWaveType.TypeRecordData("FOX"),
                                new AttackWaveType.TypeRecordData("DUST", maxCount : 2),
                                new AttackWaveType.TypeRecordData("SCHP"),
                            },
                        }
                    }, // 拉丁同盟 混合
                    new AttackWaveType()
                    {
                        UIName = "总部守卫 步兵",
                        UIDescription = "大量经过基因改造的士兵构成总部守卫的步兵战队。",
                        SideIdx = 2,
                        TypeDataTable = new List<List<AttackWaveType.TypeRecordData>>()
                        {
                            // Tech Level 1
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("HARP"),
                            },
                            // Tech Level 2
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("HARP"),
                                new AttackWaveType.TypeRecordData("STING"),
                            },
                            // Tech Level 3
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("HARP"),
                                new AttackWaveType.TypeRecordData("YURI"),
                            },
                            // Tech Level 4
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("HARP"),
                                new AttackWaveType.TypeRecordData("YURI"),
                                new AttackWaveType.TypeRecordData("STING"),
                            },
                            // Tech Level 5
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("HARP"),
                                new AttackWaveType.TypeRecordData("YURI"),
                                new AttackWaveType.TypeRecordData("BRUTE"),
                                new AttackWaveType.TypeRecordData("VIRUS"),
                            },
                            // Tech Level 6
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("STALKER"),
                                new AttackWaveType.TypeRecordData("YURI"),
                                new AttackWaveType.TypeRecordData("BRUTE"),
                                new AttackWaveType.TypeRecordData("VIRUS"),
                            },
                            // Tech Level 7
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("HARP"),
                                new AttackWaveType.TypeRecordData("STALKER"),
                                new AttackWaveType.TypeRecordData("YURI"),
                                new AttackWaveType.TypeRecordData("BRUTE"),
                                new AttackWaveType.TypeRecordData("VIRUS"),
                            },
                        }
                    }, // 总部守卫 步兵
                    new AttackWaveType()
                    {
                        UIName = "天蝎组织 陆军",
                        UIDescription = "天蝎组织的坦克部队依靠数量淹没敌人。",
                        SideIdx = 2,
                        TypeDataTable = new List<List<AttackWaveType.TypeRecordData>>()
                        {
                            // Tech Level 1
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("STING"),
                            },
                            // Tech Level 2
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("STING"),
                                new AttackWaveType.TypeRecordData("HARP"),
                            },
                            // Tech Level 3
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("STING"),
                                new AttackWaveType.TypeRecordData("HARP"),
                                new AttackWaveType.TypeRecordData("QTNK"),
                            },
                            // Tech Level 4
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("STING"),
                                new AttackWaveType.TypeRecordData("HARP"),
                                new AttackWaveType.TypeRecordData("QTNK"),
                                new AttackWaveType.TypeRecordData("TRIKE"),
                            },
                            // Tech Level 5
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("STING"),
                                new AttackWaveType.TypeRecordData("YTNK"),
                                new AttackWaveType.TypeRecordData("QTNK"),
                                new AttackWaveType.TypeRecordData("TRIKE"),
                            },
                            // Tech Level 6
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("STING"),
                                new AttackWaveType.TypeRecordData("YTNK"),
                                new AttackWaveType.TypeRecordData("QTNK"),
                                new AttackWaveType.TypeRecordData("TRIKE"),
                                new AttackWaveType.TypeRecordData("SCAV"),
                            },
                            // Tech Level 7
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("STING"),
                                new AttackWaveType.TypeRecordData("YTNK"),
                                new AttackWaveType.TypeRecordData("QTNK"),
                                new AttackWaveType.TypeRecordData("TRIKE"),
                                new AttackWaveType.TypeRecordData("SCAV"),
                                new AttackWaveType.TypeRecordData("COYO"),
                            },
                        }
                    }, // 天蝎组织 陆军
                    new AttackWaveType()
                    {
                        UIName = "总部守卫 陆军",
                        UIDescription = "总部守卫的地面部队非常擅长摧毁载具。",
                        SideIdx = 2,
                        TypeDataTable = new List<List<AttackWaveType.TypeRecordData>>()
                        {
                            // Tech Level 1
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("STNK"),
                            },
                            // Tech Level 2
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("STNK"),
                                new AttackWaveType.TypeRecordData("BRUTE"),
                            },
                            // Tech Level 3
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("STNK"),
                                new AttackWaveType.TypeRecordData("BRUTE"),
                                new AttackWaveType.TypeRecordData("HARP"),
                            },
                            // Tech Level 4
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("STNK"),
                                new AttackWaveType.TypeRecordData("BRUTE"),
                                new AttackWaveType.TypeRecordData("HARP"),
                                new AttackWaveType.TypeRecordData("DISK"),
                            },
                            // Tech Level 5
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("STNK"),
                                new AttackWaveType.TypeRecordData("BRUTE"),
                                new AttackWaveType.TypeRecordData("HARP"),
                                new AttackWaveType.TypeRecordData("DISK"),
                                new AttackWaveType.TypeRecordData("SHADOW"),
                            },
                            // Tech Level 6
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("STNK"),
                                new AttackWaveType.TypeRecordData("BRUTE"),
                                new AttackWaveType.TypeRecordData("HARP"),
                                new AttackWaveType.TypeRecordData("DEVO"),
                            },
                            // Tech Level 7
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("STNK"),
                                new AttackWaveType.TypeRecordData("BRUTE"),
                                new AttackWaveType.TypeRecordData("DEVO"),
                            },
                        }
                    }, // 总部守卫 陆军
                    new AttackWaveType()
                    {
                        UIName = "厄普西隆 混合",
                        UIDescription = "这支混合部队脆弱但输出凶猛。",
                        SideIdx = 2,
                        TypeDataTable = new List<List<AttackWaveType.TypeRecordData>>()
                        {
                            // Tech Level 1
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("INIT"),
                            },
                            // Tech Level 2
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("INIT"),
                                new AttackWaveType.TypeRecordData("STING"),
                            },
                            // Tech Level 3
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("KAOS"),
                                new AttackWaveType.TypeRecordData("STING"),
                                new AttackWaveType.TypeRecordData("DISK"),
                            },
                            // Tech Level 4
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("INIT"),
                                new AttackWaveType.TypeRecordData("DISK"),
                            },
                            // Tech Level 5
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("INIT"),
                                new AttackWaveType.TypeRecordData("DUNE"),
                                new AttackWaveType.TypeRecordData("DISK"),
                            },
                            // Tech Level 6
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("INIT"),
                                new AttackWaveType.TypeRecordData("DISK"),
                                new AttackWaveType.TypeRecordData("BASIL"),
                            },
                            // Tech Level 7
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("INIT"),
                                new AttackWaveType.TypeRecordData("KAOS"),
                                new AttackWaveType.TypeRecordData("DISK"),
                                new AttackWaveType.TypeRecordData("BASIL"),
                            },
                        }
                    }, // 厄普西隆 混合
                    new AttackWaveType()
                    {
                        UIName = "科洛尼亚侧翼 空军",
                        UIDescription = "这支舰队从空中横扫科洛尼亚侧翼的敌人。",
                        SideIdx = 3,
                        TypeDataTable = new List<List<AttackWaveType.TypeRecordData>>()
                        {
                            // Tech Level 1
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("JACKAL"),
                            },
                            // Tech Level 2
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("JACKAL"),
                                new AttackWaveType.TypeRecordData("DRACO"),
                            },
                            // Tech Level 3
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("JACKAL"),
                                new AttackWaveType.TypeRecordData("DRACO"),
                                new AttackWaveType.TypeRecordData("KNIGHT"),
                            },
                            // Tech Level 4
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("JACKAL"),
                                new AttackWaveType.TypeRecordData("DRACO"),
                                new AttackWaveType.TypeRecordData("KNIGHT"),
                                new AttackWaveType.TypeRecordData("BUZZ"),
                            },
                            // Tech Level 5
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("JACKAL"),
                                new AttackWaveType.TypeRecordData("DRACO"),
                                new AttackWaveType.TypeRecordData("KNIGHT"),
                                new AttackWaveType.TypeRecordData("BUZZ"),
                                new AttackWaveType.TypeRecordData("HURR"),
                            },
                            // Tech Level 6
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("DRACO"),
                                new AttackWaveType.TypeRecordData("BUZZ"),
                                new AttackWaveType.TypeRecordData("HURR"),
                                new AttackWaveType.TypeRecordData("VIPER"),
                            },
                            // Tech Level 7
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("DRACO"),
                                new AttackWaveType.TypeRecordData("BUZZ"),
                                new AttackWaveType.TypeRecordData("HURR"),
                                new AttackWaveType.TypeRecordData("VIPER"),
                                new AttackWaveType.TypeRecordData("QUETZ"),
                            },
                        }
                    }, // 科洛尼亚侧翼 空军
                    new AttackWaveType()
                    {
                        UIName = "科洛尼亚侧翼 陆军",
                        UIDescription = "科洛尼亚侧翼的陆军使用灵活的格斗单位掩护远程火炮推进。",
                        SideIdx = 3,
                        TypeDataTable = new List<List<AttackWaveType.TypeRecordData>>()
                        {
                            // Tech Level 1
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("KNIGHT"),
                            },
                            // Tech Level 2
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("KNIGHT"),
                                new AttackWaveType.TypeRecordData("DRACO"),
                            },
                            // Tech Level 3
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("KNIGHT"),
                                new AttackWaveType.TypeRecordData("DRACO"),
                                new AttackWaveType.TypeRecordData("ROADR"),
                            },
                            // Tech Level 4
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("KNIGHT"),
                                new AttackWaveType.TypeRecordData("ROADR"),
                                new AttackWaveType.TypeRecordData("TARCHIA"),
                            },
                            // Tech Level 5
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("KNIGHT"),
                                new AttackWaveType.TypeRecordData("DRACO"),
                                new AttackWaveType.TypeRecordData("ZORB"),
                            },
                            // Tech Level 6
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("KNIGHT"),
                                new AttackWaveType.TypeRecordData("DRACO"),
                                new AttackWaveType.TypeRecordData("ZORB"),
                                new AttackWaveType.TypeRecordData("TARCHIA"),
                            },
                            // Tech Level 7
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("KNIGHT"),
                                new AttackWaveType.TypeRecordData("DRACO"),
                                new AttackWaveType.TypeRecordData("ROADR"),
                                new AttackWaveType.TypeRecordData("TARCHIA"),
                                new AttackWaveType.TypeRecordData("ZORB"),
                            },
                        }
                    }, // 科洛尼亚侧翼 陆军
                    new AttackWaveType()
                    {
                        UIName = "最后堡垒 步兵",
                        UIDescription = "最后堡垒用纳米同步技术强化他们的步兵。",
                        SideIdx = 3,
                        TypeDataTable = new List<List<AttackWaveType.TypeRecordData>>()
                        {
                            // Tech Level 1
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("COVE"),
                            },
                            // Tech Level 2
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("COVE"),
                                new AttackWaveType.TypeRecordData("KNIGHT"),
                            },
                            // Tech Level 3
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("COVE"),
                                new AttackWaveType.TypeRecordData("KNIGHT"),
                                new AttackWaveType.TypeRecordData("SYNC"),
                            },
                            // Tech Level 4
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("COVE"),
                                new AttackWaveType.TypeRecordData("KNIGHT"),
                                new AttackWaveType.TypeRecordData("RAIL"),
                            },
                            // Tech Level 5
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("KNIGHT"),
                                new AttackWaveType.TypeRecordData("RAIL"),
                                new AttackWaveType.TypeRecordData("HUNTR"),
                                new AttackWaveType.TypeRecordData("KINGS"),
                            },
                            // Tech Level 6
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("KNIGHT"),
                                new AttackWaveType.TypeRecordData("RAIL"),
                                new AttackWaveType.TypeRecordData("HUNTR"),
                                new AttackWaveType.TypeRecordData("KINGS"),
                                new AttackWaveType.TypeRecordData("BANE_N"),
                            },
                            // Tech Level 7
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("KNIGHT"),
                                new AttackWaveType.TypeRecordData("RAIL"),
                                new AttackWaveType.TypeRecordData("HUNTR"),
                                new AttackWaveType.TypeRecordData("KINGS"),
                                new AttackWaveType.TypeRecordData("BANE_N"),
                                new AttackWaveType.TypeRecordData("SYNC_N"),
                            },
                        }
                    }, // 最后堡垒 步兵
                    new AttackWaveType()
                    {
                        UIName = "最后堡垒 混合",
                        UIDescription = "焚风的地面机械技术最终孕育出乳齿象，它们能轻易击毁大量载具。",
                        SideIdx = 3,
                        TypeDataTable = new List<List<AttackWaveType.TypeRecordData>>()
                        {
                            // Tech Level 1
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("KNIGHT"),
                            },
                            // Tech Level 2
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("KNIGHT"),
                                new AttackWaveType.TypeRecordData("SYNC"),
                            },
                            // Tech Level 3
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("KNIGHT"),
                                new AttackWaveType.TypeRecordData("SYNC"),
                                new AttackWaveType.TypeRecordData("ROACH"),
                            },
                            // Tech Level 4
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("KNIGHT"),
                                new AttackWaveType.TypeRecordData("SYNC"),
                                new AttackWaveType.TypeRecordData("ROACH"),
                                new AttackWaveType.TypeRecordData("COND"),
                            },
                            // Tech Level 5
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("KNIGHT"),
                                new AttackWaveType.TypeRecordData("GHTNK"),
                                new AttackWaveType.TypeRecordData("COND"),
                            },
                            // Tech Level 6
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("KNIGHT"),
                                new AttackWaveType.TypeRecordData("GHTNK"),
                                new AttackWaveType.TypeRecordData("ROACH"),
                                new AttackWaveType.TypeRecordData("COND"),
                            },
                            // Tech Level 7
                            new List<AttackWaveType.TypeRecordData>()
                            {
                                new AttackWaveType.TypeRecordData("GHTNK"),
                                new AttackWaveType.TypeRecordData("COND"),
                                new AttackWaveType.TypeRecordData("PROME"),
                            },
                        }
                    }, // 最后堡垒 混合
                    //new AttackWaveType()
                    //{
                    //    UIName = "",
                    //    UIDescription = "",
                    //    SideIdx = 0,
                    //    TypeDataTable = new List<List<AttackWaveType.TypeRecordData>>()
                    //    {
                    //        // Tech Level 1
                    //        new List<AttackWaveType.TypeRecordData>()
                    //        {
                    //            new AttackWaveType.TypeRecordData(""),
                    //        },
                    //        // Tech Level 2
                    //        new List<AttackWaveType.TypeRecordData>()
                    //        {
                    //            new AttackWaveType.TypeRecordData(""),
                    //            new AttackWaveType.TypeRecordData(""),
                    //        },
                    //        // Tech Level 3
                    //        new List<AttackWaveType.TypeRecordData>()
                    //        {
                    //            new AttackWaveType.TypeRecordData(""),
                    //            new AttackWaveType.TypeRecordData(""),
                    //        },
                    //        // Tech Level 4
                    //        new List<AttackWaveType.TypeRecordData>()
                    //        {
                    //            new AttackWaveType.TypeRecordData(""),
                    //            new AttackWaveType.TypeRecordData(""),
                    //            new AttackWaveType.TypeRecordData(""),
                    //        },
                    //        // Tech Level 5
                    //        new List<AttackWaveType.TypeRecordData>()
                    //        {
                    //            new AttackWaveType.TypeRecordData(""),
                    //            new AttackWaveType.TypeRecordData(""),
                    //            new AttackWaveType.TypeRecordData(""),
                    //            new AttackWaveType.TypeRecordData(""),
                    //        },
                    //        // Tech Level 6
                    //        new List<AttackWaveType.TypeRecordData>()
                    //        {
                    //            new AttackWaveType.TypeRecordData(""),
                    //            new AttackWaveType.TypeRecordData(""),
                    //            new AttackWaveType.TypeRecordData(""),
                    //            new AttackWaveType.TypeRecordData(""),
                    //        },
                    //        // Tech Level 7
                    //        new List<AttackWaveType.TypeRecordData>()
                    //        {
                    //            new AttackWaveType.TypeRecordData(""),
                    //            new AttackWaveType.TypeRecordData(""),
                    //            new AttackWaveType.TypeRecordData(""),
                    //            new AttackWaveType.TypeRecordData(""),
                    //            new AttackWaveType.TypeRecordData(""),
                    //        },
                    //    }
                    //},
                };
            }
        }

        private void InitType()
        {
            // 随机选择一个AI玩家确定阵营
            int aiHouseIdx = GetSpawnerWithData().spawner.Ref.Type.Ref.SideIndex;
            // 找到符合阵营的攻击波次类型
            var availableTypes = new List<AttackWaveType>();
            foreach (var type in AttackWaveType.Array)
            {
                if (type.SideIdx == aiHouseIdx)
                    availableTypes.Add(type);
            }
            // 从符合阵营的类型中随机选择一个作为当前类型
            if (availableTypes.Count > 0)
            {
                CurrentType = ScenarioClass.GetRandomInList(availableTypes);
            }
            else
            {
                Logger.Log("AttackWaveManager: No available attack wave type for side index " + aiHouseIdx);
            }
        }

        public /*override*/ void OnInit()
        {
            InitType();
        }

        private void UpdateAttackWaveSpawn()
        {
            for (int i = 0; i < Schedule.Count; i++)
            {
                var node = Schedule[i];

                // 刷出
                if (node.SpawningFrame <= Game.CurrentFrame)
                {
                    SpawnByScheduleNode(node);

                    node.LoopCount--;
                    node.SpawningFrame += node.LoopDelay;
                    // TODO : 真正的升级
                    if (node.TechLevel < 7)
                        node.TechLevel++;
                    if (node.AmountLevel < 7)
                        node.AmountLevel++;
                }

                // 移除已完成的节点
                if (node.LoopCount == 0)
                {
                    Schedule.RemoveAt(i);
                    i--;
                }
                else
                {
                    Schedule[i] = node;
                }
            }
        }
        private void UpdateAttackWaves()
        {
            // 周期更新进攻波次
            var count = AttackWave.Array.Count;
            for (int i = 0; i < count; i++)
            {
                if (Game.CurrentFrame % count == i)
                {
                    var wave = AttackWave.Array[i];
                    wave.Update();

                    if (wave.ShouldDeleteNow())
                    {
                        AttackWave.Array.RemoveAt(i);
                        count--;
                        i--;
                    }
                }
            }
        }
        public /*override*/ void OnUpdate()
        {
            UpdateAttackWaveSpawn();
            UpdateAttackWaves();
        }

        public AttackWave SpawnAttackWave(CoordStruct crd, int techLevel, int amountLevel)
        {
            var spawnerData = GetSpawnerWithData();

            if (spawnerData.spawner.IsNull || spawnerData.amountMultiplier <= 0.0)
            {
                Logger.Log("AttackWaveManager: No valid spawner found for spawning attack wave.");
                return null;
            }
            
            var script = AttackWaveScriptNode.DefaultScript();
            var result = CurrentType.SpawnAt(spawnerData.spawner, script, spawnerData.houseCount, spawnerData.amountMultiplier, crd, techLevel, amountLevel);
            return result;
        }

        public AttackWave SpawnByScheduleNode(AttackWaveScheduleNode node)
        {
            var spawnerData = GetSpawnerWithData(node.ForcedHouseIdx);

            if (spawnerData.spawner.IsNull || spawnerData.amountMultiplier <= 0.0)
            {
                Logger.Log("AttackWaveManager: No valid spawner found for schedule node with forced house index " + node.ForcedHouseIdx);
                return null;
            }

            var crd = node.SelectSpawningCrdFunc(spawnerData.spawner, Pointer<AbstractClass>.Zero);
            var type = node.ForecedType ?? CurrentType;
            var result = type.SpawnAt(spawnerData.spawner, node.Script, spawnerData.houseCount, spawnerData.amountMultiplier, crd, node.TechLevel, node.AmountLevel);
            var addtionalMembers = new List<Pointer<TechnoClass>>();

            if (node.AdditionalUnits != null)
            {
                foreach (var typeRecordData in node.AdditionalUnits)
                {
                    var technoType = TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find(typeRecordData.TypeName);
                    var techno = technoType.Ref.Base.CreateObject(spawnerData.spawner).Convert<TechnoClass>();

                    if (typeRecordData.OnSpawnFunc != null)
                        typeRecordData.OnSpawnFunc(techno);

                    addtionalMembers.Add(techno);
                }
            }

            result.AddMembers(addtionalMembers);
            
            // 添加风暴英雄
            foreach (var mutator in MutatorCacheManager.Instance.MutatorArray)
            {
                if (mutator is Mutators.HeroesFromTheStorm hftsm && hftsm.IsOnOurSide(result.OwnerHouse))
                    hftsm.AddHeroToAttackWave(result);
            }
            
            return result;
        }

        private double GetAmountMultiplier(Pointer<HouseClass> owner)
        {
            return 1.0;
        }

        private static bool IsOnOurSide(Pointer<HouseClass> owner)
        {
            if (owner.Ref.Type.Ref.MultiplayPassive)
                return false;

            foreach (var house in HouseClass.Array)
            {
                if (owner.Ref.Type.Ref.MultiplayPassive)
                    continue;

                // 和其中一个玩家双向敌对就算我方
                if (house.Ref.ControlledByHuman() && !owner.Ref.IsAlliedWith(house) && !house.Ref.IsAlliedWith(owner))
                    return true;
            }
            return false;
        }

        private (Pointer<HouseClass> spawner, int houseCount, double amountMultiplier) GetSpawnerWithData(int forcedIdx = -1)
        {
            List<Pointer<HouseClass>> candidates = new List<Pointer<HouseClass>>();
            double totalMultiplier = 0.0;

            foreach (var house in HouseClass.Array)
            {
                if (house.Ref.Type.Ref.SideIndex > 3) // MO only
                    continue;

                if (IsOnOurSide(house))
                {
                    // 计算倍率考虑的更多
                    totalMultiplier += GetAmountMultiplier(house);

                    // 第一帧之前没有basecenter
                    if (Game.CurrentFrame >= 1)
                    {
                        // 必须在有效区域内
                        if (!MapClass.Instance.IsWithinUsableArea(house.Ref.GetBaseCenter(), true))
                            continue;

                        // 被拆完的地方就不要刷红点了
                        if (house.Ref.Buildings.Count <= 0)
                            continue;
                    }

                    // 刷出者的筛选更严格
                    candidates.Add(house);
                }
            }
            return (forcedIdx < 0 ? ScenarioClass.GetRandomInList(candidates) : HouseClass.Array[forcedIdx], candidates.Count, totalMultiplier / candidates.Count);
        }

        public List<AttackWaveType> TypeArray = new List<AttackWaveType>();
        public List<AttackWave> Array = new List<AttackWave>();

        public AttackWaveType CurrentType;
        private TimerStruct UpdateTimer;
        private List<AttackWaveScheduleNode> Schedule = new List<AttackWaveScheduleNode>();

        public readonly int HouseCountMax = 7;
    }
}
