
using System;
using DynamicPatcher;
using PatcherYRpp;

namespace ExtensionHooks
{
    public class EventExtHooks
    {
        [Hook(HookType.AresHook, Address = 0x4C6CC8, Size = 0x5)]
        public static unsafe UInt32 Networking_RespondToEvent(REGISTERS* R)
        {
            return Extension.Ext.EventExt.Networking_RespondToEvent(R);
        }

        [Hook(HookType.AresHook, Address = 0x64B6FE, Size = 0x6)]
        public static unsafe UInt32 sub_64B660_GetEventSize(REGISTERS* R)
        {
            return Extension.Ext.EventExt.sub_64B660_GetEventSize(R);
        }

        [Hook(HookType.AresHook, Address = 0x64BE7D, Size = 0x6)]
        public static unsafe UInt32 sub_64BDD0_GetEventSize1(REGISTERS* R)
        {
            return Extension.Ext.EventExt.sub_64BDD0_GetEventSize1(R);
        }

        [Hook(HookType.AresHook, Address = 0x64C30E, Size = 0x6)]
        public static unsafe UInt32 sub_64BDD0_GetEventSize2(REGISTERS* R)
        {
            return Extension.Ext.EventExt.sub_64BDD0_GetEventSize2(R);
        }
    }
}