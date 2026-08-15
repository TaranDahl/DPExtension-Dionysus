using DynamicPatcher;
using Extension.Ext;
using Extension.Script;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Mutators
{
    [Serializable]
    public class MutatorRhadScript : TechnoScriptable
    {
        public MutatorRhadScript(TechnoExt owner) : base(owner) { }

        // 时间参数（单位：帧）
        private int checkInterval = 0;          // 3秒检查一次
        private int globalCheckCounter = 0;      // 全局检查计数器
        
        // 治疗技能冷却
        private int healCooldown = 0;
        private int healCooldownMax = 0;

        // 常数
        private const int HealthThreshold = 150;  // 缺失血量阈值
        private const int HealAmount = 100;       // 恢复血量
        private static Pointer<WeaponTypeClass> DrugWeapon => WeaponTypeClass.ABSTRACTTYPE_ARRAY.Find("MutRhadDrug");

        public override void OnUpdate()
        {
            var pThis = Owner.OwnerObject;

            // 初始化时间参数
            if (checkInterval == 0)
            {
                checkInterval = Mutator.TimeToFrame(0, 3);      // 每3秒检查一次
                healCooldownMax = Mutator.TimeToFrame(0, 10);   // 10秒冷却
            }

            // 更新冷却时间
            if (healCooldown > 0)
                healCooldown--;

            // 定期检查是否可以释放技能
            globalCheckCounter++;
            if (globalCheckCounter >= checkInterval)
            {
                globalCheckCounter = 0;
                CheckAndCastHealSkill(pThis);
            }
        }

        private void CheckAndCastHealSkill(Pointer<TechnoClass> pThis)
        {
            // 检查单位是否有效
            if (pThis.Ref.Base.InLimbo)
                return;

            // 检查冷却
            if (healCooldown > 0)
                return;

            // 获取单位的生命值信息
            int maxHealth = pThis.Ref.GetTechnoType().Ref.Base.Strength;
            int currentHealth = pThis.Ref.Base.Health;
            int missingHealth = maxHealth - currentHealth;

            // 检查缺失血量是否达到阈值
            if (missingHealth < HealthThreshold)
                return;

            // 释放治疗技能
            CastHealSkill(pThis);
        }

        private void CastHealSkill(Pointer<TechnoClass> pThis)
        {
            // 1. 恢复自身100生命值
            int maxHealth = pThis.Ref.GetTechnoType().Ref.Base.Strength;
            int currentHealth = pThis.Ref.Base.Health;
            int newHealth = Math.Min(currentHealth + HealAmount, maxHealth);

            pThis.Ref.Base.Health = newHealth;

            // 2. 引爆武器MutRhadDrug
            if (DrugWeapon.IsNotNull)
                DrugWeapon.Ref.DetonateAtSelf(pThis);

            // 设置冷却
            healCooldown = healCooldownMax;
        }
    }
}
