using DynamicPatcher;
using Extension.Ext;
using Extension.Script;
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
    public class MutatorCycom : TechnoScriptable
    {
        private enum RecoveryStatus
        {
            Idle = 0,
            Recovering = 1,
            Cooldown = 2,
        }

        public MutatorCycom(TechnoExt owner) : base(owner) { }

        private RecoveryStatus recoveryStatus = RecoveryStatus.Idle;
        private int recoveryCounter = 0;
        private int shieldDuration = 0;      // 3秒铁幕
        private int cooldownDuration = 0;    // 60秒冷却
        private static Pointer<WeaponTypeClass> RecoveryWeapon => WeaponTypeClass.ABSTRACTTYPE_ARRAY.Find("MutCycomRecovery");

        public override void OnUpdate()
        {
            // 初始化时间参数
            if (shieldDuration == 0)
            {
                shieldDuration = Mutator.TimeToFrame(0, 3);      // 3秒
                cooldownDuration = Mutator.TimeToFrame(1, 0);    // 60秒
            }

            var pThis = Owner.OwnerObject;

            switch (recoveryStatus)
            {
                case RecoveryStatus.Recovering:
                    UpdateRecoveryStatus(pThis);
                    break;
                case RecoveryStatus.Cooldown:
                    UpdateCooldownStatus();
                    break;
            }
        }

        public override DamageState AfterReceiveDamage(Pointer<int> pDamage, int DistanceFromEpicenter, Pointer<WarheadTypeClass> pWH, Pointer<ObjectClass> pAttacker, bool IgnoreDefenses, bool PreventPassengerEscape, Pointer<HouseClass> pAttackingHouse, DamageState result, int damageTaken)
        {
            // 必须是非系统伤害才触发
            if (IgnoreDefenses)
                return result;

            // 只处理致命伤害
            if (result != DamageState.NowDead)
                return result;

            // 技能在冷却中，不触发
            if (recoveryStatus == RecoveryStatus.Cooldown)
                return result;

            // 触发被动技能
            var pThis = Owner.OwnerObject;
            TriggerRecovery(pThis);

            // 免疫该次伤害
            pThis.Ref.Base.Health = damageTaken;
            pThis.Ref.Base.IsAlive = true;
            return DamageState.Unaffected;
        }

        private void TriggerRecovery(Pointer<TechnoClass> pThis)
        {
            // 进入匍匐状态
            var infantry = pThis.Convert<InfantryClass>();
            infantry.Ref.Crawling = true;
            infantry.Ref.PlayAnim((int)SequenceAnimType.CRAWL, true, 0);

            // 获得3秒铁幕
            pThis.Ref.IronCurtain(Mutator.TimeToFrame(0, 3));

            // 进入恢复状态
            recoveryStatus = RecoveryStatus.Recovering;
            recoveryCounter = 0;
        }

        private void UpdateRecoveryStatus(Pointer<TechnoClass> pThis)
        {
            recoveryCounter++;

            // 3秒后解除匍匐状态、恢复生命值、引爆回收武器
            if (recoveryCounter >= shieldDuration)
            {
                var infantry = pThis.Convert<InfantryClass>();
                infantry.Ref.Crawling = false;
                infantry.Ref.PlayAnim((int)SequenceAnimType.STAND_READY, true, 0);

                // 恢复全部生命值
                var maxHealth = pThis.Ref.GetTechnoType().Ref.Base.Strength;
                pThis.Ref.Base.Health = maxHealth;

                // 复苏爆炸
                if (RecoveryWeapon.IsNotNull)
                {
                    RecoveryWeapon.Ref.DetonateAtSelf(pThis);
                }

                // 进入冷却状态
                recoveryStatus = RecoveryStatus.Cooldown;
                recoveryCounter = 0;
            }
        }

        private void UpdateCooldownStatus()
        {
            recoveryCounter++;

            // 60秒冷却后回到空闲
            if (recoveryCounter >= cooldownDuration)
            {
                recoveryStatus = RecoveryStatus.Idle;
                recoveryCounter = 0;
            }
        }
    }
}
