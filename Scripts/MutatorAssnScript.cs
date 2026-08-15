using System;
using System.Collections.Generic;
using DynamicPatcher;
using PatcherYRpp;
using Extension.Ext;
using Extension.Script;
using Extension.Utilities;
using System.Linq;
using Extension.Mutators;

namespace Extension.Mutators
{
    [Serializable]
    public class MutatorAssn : TechnoScriptable
    {
        private enum SkillStatus
        {
            Idle = 0,
            Casting = 1
        }

        public MutatorAssn(TechnoExt owner) : base(owner) { }

        // 时间参数
        private int searchInterval = 0;          // 每3秒检查一次
        private int globalCheckCounter = 0;      // 全局检查计数器
        private int skillBlockCounter = 0;       // 技能阻止计数器
        private int skillBlockDuration = 0;      // 前一个技能完成后2秒内不释放新技能

        // 技能1：爆虫冲锋
        private SkillStatus banelingChargeStatus = SkillStatus.Idle;
        private int banelingChargeCooldown = 0;
        private int banelingChargeCooldownMax = 0;
        private int banelingChargeCounter = 0;
        private SwizzleablePointer<CellClass> banelingTargetCell = new SwizzleablePointer<CellClass>(Pointer<CellClass>.Zero);
        private int banelingShotCounter = 0;
        private int banelingShotMax = 0;

        // 技能2：召唤弓箭手
        private int summonArchersCooldown = 0;
        private int summonArchersCooldownMax = 0;
        private static Pointer<InfantryTypeClass> ArcherType => InfantryTypeClass.ABSTRACTTYPE_ARRAY.Find("MUTASSNHARP");

        // 技能3：群体狂暴
        private int massRageCooldown = 0;
        private int massRageCooldownMax = 0;
        private static Pointer<WeaponTypeClass> RageWeapon => WeaponTypeClass.ABSTRACTTYPE_ARRAY.Find("MutAssnRage");
        private static Pointer<AnimTypeClass> RageAnim => AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("MINDHAZEBUM");

        // 技能4：狂兽人崛起
        private SkillStatus bruteRisingStatus = SkillStatus.Idle;
        private int bruteRisingCooldown = 0;
        private int bruteRisingCooldownMax = 0;
        private int bruteRisingCounter = 0;
        private int bruteRisingTriggerCount = 0;
        private SwizzleablePointer<CellClass> bruteRisingTargetCell = new SwizzleablePointer<CellClass>(Pointer<CellClass>.Zero);
        private static Pointer<AnimTypeClass> GenDeathAnim => AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("GENDEATH");

        public override void OnUpdate()
        {
            var pThis = Owner.OwnerObject;

            // 初始化时间参数
            if (searchInterval == 0)
            {
                searchInterval = Mutator.TimeToFrame(0, 3);        // 每3秒检查一次
                skillBlockDuration = Mutator.TimeToFrame(0, 2);    // 技能阻止2秒
                banelingChargeCooldownMax = Mutator.TimeToFrame(0, 15);
                summonArchersCooldownMax = Mutator.TimeToFrame(0, 30);
                massRageCooldownMax = Mutator.TimeToFrame(2, 0);
                bruteRisingCooldownMax = Mutator.TimeToFrame(1, 0);
                banelingShotMax = 4;
            }

            // 更新冷却时间
            if (banelingChargeCooldown > 0)
                banelingChargeCooldown--;
            if (summonArchersCooldown > 0)
                summonArchersCooldown--;
            if (massRageCooldown > 0)
                massRageCooldown--;
            if (bruteRisingCooldown > 0)
                bruteRisingCooldown--;

            // 更新技能阻止计数器
            if (skillBlockCounter > 0)
                skillBlockCounter--;

            // 更新持续施法技能
            UpdatebanelingCharge(pThis);
            UpdatebruteRising(pThis);

            // 每3秒检查一次所有技能
            globalCheckCounter++;
            if (globalCheckCounter >= searchInterval)
            {
                globalCheckCounter = 0;
                
                // 如果有技能阻止，不释放新技能
                if (skillBlockCounter <= 0)
                {
                    CheckAndCastSkills(pThis);
                }
            }
        }

