using DynamicPatcher;
using Extension.Ext;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using Extension.Mutators;

namespace ExtensionHooks
{
    public class MutatorHooks_TechnoClass
    {
        [Hook(HookType.AresHook, Address = 0x413F68, Size = 6)] // aircraft
        [Hook(HookType.AresHook, Address = 0x517CB0, Size = 6)] // infantry
        [Hook(HookType.AresHook, Address = 0x735774, Size = 5)] // unit
        [Hook(HookType.AresHook, Address = 0x43BCD5, Size = 5)] // building
        [Hook(HookType.AresHook, Address = 0x43BCE7, Size = 6)] // building
        static public unsafe UInt32 TechnoClass_CTOR_Mutator(REGISTERS* R)
        {
            var pItem = (Pointer<TechnoClass>)R->ESI;
            foreach (var mutator in Mutator.Array)
            {
                if (mutator is BuffMutator buffMutator)
                {
                    buffMutator.OnTechnoCTOR(pItem);
                }
            }
            return 0;
        }
        [Hook(HookType.AresHook, Address = 0x7014A0, Size = 8)]
        static public unsafe UInt32 TechnoClass_SetOwningHouse_Mutator(REGISTERS* R)
        {
            var pItem = (Pointer<TechnoClass>)R->ECX;
            var pToHouse = R->Stack<Pointer<HouseClass>>(0x4);
            foreach (var mutator in Mutator.Array)
            {
                if (mutator is BuffMutator buffMutator)
                {
                    buffMutator.OnTechnoChangeOwner(pItem, pToHouse);
                }
            }
            return 0;
        }
    }
    public class MutatorHooks_HouseClass
    {

    }
    public class MutatorHooks_SuperClass
    {
        [Hook(HookType.AresHook, Address = 0x6CC390, Size = 6)]
        public static unsafe UInt32 SuperClass_Launch_Mutator(REGISTERS* R)
        {
            var pItem = (Pointer<SuperClass>)R->ECX;
            var mutatorToActivate = SuperWeaponTypeExt.ExtMap.Find(pItem.Ref.Type).ActivateMutator;
            if (mutatorToActivate != null)
            {
                var owner = pItem.Ref.Owner;
                var mutator = Mutator.CreateMutator(mutatorToActivate, Pointer<HouseClass>.Zero);
                if (mutator != null)
                {
                    mutator.Init();
                }
            }
            var bPlus = SuperWeaponTypeExt.ExtMap.Find(pItem.Ref.Type).ActivateBrutalPlus;
            if (bPlus >= 1 && bPlus <= 6)
            {
                var mutators = MutatorRandomizer.BrutalPlusRandom(bPlus);
                foreach (var name in mutators)
                {
                    MutatorRandomizer.ActiveMutatorByName(name);
                }
                var sw = SuperWeaponTypeClass.ABSTRACTTYPE_ARRAY.Find("MutGameStartSpecial");
                var player = MutatorRandomizer.FindFirstPlayer();
                player.Ref.FindSuperWeapon(sw).Ref.Launch(player.Ref.BaseSpawnCell, false);
            }
            return 0;
        }
    }
    public class MutatorHooks_Misc
    {
        [Hook(HookType.AresHook, Address = 0x55DC99, Size = 5)]
        public static unsafe UInt32 Game_MainLoop_Mutator(REGISTERS* R)
        {
            for (; Mutator.ToBeAddedArray.Count > 0; )
            {
                var mutator = Mutator.ToBeAddedArray[0];
                Mutator.ToBeAddedArray.RemoveAt(0);
                Mutator.Array.Add(mutator);
            }
            foreach (var mutator in Mutator.Array)
            {
                mutator.Update();
            }
            for (; Mutator.ToBeRemovedArray.Count > 0; )
            {
                var mutator = Mutator.ToBeRemovedArray[0];
                Mutator.ToBeRemovedArray.RemoveAt(0);
                Mutator.Array.Remove(mutator);
            }
            return 0;
        }

        [Hook(HookType.AresHook, Address = 0x6E2281, Size = 5)]
        public static unsafe UInt32 TActionClass_ExecuteResizePlayerView_Mutator(REGISTERS* R)
        {
            TargetCellMutator.OnUsableAreaChange();
            return 0;
        }

        [Hook(HookType.AresHook, Address = 0x56C020, Size = 5)]
        public static unsafe UInt32 MapClass_CrateCollected_Mutator(REGISTERS* R)
        {
            var mapCrd = R->Stack<Pointer<CellStruct>>(0x4);
            SlimPackings.OnCrateCollected(mapCrd.Ref);
            return 0;
        }

        [Hook(HookType.AresHook, Address = 0x693366, Size = 10)]
        [Hook(HookType.AresHook, Address = 0x6932A4, Size = 10)]
        [Hook(HookType.AresHook, Address = 0x6931F9, Size = 10)]
        [Hook(HookType.AresHook, Address = 0x693126, Size = 10)]
        public static unsafe UInt32 MouseButtonClick_Mutator(REGISTERS* R)
        {
            MicroTransactions.MouseButtonClick();
            return 0;
        }

        [Hook(HookType.AresHook, Address = 0x65FA70, Size = 6)]
        public static unsafe UInt32 RadarEventClass_Create_Mutator(REGISTERS* R)
        {
            var mapCrd = R->Stack<CellStruct>(0x4);
            if (Darkness.ShouldHideMapEvent(mapCrd))
            {
                R->EAX = 0;
                return 0x65FB52;
            }
            return 0;
        }

        [Hook(HookType.AresHook, Address = 0x55DEE0, Size = 5)]
        public static unsafe UInt32 Game_KeyboardProcess_BoomBots(REGISTERS* R)
        {
            return Extension.Mutators.BoomBots.KeyboardProcessBridge(R);
        }

        [Hook(HookType.AresHook, Address = 0x6F64A0, Size = 5)]
        public static unsafe UInt32 TechnoClass_DrawHealthBar_BoomBots(REGISTERS* R)
        {
            return Extension.Mutators.BoomBots.DrawHealthBarBridge(R);
        }
    }
    public class MutatorHooks_Test
    {
        //[Hook(HookType.AresHook, Address = 0x5F65F0, Size = 6)]
        //public static unsafe UInt32 ObjectClass_UnInit_Log(REGISTERS* R)
        //{
        //    var pItem = (Pointer<ObjectClass>)R->ECX;
        //    if (pItem.Ref.Base.WhatAmI() == AbstractType.Building)
        //    {
        //        var ret = R->Stack<int>(0);
        //        Logger.Log(ret);
        //    }
        //    return 0;
        //}
        //[Hook(HookType.AresHook, Address = 0x4DBDF0, Size = 6)]
        //public static unsafe UInt32 FootClass_GetDestination_Test(REGISTERS* R)
        //{
        //    var pItem = (Pointer<FootClass>)R->ECX;
        //    var pLinkedTo = pItem.Ref.Locomotor.ToLocomotionClass().Ref.LinkedTo;
        //    if (pLinkedTo.IsNull)
        //    {
        //        Logger.Log("Error!");
        //        Logger.Log(pItem.Ref.Base.GetTechnoType().Ref.BaseAbstractType.ID);
        //        Logger.Log(pItem);
        //    }
        //    return 0;
        //}
    }
}
