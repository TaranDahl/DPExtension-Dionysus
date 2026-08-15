using DynamicPatcher;
using Extension.Decorators;
using Extension.Ext;
using Extension.Utilities;
using PatcherYRpp;
using InteropUtils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Extension.Mutators
{
    [Serializable]
    public class Shortsighted : BuffMutator
    {
        // Mutator
        public override string UIName => "短视症";
        public override string Description => "玩家单位及其建筑的视野范围缩短。";
        public override bool IsAvailableInRPG => false;
        public override int Score => 1;

        public Shortsighted(Pointer<HouseClass> owner) : base(owner) { }

        private static bool IsCallbackRegistered = false;
        private static readonly double SightReductionFactor = 0.4; // 视野缩小到 40%
        private static readonly double MinSight = 1.0; // 最小视野范围，防止完全看不见

        // BuffMutator
        protected override bool IsBuffEnemy => true;

        public override void Init(bool isInitial = true)
        {
            base.Init(isInitial);

            // 注册视野计算回调
            if (!IsCallbackRegistered)
            {
                PhobosTechnoExt.RegisterCalculateSightProvider(CalculateSight);
                IsCallbackRegistered = true;
            }

            // 生效时，给所有敌方单位贴buff
            foreach (var techno in TechnoClass.Array)
            {
                if (IsTechnoValid(techno))
                    BuffTechno(techno);
            }
        }

        public override void Uninit()
        {
            // 失效时移除所有敌方单位身上的buff
            foreach (var techno in TechnoClass.Array)
            {
                if (IsTechnoValid(techno))
                    UnbuffTechno(techno);
            }
            base.Uninit();
        }

        protected override void BuffTechno(Pointer<TechnoClass> techno)
        {
            var ext = TechnoExt.ExtMap.Find(techno);
            if (ext.Get(ShortsightedBuff.ID) == null)
                ext.CreateDecorator<ShortsightedBuff>(ShortsightedBuff.ID, "ShortsightedBuff");
        }

        protected override void UnbuffTechno(Pointer<TechnoClass> techno)
        {
            var ext = TechnoExt.ExtMap.Find(techno);
            ext.Remove(ShortsightedBuff.ID);
        }

        public override void OnTechnoCTOR(Pointer<TechnoClass> techno)
        {
            if (IsTechnoValid(techno))
                BuffTechno(techno);
        }

        public override void OnTechnoChangeOwner(Pointer<TechnoClass> techno, Pointer<HouseClass> toHouse)
        {
            if (!IsTechnoOwnerValid(toHouse))
                UnbuffTechno(techno);
            else
                BuffTechno(techno);
        }

        // Shortsighted
        private bool IsTechnoValid(Pointer<TechnoClass> techno)
        {
            if (!IsTechnoOwnerValid(techno.Ref.BaseAbstract.GetOwningHouse()))
                return false;

            return true;
        }

        private static double CalculateSight(Pointer<TechnoClass> pThis, double originalSight)
        {
            if (pThis.IsNull)
                return originalSight;

            // 检查单位是否有短视症 Buff
            var ext = TechnoExt.ExtMap.Find(pThis);
            if (ext != null)
            {
                var buff = ext.Get(ShortsightedBuff.ID);
                if (buff != null)
                {
                    // 应用视野缩小效果
                    return Math.Max(originalSight * SightReductionFactor, MinSight);
                }
            }

            return originalSight;
        }

        [Serializable]
        private class ShortsightedBuff : EventDecorator
        {
            // Decorator
            public static new DecoratorId ID => new DecoratorId((int)Mutator.TechnoDecoratorIDs.ShortsightedBuff);

            public ShortsightedBuff() { }
        }
    }
}