        // ========== 技能检查和施放 ==========
        private void CheckAndCastSkills(Pointer<TechnoClass> pThis)
        {
            var pMyHouse = pThis.Ref.BaseAbstract.GetOwningHouse();

            // 第一步：预筛选所有可能有效的目标
            var list = pThis.Ref.GetPresentNearBy(10);
            // 收集所有可用的地面敌方单位
            List<(Pointer<TechnoClass> techno, CellStruct mapCrd)> validGroundEnemies = new List<(Pointer<TechnoClass>, CellStruct)>();
            // 收集所有友军
            List<(Pointer<TechnoClass> techno, CellStruct mapCrd)> allies = new List<(Pointer<TechnoClass>, CellStruct)>();

            foreach (var techno in list)
            {
                var owner = techno.Ref.BaseAbstract.GetOwningHouse();
                var technoMapCrd = techno.Ref.BaseAbstract.GetMapCrd();

                // 收集敌方单位
                if (!pMyHouse.Ref.IsAlliedWith(owner) && !techno.Ref.IsAirUnit())
                {
                    validGroundEnemies.Add((techno, technoMapCrd));
                }
                else
                {
                    allies.Add((techno, technoMapCrd));
                }
            }

            // 第二步：检查所有可用技能，按优先级4321排序
            List<int> availableSkills = new List<int>();
            var targetMapCrd = CellStruct.Empty;

            // 技能4：狂兽人崛起
            if (bruteRisingStatus == SkillStatus.Idle && bruteRisingCooldown <= 0)
            {
                if (CanCastbruteRising(pThis, out targetMapCrd, validGroundEnemies))
                    availableSkills.Add(4);
            }

            // 技能3：群体狂暴
            if (massRageCooldown <= 0 && pThis.Ref.Target.IsNotNull)
            {
                if (CanCastMassRage(pThis, allies))
                    availableSkills.Add(3);
            }

            // 技能2：召唤弓箭手
            if (summonArchersCooldown <= 0 && pThis.Ref.Target.IsNotNull)
            {
                availableSkills.Add(2);
            }

            // 技能1：爆虫冲锋
            if (banelingChargeStatus == SkillStatus.Idle && banelingChargeCooldown <= 0 && pThis.Ref.Target.IsNotNull)
            {
                if (CanCastbanelingCharge(pThis,out targetMapCrd, validGroundEnemies))
                    availableSkills.Add(1);
            }

            // 第三步：按优先级施放第一个可用技能
            if (availableSkills.Count > 0)
            {
                int skillTocast = availableSkills[0];
                switch (skillTocast)
                {
                    case 1:
                        CastbanelingCharge(pThis, targetMapCrd, validGroundEnemies);
                        break;
                    case 2:
                        CastSummonArchers(pThis);
                        break;
                    case 3:
                        CastMassRage(pThis, allies);
                        break;
                    case 4:
                        CastbruteRising(pThis, targetMapCrd, validGroundEnemies);
                        break;
                }
            }
        }

        // ========== 技能1：爆虫冲锋 ==========
        private bool CanCastbanelingCharge(Pointer<TechnoClass> pThis, out CellStruct targetMapCrd, List<(Pointer<TechnoClass>, CellStruct)> validGroundEnemies)
        {
            targetMapCrd = CellStruct.Empty;
            var myMapCrd = pThis.Ref.BaseAbstract.GetMapCrd();
            // 搜索范围7格
            var cellEnum = new CellSpreadEnumerator(7);
            var availableTargets = new List<CellStruct>();

            foreach (var offset in cellEnum)
            {
                var checkMapCrd = myMapCrd + offset;
                int groundEnemyCount = 0;
                bool hasEnemyOnCell = false;

                // 统计周围2格内的敌方地面单位
                foreach (var (techno, technoMapCrd) in validGroundEnemies)
                {
                    if (technoMapCrd == checkMapCrd)
                        hasEnemyOnCell = true;

                    if (technoMapCrd.DistanceFrom(checkMapCrd) <= 2)
                        groundEnemyCount++;

                    // 周围敌方地面单位超过2个且格子上有敌方
                    if (hasEnemyOnCell && groundEnemyCount > 2)
                    {
                        availableTargets.Add(checkMapCrd);
                        break;
                    }
                }
            }

            if (availableTargets.Count > 0)
            {
                targetMapCrd = ScenarioClass.GetRandomInList(availableTargets);
                return true;
            }
            else
            {
                return false;
            }
        }

