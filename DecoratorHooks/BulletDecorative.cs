
using System;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DynamicPatcher;
using PatcherYRpp;
using Extension.Decorators;

namespace DecoratorHooks
{
    public class BulletDecorativeHooks
    {
        [Hook(HookType.AresHook, Address = 0x4666E0, Size = 6)]
        static public unsafe UInt32 OnUpdate(REGISTERS* R)
        {
            try {
            return BulletDecorative.OnUpdate(R);
            }
			catch (Exception e)
			{
                Logger.PrintException(e);
				return (uint)0;
			}
        }
    }
}