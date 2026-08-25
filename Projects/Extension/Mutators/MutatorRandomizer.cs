using DynamicPatcher;
using Microsoft.CSharp;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Mutators
{
    public class MutatorRandomizer
    {
        private const int MaxScore = 10;
        private const int MinScore = 1;

        private static Dictionary<string, string> MutatorDesc = new Dictionary<string, string>()
        {
            //点数为1的因子
            { "震荡攻击", "玩家单位会被所有敌方攻击减速。" },
            { "强行征用", "敌人摧毁你的建筑后将获得建筑的控制权。" },
            { "闪避机动", "敌方单位受到伤害时将传送一小段距离。" },
            { "生命吸取", "敌方单位和建筑在造成伤害时偷取生命值或护盾。" },
            { "轨道轰炸", "敌人会在地图上周期性地施放轨道轰炸。" },
            { "光子过载", "所有敌方建筑会攻击附近的敌对单位。" },
            { "短视症", "玩家单位及其建筑的视野范围缩短。" },
            { "时空力场", "地图上会周期性地部署敌人的时空力场。" },
            { "时间扭曲", "地图上会周期性地部署敌人的时间扭曲。" },
            //点数为2的因子
            { "异形寄生", "所有敌方单位在死亡时会孵化巢虫。" },
            { "减伤屏障", "敌方单位和建筑在第一次受到伤害时会获得一个临时护盾。" },
            { "暗无天日", "先前探索过的区域若离开了玩家的视野范围将会重新变成一片黑色。" },
            { "坚强意志", "敌方英雄单位附近有任何非英雄敌方单位时，其所受到的伤害最高不超过10点。" },
            { "鼓舞人心", "敌方英雄单位提高小范围内所有敌人的攻击速度和护甲。" },
            { "激光钻机", "一台敌方激光钻机会不停地攻击位于敌人视野范围内的玩家单位。" },
            { "超远视距", "敌方单位和建筑的武器射程与视野范围提高。" },
            { "晶矿护盾", "玩家基地中的晶体矿簇会被周期性包覆一层护盾，必须将其摧毁才能继续采集资源。" },
            { "默哀", "敌方英雄单位死亡时，在其周围的所有玩家单位都会反思自己的过错，无法攻击或使用技能。" },
            { "净化光束", "地图上会出现一道敌人的净化光束并朝附近的玩家单位移动。" },
            { "焦土政策", "敌方单位死亡时会点燃地面。" },
            { "速度狂魔", "敌方单位移动速度提高。" },
            { "龙卷风暴", "多股龙卷风在地图上移动，对位于其行进路线上的玩家单位造成伤害并将其击退。" },
            { "行尸走肉", "敌方单位在死亡时生成大量的被感染的人类，具体数量由死亡单位的生命值决定。" },
            //点数为3的因子
            { "进攻部署", "周期性地将额外的敌方单位部署到战场上。" },
            { "伤害散射", "对敌人造成的伤害将平摊给所有附近的单位，包括你的单位。" },
            { "双重压力", "你的单位也会受到他们自身造成的所有伤害，但是会持续恢复。" },
            { "致命勾引", "敌方单位或建筑被摧毁后，你附近的任何单位将被牵拉至被它们的位置。" },
            { "无边恐惧", "玩家的单位在受到伤害时会不时地停止攻击，并且害怕地到处乱跑。" },
            { "核弹打击", "核弹会随机在整张地图上进行发射。" },
            { "岩浆爆发", "岩浆会周期性地在随机位置从地下喷发，并对玩家的空中和地面单位造成伤害。" },
            { "飞弹大战", "你的建筑会不停地遭受飞弹轰炸的袭击，你必须将它们击落。" },
            { "丧尸大战", "敌方被感染的人类会不断地出现在地图上。" },
            { "自毁程序", "敌方单位死亡时发生爆炸，并对附近的玩家单位造成伤害。" },
            { "来去无踪", "所有敌方单位永久隐形。" },
            //点数为4的因子
            { "暴风雪", "风暴雷云在地图上飘荡，对位于其行进路线上的玩家单位造成伤害并将其冻结。" },
            { "强磁雷场", "麦格天雷会在任务一开始布满整个地图。" },
            //点数为5的因子
            { "复仇战士", "当附近的敌方单位死亡时，敌方单位的攻击速度、移动速度、护甲、生命值以及生命回复速度提高。" },
            { "拿钱说话", "对你的单位发出指令会消耗资源，数量取决于该单位的生产价格。" },
            { "相互摧毁", "敌方混合体死亡时会引爆一发核弹。" },
            { "灵能爆表", "所有敌方单位拥有能量并且使用随机技能。" },
            { "小捞油水", "玩家的工人单位采集资源的效率降低，但是地图上会生成可以拾取的资源。" },
            { "虚空重生者", "虚空重生者游荡在战场上，不断地复活你的敌人。" },
            //点数为6的因子
            { "杀戮机器人", "来源不明的进攻性机器人已被释放到了科普卢星区，意图制造毁灭。" },
            { "扫雷专家", "数量庞大的寡妇雷和蜘蛛雷遍布整个战场。" },
            //点数为7的因子
            { "黑死病", "一些敌方单位携带着一种疫病，不仅会持续造成伤害，还会传染给附近的其它单位。" },
            { "给我死吧！", "敌方单位死亡后会自动复活。" },
            { "极性不定", "每一个敌方单位不是对你的单位免疫，就是对你盟友的单位免疫。" },
            { "力量蜕变", "敌方单位造成伤害时有一定几率变形成更强大的单位。" },
            //点数为8的因子
            { "同化体", "无形的软泥怪缓慢爬向你的基地，被其接触到的任何单位和建筑都将变成和它们一样的复制体。" },
            //点数为10的因子
            { "炸弹机器人", "对一切都毫不在意的机器人携带着聚变弹头朝你的基地进发。一名玩家必须识别出拆弹的顺序，另一名玩家则必须正确输入才能解除危机。" },
            { "风暴英雄", "每一轮攻击波次都会由实力越来越强的英雄率领。" },
            { "虚空裂隙", "虚空裂隙周期性地出现在随机位置，并会不断地生成敌方单位，直至其被摧毁。" },
            //无点数的限定因子
            { "惧怕黑暗", "通过各种方式提供的视野都会受到极大的限制，只有你镜头中的视野一切正常。" },
            { "混乱工作室", "突变因子会随机选择，并且在任务中周期性轮换。" },
            { "焰火秀", "敌人死亡时会发射灿烂的烟花，对你周围的单位造成伤害。" },
            { "礼尚往来", "地图上会周期性地放置一些礼物。" }
        };

        // 检查有效性后才会塞进AvailableMutators
        private static Dictionary<string, (string SWID, int Score)> AvailableMutators = new Dictionary<string, (string, int)>();

        // 进程级一次性初始化标志：AvailableMutators 是静态配置（内容固定），只允许初始化一次。
        // 用独立标志而非 Count>0 判断，避免语义混淆。
        private static bool Initialized = false;
        public static List<Type> AvailableMutatorsForRandom = new List<Type>()
        { 
            typeof(BlackDeath),
            typeof(Fear),
            typeof(KillBots),
            typeof(LaserDrill),
            typeof(Magnificent),
            typeof(MineralShields),
            typeof(Minesweeper),
            typeof(MissileCommand),
            typeof(Outbreak),
            typeof(Propagators),
            typeof(PurifierBeam),
            typeof(SlimPackings),
            typeof(Transmutation),
            typeof(VoidRifts),
            typeof(VoidReanimators),
            typeof(EvasiveManeuvers),
            typeof(EminentDomain),
            typeof(HeroesFromTheStorm),
            typeof(AggressiveDeployment),
            typeof(LongRange),
            typeof(GoingNuclear),
            typeof(LavaBurst),
            typeof(TimeWarp),
            typeof(Blizzard),
            typeof(OrbitalStrike),
            typeof(MicroTransactions),
            typeof(Polarity),
            typeof(Darkness),
            typeof(BoomBots),
            typeof(PowerOverwhelming),
            typeof(TemporalField),
            typeof(LifeLeech),
            typeof(Diffusion),
            typeof(SelfDestruction),
            typeof(Twister),
            typeof(FatalAttraction),
            typeof(Shortsighted)
        };

        // 新的因子需要在这里注册，SWID为空表示通过代码实现，非空表示通过INI实现
        private static Dictionary<string, (string SWID, int Score)> MutatorDataBase = new Dictionary<string, (string, int)>
        {
            // 已完成的
            {"BlackDeath", ("", 7)},
            {"Fear", ("", 3)},
            {"KillBots", ("", 6)},
            {"LaserDrill", ("", 2)},
            {"Magnificent", ("", 4)},
            {"MineralShields", ("", 2)},
            {"Minesweeper", ("", 6)},
            {"MissileCommand", ("", 3)},
            {"Outbreak", ("", 3)},
            {"Propagators", ("", 8)},
            {"PurifierBeam", ("", 2)},
            {"SlimPackings", ("", 5)},
            {"Transmutation", ("", 7)},
            {"VoidRifts", ("", 10)},
            {"VoidReanimators", ("", 5)},
            {"EvasiveManeuvers", ("", 1)},
            {"EminentDomain", ("", 1)},
            {"HeroesFromTheStorm", ("", 10) },
            {"AggressiveDeployment", ("", 3) },
            {"LongRange", ("", 2) },
            {"GoingNuclear", ("", 3) },
            {"LavaBurst", ("", 3) },
            {"TimeWarp", ("", 1) },
            {"Blizzard", ("", 4) },
            {"OrbitalStrike", ("", 1)},
            {"MicroTransactions", ("", 5)},
            {"Polarity", ("", 7)},
            {"Darkness", ("", 2)},
            {"BoomBots", ("", 10)},
            {"PowerOverwhelming", ("", 5)},
            {"TemporalField", ("", 1)},
            {"LifeLeech", ("", 1)},
            {"Diffusion", ("", 3)},
            {"SelfDestruction", ("", 3)},
            {"Twister", ("", 2)},
            {"FatalAttraction", ("", 3)},
            {"Shortsighted", ("", 1)},
            // ini实现的
            {"ConcussiveAttacks", ("200SSW", 1)},
            // {"LifeLeech", ("1500SSW", 1)},
            {"PhotonOverload", ("100SSW", 1)},
            {"AlienIncubation", ("600SSW", 2)},
            {"Barrier", ("300SSW", 2)},
            {"HardenedWill", ("1000SSW", 2)},
            {"Inspiration", ("1100SSW", 2)},
            {"MomentofSilence", ("500SSW", 2)},
            {"ScorchedEarth", ("2000SSW", 2)},
            {"SpeedFreaks", ("2100SSW", 2)},
            {"WalkingInfested", ("700SSW", 2)},
            // {"Diffusion", ("1400SSW", 3)},
            {"DoubleEdged", ("900SSW", 3)},
            {"WeMoveUnseen", ("1700SSW", 3)},
            {"Avenger", ("800SSW", 5)},
            {"MutuallyAssuredDestruction", ("1800SSW", 5)},
            {"JustDie", ("1600SSW", 7)}, // TODO : 重制
            // {"SelfDestruction", ("1900SSW", 3)}
            
            // 不会加入随机池
            //{"ChaosStudios", ("", 0)},

            // ==== 以下为未完全实现/待制作的因子（注释备用） ====

            //{"NaughtyList", ("", 7)},
            //{"TrickorTreat", ("", 3)},
            //{"Fireworks", ("", 10)},
            //{"SharingisCaring", ("", 4)}, // 改成电力

            // 不会加入随机池
            //{"GiftExchange", ("", 0)},
            //{"AfraidoftheDark", ("", 0)},
            //{"LuckyEnvelopes", ("", 0)},
            //{"TurkeyShoot", ("", 0)}, // 改成电力

            // ......傻逼
            //{"Vertigo", ("", 0)} // 迷失方向
        };
        private static Dictionary<int, (int sumMin, int sumMax, int countMin, int countMax)> BrutalPlusParams = new Dictionary<int, (int sumMin, int sumMax, int countMin, int countMax)>
        {
            { 1, (4, 6, 2, 3) },
            { 2, (7, 8, 2, 3) },
            { 3, (9, 10, 2, 3) },
            { 4, (11, 12, 2, 3) },
            { 5, (15, 16, 2, 4) },
            { 6, (19, 20, 2, 4) }
        };
        public static Pointer<HouseClass> FindFirstPlayer()
        {
            foreach (var house in HouseClass.Array)
            {
                if (house.Ref.ControlledByHuman())
                    return house;
            }
            return Pointer<HouseClass>.Zero;
        }
        public static void Init()
        {
            // 检查所有因子名称，记录可用的名称和分数
            // AvailableMutators 是进程内静态、内容固定，只允许初始化一次。
            // 否则读档后 CurrentFrame 归零会再次进入此处，往字典 Add 重复键抛 ArgumentException。
            if (Initialized)
            {
                return;
            }
            Initialized = true;

            var player = FindFirstPlayer();
            bool canUseINIMutator = player.IsNotNull;
            if (!canUseINIMutator)
            {
                Logger.Log("No player found! Can not active INI implemented mutators!");
            }
            foreach (var keyValuePair in MutatorDataBase)
            {
                var name = keyValuePair.Key;
                var pair = keyValuePair.Value;
                if (pair.SWID == "")
                {
                    Type mutatorType = Type.GetType("Extension.Mutators." + name);
                    if (mutatorType == null)
                    {
                        Logger.Log(name + " is not a type!");
                        continue;
                    }
                    if (!typeof(Mutator).IsAssignableFrom(mutatorType))
                    {
                        Logger.Log(name + " is not a mutator!");
                        continue;
                    }
                    if (mutatorType.IsAbstract)
                    {
                        Logger.Log(name + " is an abstract class!");
                        continue;
                    }
                }
                else
                {
                    var sw = SuperWeaponTypeClass.ABSTRACTTYPE_ARRAY.Find(pair.SWID);
                    if (sw.IsNull)
                    {
                        Logger.Log("Activator " + pair.SWID + " is invalid!");
                        continue;
                    }
                }
                AvailableMutators.Add(name, pair);
            }
            Logger.Log("MutatorRandomizer.Init() done. AvailableMutators.Count={0}\n", AvailableMutators.Count);
        }
        private static int GetScoreByName(string name)
        {
            return AvailableMutators.TryGetValue(name, out var pair) ? pair.Score : 0;
        }
        public static bool ActiveMutatorByName(string name)
        {
            if (!AvailableMutators.TryGetValue(name, out var pair))
                return false;

            if (pair.SWID == "")
            {
                Type mutatorType = Type.GetType("Extension.Mutators." + name);
                var mutator = Mutator.CreateMutator(mutatorType, Pointer<HouseClass>.Zero);
                if (mutator != null)
                {
                    mutator.Init();
                    return true;
                }
                else
                {
                    Logger.Log("Create mutator failed!");
                    return false;
                }
            }
            else
            {
                var sw = SuperWeaponTypeClass.ABSTRACTTYPE_ARRAY.Find(pair.SWID);
                var player = FindFirstPlayer();
                player.Ref.FindSuperWeapon(sw).Ref.Launch(player.Ref.BaseSpawnCell, false);
                return true;
            }
        }
        public static bool DeactiveMutatorByName(string name)
        {
            if (!AvailableMutators.TryGetValue(name, out var pair))
                return false;
            if (pair.SWID == "")
            {
                Type mutatorType = Type.GetType("Extension.Mutators." + name);
                Mutator.DestroyMutator(mutatorType, Pointer<HouseClass>.Zero);
                return true;
            }
            else
            {
                // 通过超级武器实现的因子无法被取消
                return false;
            }
        }
        public static List<string> GetMutatorNamesByScore(int score)
        {
            List<string> mutatorNamesOfScore = new List<string>();
            foreach (var pair in AvailableMutators)
            {
                if (pair.Value.Score == score)
                    mutatorNamesOfScore.Add(pair.Key);
            }
            return mutatorNamesOfScore;
        }
        public static List<string> SimpleRandom(int count)
        {
            List<string> selectedNames = new List<string>();
            for (var i = 0; i < 100 && count > 0; i++)
            {
                var name = ScenarioClass.GetRandomKeyInDictionary(AvailableMutators);
                if (selectedNames.Contains(name))
                    continue;
                selectedNames.Add(name);
                count--;
            }
            return selectedNames;
        }
        private static void CheckBrutalPlusRandomResult(int brutalLevel, List<string> selected)
        {
            BrutalPlusParams.TryGetValue(brutalLevel, out var brutalPlusParams);
            var score = 0;
            var count = selected.Count;
            foreach (var name in selected)
            {
                score += GetScoreByName(name);
                Logger.Log("Selected " + name);
            }
            if (score > brutalPlusParams.sumMax)
                Logger.Log("Want max {0} but score is {1}", brutalPlusParams.sumMax, score);
            if (score < brutalPlusParams.sumMin)
                Logger.Log("Want min {0} but score is {1}", brutalPlusParams.sumMin, score);
            if (count > brutalPlusParams.countMax)
                Logger.Log("Want max {0} but count is {1}", brutalPlusParams.countMax, score);
            if (count < brutalPlusParams.countMin)
                Logger.Log("Want min {0} but count is {1}", brutalPlusParams.countMin, score);
        }
        public static List<string> BrutalPlusRandom(int brutalPlusLevel)
        {
            List<string> selectedNames = new List<string>();
            if (!BrutalPlusParams.TryGetValue(brutalPlusLevel, out var brutalPlusParams))
            {
                Logger.Log("Invalid brutal plus level!");
                return selectedNames;
            }
            int scoreSelected = 0;
            for (var i = 0; i < 100 && scoreSelected < brutalPlusParams.sumMin; i++)
            {
                var remainedCountMin = brutalPlusParams.countMin - selectedNames.Count; // 至少还得选这么多个
                var scoreMin = (brutalPlusParams.sumMin - scoreSelected) / (brutalPlusParams.countMax - selectedNames.Count);
                var scoreMax = brutalPlusParams.sumMax - scoreSelected;
                if (remainedCountMin - 1 > 0) // 选完这个之后还要接着选
                {
                    scoreMax -= (remainedCountMin - 1) * MinScore; // 给后续每个因子至少留1分
                }
                if (selectedNames.Count == 0) // 第一次选提高上限
                {
                    scoreMin += (brutalPlusLevel + 1) / 2;
                }
                // 分数不能超过上下限
                scoreMin = Math.Max(MinScore, scoreMin);
                scoreMax = Math.Min(MaxScore, scoreMax);
                // 生成随机池
                var pool = new List<string>();
                foreach (var mutator in AvailableMutators)
                {
                    var score = mutator.Value.Score;
                    if (score >= scoreMin && score <= scoreMax && !selectedNames.Contains(mutator.Key))
                        pool.Add(mutator.Key);
                }
                // 随机
                if (pool.Count > 0)
                {
                    var selected = ScenarioClass.GetRandomInList(pool);
                    scoreSelected += GetScoreByName(selected);
                    selectedNames.Add(selected);
                }
            }
            CheckBrutalPlusRandomResult(brutalPlusLevel, selectedNames);
            return selectedNames;
        }
    }
}