        private void CastbanelingCharge(Pointer<TechnoClass> pThis, CellStruct targetMapCrd, List<(Pointer<TechnoClass>, CellStruct)> validGroundEnemies)
        {
            banelingTargetCell = new SwizzleablePointer<CellClass>(MapClass.Instance.GetCellAt(targetMapCrd));
            banelingChargeStatus = SkillStatus.Casting;
            banelingChargeCounter = 0;
            banelingShotCounter = 0;

            pThis.Ref.BaseMission.OverrideMission(Mission.Attack, banelingTargetCell.Pointer.Convert<AbstractClass>());
            skillBlockCounter = skillBlockDuration;
        }

        private void UpdatebanelingCharge(Pointer<TechnoClass> pThis)
        {
            if (banelingChargeStatus != SkillStatus.Casting)
                return;

            banelingChargeCounter++;
            skillBlockCounter = skillBlockDuration;
            
            // 每0.25秒（7.5帧左右）开火一次，共4次
            int fireInterval = Mutator.TimeToFrame(0, 0.25);
            if (banelingChargeCounter % fireInterval == 0 && banelingShotCounter < banelingShotMax)
            {
                pThis.Ref.Fire(banelingTargetCell.Pointer.Convert<AbstractClass>(), 1);
                banelingShotCounter++;
            }

            // 4次射击完毕后进入冷却
            if (banelingShotCounter >= banelingShotMax)
            {
                banelingChargeStatus = SkillStatus.Idle;
                banelingChargeCooldown = banelingChargeCooldownMax;
                pThis.Ref.SetTarget(Pointer<AbstractClass>.Zero);
                banelingTargetCell = new SwizzleablePointer<CellClass>(Pointer<CellClass>.Zero);
            }
        }

        // ========== 技能2：召唤弓箭手 ==========
        private void CastSummonArchers(Pointer<TechnoClass> pThis)
        {
            if (ArcherType.IsNull)
                return;

            var pHouse = pThis.Ref.BaseAbstract.GetOwningHouse();
            var myMapCrd = pThis.Ref.BaseAbstract.GetMapCrd();

            // 在相邻9个格子召唤共8个弓箭手
            List<CellStruct> summonCells = new List<CellStruct>
            {
                myMapCrd + new CellStruct(1, 1),
                myMapCrd + new CellStruct(1, 0),
                myMapCrd + new CellStruct(1, -1),
                myMapCrd + new CellStruct(0, -1),
                myMapCrd + new CellStruct(-1, -1),
                myMapCrd + new CellStruct(-1, 0),
                myMapCrd + new CellStruct(-1, 1),
                myMapCrd + new CellStruct(0, 1)
            };

            foreach (var cell in summonCells)
            {
                var infantry = ArcherType.Ref.BaseObjectType.CreateObject(pThis.Ref.BaseAbstract.GetOwningHouse()).Convert<InfantryClass>();
                infantry.Ref.BaseObject.Put(MapClass.Instance.GetCellAt(CellClass.Cell2Coord(cell)).Ref.Base.GetCoords(), DirType.North);
                infantry.Ref.BaseTechno.Veterancy.SetElite(true);
                infantry.Ref.BaseObject.Scatter();
            }

            summonArchersCooldown = summonArchersCooldownMax;
            skillBlockCounter = skillBlockDuration;
        }

        // ========== 技能3：群体狂暴 ==========
        private bool CanCastMassRage(Pointer<TechnoClass> pThis, List<(Pointer<TechnoClass>, CellStruct)> allies)
        {
            var myMapCrd = pThis.Ref.BaseAbstract.GetMapCrd();
            int allyCount = 0;
            foreach (var (techno, technoMapCrd) in allies)
            {
                if (technoMapCrd.DistanceFrom(myMapCrd) <= 5)
                    allyCount++;
            }
            return allyCount > 5;
        }

