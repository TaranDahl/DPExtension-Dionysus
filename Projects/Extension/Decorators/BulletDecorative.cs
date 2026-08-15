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
    public class BulletDecorative
    {
        static public unsafe UInt32 OnUpdate(REGISTERS* R)
        {
            Pointer<BulletClass> pBullet = (IntPtr)R->ECX;

            IDecorativeInterface<IEventDecorator> decorative = BulletExt.ExtMap.Find(pBullet);
            foreach (var decorator in decorative.GetDecorators())
            {
                decorator.OnUpdate();
            }

            return 0;
        }
    }
}
