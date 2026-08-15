using DynamicPatcher;
using Extension.Ext;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Decorators
{
    public class TechnoDecorative
    {
        static private int ReceiveDamageContext_OriginalHealth { get; set; }

        static public unsafe UInt32 OnUpdate(REGISTERS* R)
        {
            Pointer<TechnoClass> pTechno = (IntPtr)R->ESI;

            IDecorativeInterface<IEventDecorator> decorative = TechnoExt.ExtMap.Find(pTechno);
            foreach (var decorator in decorative.GetDecorators())
            {
                decorator.OnUpdate();
            }

            return 0;
        }

        static public unsafe UInt32 OnReceiveDamage(REGISTERS* R)
        {
            Pointer<TechnoClass> pTechno = (IntPtr)R->ECX;
            var pDamage = R->Stack<Pointer<int>>(0x4);
            var distanceFromEpicenter = R->Stack<int>(0x8);
            var pWH = R->Stack<Pointer<WarheadTypeClass>>(0xC);
            var pAttacker = R->Stack<Pointer<ObjectClass>>(0x10);
            var ignoreDefenses = R->Stack<bool>(0x14);
            var preventPassengerEscape = R->Stack<bool>(0x18);
            var pAttackingHouse = R->Stack<Pointer<HouseClass>>(0x1C);

            IDecorativeInterface<IEventDecorator> decorative = TechnoExt.ExtMap.Find(pTechno);
            foreach (var decorator in decorative.GetDecorators())
            {
                decorator.OnReceiveDamage(pDamage, distanceFromEpicenter, pWH, pAttacker, ignoreDefenses, preventPassengerEscape, pAttackingHouse);
            }

            ReceiveDamageContext_OriginalHealth = pTechno.Ref.Base.Health;

            return 0;
        }

        static public unsafe UInt32 AfterReceiveDamage(REGISTERS* R)
        {
            Pointer<TechnoClass> pTechno = (IntPtr)R->ESI;
            DamageState result = (DamageState)R->EAX;
            int damageDealt = ReceiveDamageContext_OriginalHealth - pTechno.Ref.Base.Health;
            var pDamage = R->Stack<Pointer<int>>(0xC4 + 0x4);
            var distanceFromEpicenter = R->Stack<int>(0xC4 + 0x8);
            var pWH = R->Stack<Pointer<WarheadTypeClass>>(0xC4 + 0xC);
            var pAttacker = R->Stack<Pointer<ObjectClass>>(0xC4 + 0x10);
            var pAttackerTechno = R->Stack<Pointer<TechnoClass>>(0xC4 + 0x10);
            var ignoreDefenses = R->Stack<bool>(0xC4 + 0x14);
            var preventPassengerEscape = R->Stack<bool>(0xC4 + 0x18);
            var pAttackingHouse = R->Stack<Pointer<HouseClass>>(0xC4 + 0x1C);

            IDecorativeInterface<IEventDecorator> decorative = TechnoExt.ExtMap.Find(pTechno);
            foreach (var decorator in decorative.GetDecorators())
            {
                result = decorator.AfterReceiveDamage(pDamage, distanceFromEpicenter, pWH, pAttacker, ignoreDefenses, preventPassengerEscape, pAttackingHouse, result, damageDealt);
            }

            if (pAttackerTechno.IsNotNull)
            {
                IDecorativeInterface<IEventDecorator> attackerDecorative = TechnoExt.ExtMap.Find(pAttackerTechno);
                foreach (var decorator in attackerDecorative.GetDecorators())
                {
                    result = decorator.OnDealDamage(pDamage, distanceFromEpicenter, pWH, pTechno, ignoreDefenses, preventPassengerEscape, pAttackingHouse, result, damageDealt);
                }
            }

            R->EAX = (uint)result;
            R->EDI = (uint)result;
            return 0;
        }

        static public unsafe UInt32 OnFire(REGISTERS* R)
        {
            Pointer<TechnoClass> pTechno = (IntPtr)R->ECX;
            var pTarget = R->Stack<Pointer<AbstractClass>>(0x4);
            var nWeaponIndex = R->Stack<int>(0x8);

            IDecorativeInterface<IEventDecorator> decorative = TechnoExt.ExtMap.Find(pTechno);
            foreach (var decorator in decorative.GetDecorators())
            {
                decorator.OnFire(pTarget, nWeaponIndex);
            }

            return 0;
        }
    }
}
