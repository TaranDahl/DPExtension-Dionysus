using System;
using DynamicPatcher;
using PatcherYRpp;
using Extension.Ext;

namespace ExtensionHooks
{
    public class RulesExtHooks
    {
        // RulesClass 构造：创建 RulesExt 单例（地址/寄存器对齐 Phobos RulesExt::Allocate）
        [Hook(HookType.AresHook, Address = 0x667A1D, Size = 5)]
        static public unsafe UInt32 RulesClass_CTOR(REGISTERS* R)
        {
            try
            {
                return RulesExt.RulesClass_CTOR(R);
            }
            catch (Exception e)
            {
                Logger.PrintException(e);
                return (uint)0;
            }
        }

        // RulesClass 析构：清空单例
        [Hook(HookType.AresHook, Address = 0x667A30, Size = 5)]
        static public unsafe UInt32 RulesClass_DTOR(REGISTERS* R)
        {
            try
            {
                return RulesExt.RulesClass_DTOR(R);
            }
            catch (Exception e)
            {
                Logger.PrintException(e);
                return (uint)0;
            }
        }

        // RulesClass::Read_File（加载 rulesmd.ini 时触发）：读取全局配置字段
        [Hook(HookType.AresHook, Address = 0x668BF0, Size = 5)]
        static public unsafe UInt32 RulesClass_LoadFromINI(REGISTERS* R)
        {
            try
            {
                return RulesExt.RulesClass_LoadFromINI(R);
            }
            catch (Exception e)
            {
                Logger.PrintException(e);
                return (uint)0;
            }
        }

        // RulesClass 存读档：对齐 ScenarioExt 的 g_pStm + WriteObject/ReadObject 机制（已启用）。
        [Hook(HookType.AresHook, Address = 0x674730, Size = 6)]
        [Hook(HookType.AresHook, Address = 0x675210, Size = 5)]
        static public unsafe UInt32 RulesClass_SaveLoad_Prefix(REGISTERS* R)
        {
            try
            {
                return RulesExt.RulesClass_SaveLoad_Prefix(R);
            }
            catch (Exception e)
            {
                Logger.PrintException(e);
                return (uint)0;
            }
        }

        [Hook(HookType.AresHook, Address = 0x678841, Size = 7)]
        static public unsafe UInt32 RulesClass_Load_Suffix(REGISTERS* R)
        {
            try
            {
                return RulesExt.RulesClass_Load_Suffix(R);
            }
            catch (Exception e)
            {
                Logger.PrintException(e);
                return (uint)0;
            }
        }

        [Hook(HookType.AresHook, Address = 0x675205, Size = 8)]
        static public unsafe UInt32 RulesClass_Save_Suffix(REGISTERS* R)
        {
            try
            {
                return RulesExt.RulesClass_Save_Suffix(R);
            }
            catch (Exception e)
            {
                Logger.PrintException(e);
                return (uint)0;
            }
        }
    }
}
