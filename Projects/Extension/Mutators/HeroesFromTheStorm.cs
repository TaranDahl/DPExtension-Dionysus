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
    public class HeroesFromTheStorm : Mutator
    {
        public override string UIName => "风暴英雄";
        public override string Description => "每一轮攻击波次都会由实力越来越强的英雄率领。";
        public override bool IsAvailableInRPG => false;
        public override int Score => 10;
        public HeroesFromTheStorm(Pointer<HouseClass> owner) : base(owner) { }

        public struct HeroFromTheStormData
        {
            public string ID;
            public string SpawningVoice;
            public int PointCost;
        }

        public static List<HeroFromTheStormData> HeroesData = new List<HeroFromTheStormData>()
        {
            new HeroFromTheStormData() { ID = "MUTBORIS", SpawningVoice = "BorisSelect", PointCost = 1 },
            new HeroFromTheStormData() { ID = "MUTKRUKOV", SpawningVoice = "KrukovSelect", PointCost = 1 },
            new HeroFromTheStormData() { ID = "MUTRHAD", SpawningVoice = "AzizSelect", PointCost = 1 },
            new HeroFromTheStormData() { ID = "MUTREZNOV", SpawningVoice = "ReznovSelect", PointCost = 3 },
            new HeroFromTheStormData() { ID = "MUTASSN", SpawningVoice = "RahnSelect", PointCost = 3 },
            new HeroFromTheStormData() { ID = "MUTVOLKOV", SpawningVoice = "VolkovSelect", PointCost = 3 },
            new HeroFromTheStormData() { ID = "MUTUNDER", SpawningVoice = "UnderminerSelect", PointCost = 5 },
            new HeroFromTheStormData() { ID = "MUTLIBRA", SpawningVoice = "LibraSelect", PointCost = 5 },
            new HeroFromTheStormData() { ID = "MUTCYCOM", SpawningVoice = "KinAdSelect", PointCost = 5 },
        };

        private int HeroPool = 0;
        private TimerStruct PointTimer = new TimerStruct();
        private int HeroSpawnedThisGame = 0;

        int TakeOutHeroFromPool()
        {
            bool isHeroAvailable(int idx)
            {
                var data = HeroesData[idx];

                // 检查点数是否足够
                if (data.PointCost > HeroPool) return false;

                // 第一轮按顺序生成
                if (HeroSpawnedThisGame < HeroesData.Count)
                    return HeroSpawnedThisGame == idx;

                return true;
            }

            List<int> currentAvailableIdx = Enumerable.Range(0, HeroesData.Count).Where(isHeroAvailable).ToList();
            if (currentAvailableIdx.Count == 0) return -1;

            // 从可用的英雄中随机选择一个生成，并扣除相应的点数
            int idxToSpawn = ScenarioClass.GetRandomInList(currentAvailableIdx);
            HeroSpawnedThisGame++;
            HeroPool -= HeroesData[idxToSpawn].PointCost;
            return idxToSpawn;
        }

        public void AddHeroToAttackWave(AttackWave.AttackWave wave)
        {
            List<Pointer<TechnoClass>> heroes = new List<Pointer<TechnoClass>>();

            for (int i = 0; i < 6 && HeroPool > 0; ++i)
            {
                int heroIdx = TakeOutHeroFromPool();
                if (heroIdx < 0) break;
                var technoType = TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find(HeroesData[heroIdx].ID);
                var techno = technoType.Ref.Base.CreateObject(wave.OwnerHouse).Convert<TechnoClass>();
                heroes.Add(techno);
                wave.HeroesFromTheStormIdx.Add(heroIdx);
            }

            wave.AddMembers(heroes);
        }

        public static void AddVoiceEffects(AttackWave.AttackWave wave)
        {
            // 遍历波次中的英雄，为每个英雄创建延迟事件
            List<string> voices = new List<string>();
            foreach (var heroIdx in wave.HeroesFromTheStormIdx)
            {
                var heroData = HeroesData[heroIdx];
                
                // 跳过没有语音的英雄
                if (string.IsNullOrEmpty(heroData.SpawningVoice))
                    continue;

                voices.Add(heroData.SpawningVoice);
            }
            var voice = ScenarioClass.GetRandomInList(voices);

            // 创建延迟事件，存储英雄信息
            var delayedEvent = ScenarioExt.Global().CreateDecorator<HeroesFromTheStormVoice>(
                ScenarioExt.Global().FetchScenarioDecoratorID,
                "DelayedHeroVoiceEvent",
                voice
            );

            // *我不会退却，我不会动摇*
            delayedEvent.AddDelayedAction(
                Mutator.TimeToFrame(0, 0, 75),
                () => delayedEvent.PlayHeroVoice()
            );

            // *埃蒙叫*
            delayedEvent.AddDelayedAction(
                Mutator.TimeToFrame(0, 0, 120),
                () => VocClass.PlayGlobal("WhisperOfAmon")
            );
        }

        [Serializable]
        private class HeroesFromTheStormVoice : Mutator.DelayedGlobalEvent
        {
            private string HeroVoice { get; set; }

            public HeroesFromTheStormVoice(string heroVoice) : base()
            {
                HeroVoice = heroVoice;
            }

            public void PlayHeroVoice()
            {
                if (!string.IsNullOrEmpty(HeroVoice))
                    VocClass.PlayGlobal(HeroVoice);
            }
        }

        public override bool Update()
        {
            if (!base.Update())
                return false;

            (int Point, int NextDelay) getUpdateData()
            {
                int point = 0;
                int nextDelay = 0;
                int currentFrame = Game.CurrentFrame;

                if (currentFrame < TimeToFrame(4, 15))
                {
                    point = 1;
                    nextDelay = TimeToFrame(2, 0);
                }
                else if (currentFrame < TimeToFrame(14, 15))
                {
                    point = 4;
                    nextDelay = TimeToFrame(1, 30);
                }
                else if (currentFrame < TimeToFrame(20, 15))
                {
                    point = 3;
                    nextDelay = TimeToFrame(1, 0);
                }
                else
                {
                    point = 3;
                    nextDelay = TimeToFrame(0, 30);
                }

                return (point, nextDelay);
            }

            if (PointTimer.Expired())
            {
                var data = getUpdateData();
                HeroPool += data.Point;
                PointTimer.Start(data.NextDelay);
            }
            return true;
        }
    }
}
