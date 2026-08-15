using DynamicPatcher;
using Extension.Script;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Extension.Mutators
{
    [Serializable]
    public class ChaosStudios : Mutator
    {
        public override string UIName => "混乱工作室";
        public override string Description => "突变因子会随机选择，并且在任务中周期性轮换。";
        public override bool IsAvailableInRPG => false;
        public override int Score => 0;

        private const int SlotCount = 3;
        private int FirstRotationDelay = Mutator.TimeToFrame(4, 30);
        private int RotationInterval = Mutator.TimeToFrame(1, 30);
        private int RotationEndDelay = Mutator.TimeToFrame(0, 15);

        private Mutator[] activeMutators = new Mutator[SlotCount];
        private int removeMutatorTimer = Mutator.TimeToFrame(1, 30);
        private int rotationEndTimer = 0;

        public ChaosStudios(Pointer<HouseClass> owner) : base(owner) { }

        public override void Init(bool isInitial = true)
        {
            base.Init(isInitial);
            for (int i = 0; i < SlotCount; i++)
            {
                activeMutators[i] = ActiveRandomMutator();
            }
        }

        public override void Uninit()
        {
            // 销毁所有活跃的因子
            for (int i = 0; i < SlotCount; i++)
            {
                if (activeMutators[i] != null)
                {
                    Mutator.DestroyMutator(activeMutators[i]);
                    activeMutators[i] = null;
                }
            }
            base.Uninit();
        }

        public override bool Update()
        {
            if (!base.Update())
                return false;

            if (Game.CurrentFrame < FirstRotationDelay)
            {
                return true;    // 等待第一次轮换的时间
            }

            removeMutatorTimer++;
            rotationEndTimer++;

            if (removeMutatorTimer >= RotationInterval)
            {
                RotationBegin();
            }
            
            if (rotationEndTimer == RotationEndDelay)
            {
                RotationEnd();
            }

            return true;
        }

        private Mutator ActiveRandomMutator()
        {
            var types = MutatorRandomizer.AvailableMutatorsForRandom.ToList();
            if (types == null || types.Count == 0)
            {
                Logger.Log("No available mutators for random selection.");
                return null;
            }

            foreach (var mutator in Mutator.Array)
            {
                types.Remove(mutator.GetType());
            }

            if (types.Count == 0)
            {
                Logger.Log("All mutators are currently active, cannot select a new one.");
                return null;
            }

            var mutatorType = ScenarioClass.GetRandomInList(types);
            if (mutatorType == null)
            {
                Logger.Log("Failed to select a random mutator type.");
                return null;
            }

            var result = Mutator.CreateMutator(mutatorType, Owner);
            if (result == null)
            {
                Logger.Log("Failed to create a mutator instance.");
                return null;
            }
            result.Init();
            return result;
        }
        private void RotationBegin()
        {
            activeMutators[0].Uninit();
            for (int i = 0; i < SlotCount - 1; i++)
            {
                activeMutators[i] = activeMutators[i + 1];
            }
            removeMutatorTimer = 0;
            rotationEndTimer = 0;
        }
        private void RotationEnd()
        {
            activeMutators[SlotCount - 1] = ActiveRandomMutator();
        }
    }
}
