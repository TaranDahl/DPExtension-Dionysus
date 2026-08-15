
using System;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DynamicPatcher;
using PatcherYRpp;
using Extension.Ext;
using Extension.Script;

namespace ExtensionHooks
{
    public class ScenarioExtHooks
    {
        [Hook(HookType.AresHook, Address = 0x683549, Size = 9)]
        static public unsafe UInt32 ScenarioClass_CTOR(REGISTERS* R)
        {
            Logger.Log("Scenario ext ctor.");
            return ScenarioExt.ScenarioClass_CTOR(R);
        }

        [Hook(HookType.AresHook, Address = 0x6BEB7D, Size = 6)]
        static public unsafe UInt32 ScenarioClass_DTOR(REGISTERS* R)
        {
            return ScenarioExt.ScenarioClass_DTOR(R);
        }

        [Hook(HookType.AresHook, Address = 0x68785C, Size = 7)]
        static public unsafe UInt32 ScenarioClass_LoadFromINI(REGISTERS* R)
        {
            return ScenarioExt.ScenarioClass_LoadFromINI(R);
        }

        //[Hook(HookType.AresHook, Address = 0x689470, Size = 5)]
        //[Hook(HookType.AresHook, Address = 0x689310, Size = 5)]
        //static public unsafe UInt32 ScenarioClass_SaveLoad_Prefix(REGISTERS* R)
        //{
        //    return ScenarioExt.ScenarioClass_SaveLoad_Prefix(R);
        //}

        //[Hook(HookType.AresHook, Address = 0x689669, Size = 6)]
        //static public unsafe UInt32 ScenarioClass_Load_Suffix(REGISTERS* R)
        //{
        //    return ScenarioExt.ScenarioClass_Load_Suffix(R);
        //}

        //[Hook(HookType.AresHook, Address = 0x68945B, Size = 8)]
        //static public unsafe UInt32 ScenarioClass_Save_Suffix(REGISTERS* R)
        //{
        //    return ScenarioExt.ScenarioClass_Save_Suffix(R);
        //}

        [Hook(HookType.AresHook, Address = 0x55AFB3, Size = 6)]
        static public unsafe UInt32 ScenarioClass_Update(REGISTERS* R)
        {
            ScenarioExt.ScenarioClass_Update(R);
            return 0;
            //try
            //{
            //    return ScriptManager.ScenarioClass_Update_Script(R);
            //}
            //catch (Exception e)
            //{
            //    Logger.PrintException(e);
            //    return (uint)0;
            //}
        }
        [Hook(HookType.AresHook, Address = 0x6D4934, Size = 6)]
        static public unsafe UInt32 TacticalClass_Render(REGISTERS* R)
        {
            ScenarioExt.TacticalClass_Render();
            return 0;
        }
    }
}