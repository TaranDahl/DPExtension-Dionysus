using DynamicPatcher;
using Extension.Ext;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// TODO : Decorator函数和钩子, 当前和ScenarioExt在一起
//namespace Extension.Decorators
//{
//    public class ScenarioDecorative
//    {
//        //[Hook(HookType.AresHook, Address = 0x4666E0, Size = 6)]
//        static public unsafe UInt32 OnUpdate(REGISTERS* R)
//        {
//            Pointer<BulletClass> pBullet = (IntPtr)R->ECX;

//            IDecorative<EventDecorator> decorative = BulletExt.ExtMap.Find(pBullet);
//            foreach (var decorator in decorative.GetDecorators())
//            {
//                decorator.OnUpdate();
//            }

//            return 0;
//        }
//    }
//}
