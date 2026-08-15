using DynamicPatcher;
using Extension.Decorators;
using Extension.Ext;
using Extension.Utilities;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using InteropUtils;

namespace Extension.Mutators
{
    [Serializable]
    public class Transmutation : BuffMutator
    {
        // Mutator
        public override string UIName => "力量蜕变";
        public override string Description => "敌方单位造成伤害时有一定几率变形成更强大的单位。";
        public override bool IsAvailableInRPG => true;
        public override int Score => 7;
        public Transmutation(Pointer<HouseClass> owner) : base(owner) { }
        public override void Init(bool isInitial = true)
        {
            base.Init();
            // 生效时，给所有友方单位概率贴buff
            foreach (var techno in TechnoClass.Array)
            {
                if (IsTechnoValid(techno))
                    BuffTechno(techno);
            }
        }
        public override void Uninit()
        {
            // 失效时移除所有友方单位身上的buff
            foreach (var techno in TechnoClass.Array)
            {
                if (IsTechnoValid(techno))
                    UnbuffTechno(techno);
            }
            base.Uninit();
        }

        // BuffMutator
        protected override bool IsBuffEnemy => false;
        protected override void BuffTechno(Pointer<TechnoClass> techno)
        {
            var ext = TechnoExt.ExtMap.Find(techno);
            if (ext.Get(TransmutationBuff.ID) == null)
                ext.CreateDecorator<TransmutationBuff>(TransmutationBuff.ID, "TransmutationBuff");
        }
        protected override void UnbuffTechno(Pointer<TechnoClass> techno)
        {
            var ext = TechnoExt.ExtMap.Find(techno);
            ext.Remove(TransmutationBuff.ID);
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

        // Transmutation
        private static List<List<List<string>>> TransmutationTable => MutatorCacheManager.Instance.TechnoLevelTable;
        private bool IsTechnoValid(Pointer<TechnoClass> techno)
        {
            if (!IsTechnoOwnerValid(techno.Ref.BaseAbstract.GetOwningHouse()))
                return false;

            if ((techno.Ref.BaseAbstract.AbstractFlags & AbstractFlags.Foot) == AbstractFlags.None)
                return false;

            return true;
        }

        [Serializable]
        private class TransmutationBuff : EventDecorator
        {
            // static 
            private static Pointer<AnimTypeClass> TransmutationAnim => AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("TransmutationAnim");

            // Decorator
            public static new DecoratorId ID => new DecoratorId((int)TechnoDecoratorIDs.TransmutationBuff);

            // EventDecorator
            public override DamageState OnDealDamage(Pointer<int> pDamage, int DistanceFromEpicenter, Pointer<WarheadTypeClass> pWH, Pointer<TechnoClass> pVictim, bool IgnoreDefenses, bool PreventPassengerEscape, Pointer<HouseClass> pAttackingHouse, DamageState result, int damageDealt)
            {
                // 必须有伤害
                if (result == DamageState.Unaffected)
                    return result;
                if (damageDealt <= 0)
                    return result;
                // 内置CD1秒
                if (TransmutationCoolDownTimer.InProgress())
                    return result;
                // 不变建筑
                var pThis = (Decorative as TechnoExt).OwnerObject;
                if ((pThis.Ref.BaseAbstract.AbstractFlags & AbstractFlags.Foot) == AbstractFlags.None)
                    return result;

                if (result == DamageState.NowDead)
                {
                    var level = GetLevel(pVictim.Ref.GetTechnoType());
                    TryTransmutation(pThis, 1 + level);
                }
                else
                {
                    if (ScenarioClass.Instance.Random.RandomChance(1 + damageDealt, 201))
                        TryTransmutation(pThis, 1);
                }
                return result;
            }

            // TransmutationBuff
            private TimerStruct TransmutationCoolDownTimer = new TimerStruct();
            private bool TryTransmutation(Pointer<TechnoClass> attacker, uint upgradedLevel)
            {
                TransmutationCoolDownTimer.Start(TimeToFrame(0, 1));
                var attackerType = attacker.Ref.GetTechnoType();
                int rttiIdx = GetRttiIdx(attackerType);
                if (rttiIdx < 0 || rttiIdx > 2)
                    return false;
                var costLists = TransmutationTable[rttiIdx];
                int curLevel = (int)GetLevel(attackerType);
                int toLevel = Math.Min(curLevel + (int)upgradedLevel, costLists.Count - 1);

                // 如果目标等级没有能变的，那就往下一等级，直到找到能变的，或者回到本身所在等级
                for (; curLevel < toLevel; toLevel--)
                {
                    var curCostList = costLists[toLevel];
                    var canConvertTypes = new List<Pointer<TechnoTypeClass>>();
                    foreach (var name in curCostList)
                    {
                        var toType = TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find(name);
                        if (toType.IsNull)
                        {
                            Logger.Log("Can't find type \"{0}\"!", name);
                            continue;
                        }
                        if (!CanDoTransmutation(attacker, toType))
                            continue;
                        canConvertTypes.Add(toType);
                    }
                    if (canConvertTypes.Count > 0)
                    {
                        var toType = ScenarioClass.GetRandomInList(canConvertTypes);
                        // 变形
                        PhobosTechnoExt.ConvertToType(attacker.Convert<FootClass>(), toType);
                        // 回满血
                        attacker.Ref.Base.Health = toType.Ref.Base.Strength;
                        // 图像效果
                        var anim = YRMemory.Create<AnimClass>(TransmutationAnim, attacker.Ref.BaseAbstract.GetCoords());
                        anim.Ref.SetOwnerObject(attacker.Convert<ObjectClass>());
                        return true;
                    }
                }
                return false;
            }
            private bool CanDoTransmutation(Pointer<TechnoClass> techno, Pointer<TechnoTypeClass> toType)
            {
                var cell = MapClass.Instance.GetCellAt(techno.Convert<AbstractClass>());
                // 避免卡住，目标类型必须能走在这里
                if (!cell.Ref.IsClearToMove(toType.Ref.SpeedType, toType.Ref.MovementZone, true, true))
                    return false;
                return true;
            }
        }
    }
}
