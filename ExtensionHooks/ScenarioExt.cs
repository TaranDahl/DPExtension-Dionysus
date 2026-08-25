
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
            try
            {
                return ScenarioExt.ScenarioClass_CTOR(R);
            }
            catch (Exception e)
            {
                Logger.PrintException(e);
                return (uint)0;
            }
        }

        [Hook(HookType.AresHook, Address = 0x6BEB7D, Size = 6)]
        static public unsafe UInt32 ScenarioClass_DTOR(REGISTERS* R)
        {
            Logger.Log("[Hook] ScenarioClass_DTOR called.\n");
            try
            {
                return ScenarioExt.ScenarioClass_DTOR(R);
            }
            catch (Exception e)
            {
                Logger.PrintException(e);
                return (uint)0;
            }
        }

        [Hook(HookType.AresHook, Address = 0x68785C, Size = 7)]
        static public unsafe UInt32 ScenarioClass_LoadFromINI(REGISTERS* R)
        {
            Logger.Log("[Hook] ScenarioClass_LoadFromINI called.\n");
            try
            {
                return ScenarioExt.ScenarioClass_LoadFromINI(R);
            }
            catch (Exception e)
            {
                Logger.PrintException(e);
                return (uint)0;
            }
        }

        // Scenario 存读档 hook：按 Phobos 单例方式管理（静态 g_pStm + 块读写 + Canary 校验）。
        // ScenarioExt 是全局单例，不走 ExtMap 容器，因此也不存在“单例被当多实例容器管”的问题。
        [Hook(HookType.AresHook, Address = 0x689470, Size = 5)]
        [Hook(HookType.AresHook, Address = 0x689310, Size = 5)]
        static public unsafe UInt32 ScenarioClass_SaveLoad_Prefix(REGISTERS* R)
        {
            Logger.Log("[Hook] ScenarioClass_SaveLoad_Prefix called.\n");
            try
            {
                return ScenarioExt.ScenarioClass_SaveLoad_Prefix(R);
            }
            catch (Exception e)
            {
                Logger.PrintException(e);
                return (uint)0;
            }
        }

        [Hook(HookType.AresHook, Address = 0x689669, Size = 6)]
        static public unsafe UInt32 ScenarioClass_Load_Suffix(REGISTERS* R)
        {
            Logger.Log("[Hook] ScenarioClass_Load_Suffix called.\n");
            try
            {
                return ScenarioExt.ScenarioClass_Load_Suffix(R);
            }
            catch (Exception e)
            {
                Logger.PrintException(e);
                return (uint)0;
            }
        }

        [Hook(HookType.AresHook, Address = 0x68945B, Size = 8)]
        static public unsafe UInt32 ScenarioClass_Save_Suffix(REGISTERS* R)
        {
            Logger.Log("[Hook] ScenarioClass_Save_Suffix called.\n");
            try
            {
                return ScenarioExt.ScenarioClass_Save_Suffix(R);
            }
            catch (Exception e)
            {
                Logger.PrintException(e);
                return (uint)0;
            }
        }

        [Hook(HookType.AresHook, Address = 0x55AFB3, Size = 6)]
        static public unsafe UInt32 ScenarioClass_Update(REGISTERS* R)
        {
            try
            {
                ScenarioExt.ScenarioClass_Update(R);
            }
            catch (Exception e)
            {
                Logger.PrintException(e);
            }
            return 0;
        }
        [Hook(HookType.AresHook, Address = 0x55B4E1, Size = 5)]
        static public unsafe UInt32 LogicClass_Update_BeforeAll(REGISTERS* R)
        {
            try
            {
                ScenarioExt.LogicClass_Update_BeforeAll(R);
            }
            catch (Exception e)
            {
                Logger.PrintException(e);
            }
            return 0;
        }
        [Hook(HookType.AresHook, Address = 0x6D4934, Size = 6)]
        static public unsafe UInt32 TacticalClass_Render(REGISTERS* R)
        {
            try
            {
                ScenarioExt.TacticalClass_Render();
            }
            catch (Exception e)
            {
                Logger.PrintException(e);
            }
            return 0;
        }
    }
}