        private void CastMassRage(Pointer<TechnoClass> pThis, List<(Pointer<TechnoClass>, CellStruct)> allies)
        {
            if (RageWeapon.IsNotNull)
            {
                RageWeapon.Ref.DetonateAtSelf(pThis);
            }

            if (RageAnim.IsNotNull)
            {
                YRMemory.Create<AnimClass>(RageAnim, pThis.Ref.BaseAbstract.GetCoords());
            }

            massRageCooldown = massRageCooldownMax;
            skillBlockCounter = skillBlockDuration;
        }

        // ========== 技能4：狂兽人崛起 ==========
        private bool CanCastbruteRising(Pointer<TechnoClass> pThis, out CellStruct targetMapCrd, List<(Pointer<TechnoClass>, CellStruct)> validGroundEnemies)
        {
            targetMapCrd = CellStruct.Empty;
            var availableTargets = new List<CellStruct>();
            var myMapCrd = pThis.Ref.BaseAbstract.GetMapCrd();
            // 搜索范围7格
            var cellEnum = new CellSpreadEnumerator(7);

            foreach (var offset in cellEnum)
            {
                var checkMapCrd = myMapCrd + offset;
                int groundEnemyCount = 0;
                bool hasEnemyOnCell = false;

                foreach (var (techno, technoMapCrd) in validGroundEnemies)
                {
                    if (technoMapCrd == checkMapCrd)
                        hasEnemyOnCell = true;

                    if (technoMapCrd.DistanceFrom(checkMapCrd) <= 5)
                        groundEnemyCount++;

                    // 周围敌方地面单位超过2个且格子上有敌方
                    if (hasEnemyOnCell && groundEnemyCount > 2)
                    {
                        availableTargets.Add(checkMapCrd);
                        break;
                    }
                }
            }

            if (availableTargets.Count > 0)
            {
                targetMapCrd = ScenarioClass.GetRandomInList(availableTargets);
                return true;
            }
            else
            {
                return false;
            }
        }

        private void CastbruteRising(Pointer<TechnoClass> pThis, CellStruct targetMapCrd, List<(Pointer<TechnoClass>, CellStruct)> validGroundEnemies)
        {
            bruteRisingTargetCell = new SwizzleablePointer<CellClass>(MapClass.Instance.GetCellAt(targetMapCrd));
            bruteRisingStatus = SkillStatus.Casting;
            bruteRisingCounter = 0;
            bruteRisingTriggerCount = 0;
            skillBlockCounter = skillBlockDuration;
        }

        private void UpdatebruteRising(Pointer<TechnoClass> pThis)
        {
            if (bruteRisingStatus != SkillStatus.Casting)
                return;

            bruteRisingCounter++;
            
            // 每0.25秒触发一次，共10次
            int triggerInterval = Mutator.TimeToFrame(0, 0.25);
            if (bruteRisingCounter % triggerInterval == 0 && bruteRisingTriggerCount < 10)
            {
                var targetMapCrd = bruteRisingTargetCell.Ref.MapCoords;
                
                // 在目标区域附近5格范围内随机一个格子创建动画
                var cellEnum = new CellSpreadEnumerator(5);
                List<CellStruct> possibleCells = new List<CellStruct>();
                
                foreach (var offset in cellEnum)
                {
                    possibleCells.Add(targetMapCrd + offset);
                }

                if (possibleCells.Count > 0)
                {
                    var randomCell = ScenarioClass.GetRandomInList(possibleCells);
                    var anim = YRMemory.Create<AnimClass>(GenDeathAnim, MapClass.Instance.GetCellAt(randomCell).Ref.Base.GetCoords());
                    anim.Ref.Owner = pThis.Ref.BaseAbstract.GetOwningHouse();
                    anim.Ref.LightConvert = ColorScheme.Array[pThis.Ref.BaseAbstract.GetOwningHouse().Ref.ColorSchemeIndex].Ref.LightConvertPtr;
                }

                bruteRisingTriggerCount++;
            }

            // 10次触发完毕后进入冷却
            if (bruteRisingTriggerCount >= 10)
            {
                bruteRisingStatus = SkillStatus.Idle;
                bruteRisingCooldown = bruteRisingCooldownMax;
                bruteRisingTargetCell = new SwizzleablePointer<CellClass>(Pointer<CellClass>.Zero);
            }
        }
    }
}
