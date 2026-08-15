using DynamicPatcher;
using Extension.Utilities;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Extension.Script;
using Extension.Mutators;

namespace Extension.Ext
{
    [Serializable]
    public partial class SuperWeaponTypeExt : Extension<SuperWeaponTypeClass>
    {
        public static Container<SuperWeaponTypeExt, SuperWeaponTypeClass> ExtMap = new Container<SuperWeaponTypeExt, SuperWeaponTypeClass>("SuperWeaponTypeClass");

        public static void LaunchFakeSW(Pointer<SuperWeaponTypeClass> pSWToFire, Pointer<HouseClass> pFirer, CellStruct target)
        {
            var pSuper = pFirer.Ref.Supers[pSWToFire.Ref.ArrayIndex].Ref;
            pSuper.SetReadiness(true);
            pSuper.Launch(target, pFirer == HouseClass.Player);
            pSuper.Reset();
        }

        [NonSerialized]
        public Type ActivateMutator;
        public int ActivateBrutalPlus;

        public SuperWeaponTypeExt(Pointer<SuperWeaponTypeClass> OwnerObject) : base(OwnerObject)
        {
            
        }

        protected override void LoadFromINIFile(Pointer<CCINIClass> pINI)
        {
            INI_EX exINI = new INI_EX(pINI);
            INIReader reader = new INIReader(exINI);
            string section = OwnerObject.Ref.Base.ID;

            reader.ReadMutatorType(section, "ActivateMutator", ref ActivateMutator);
            reader.ReadNormal(section, "ActivateBrutalPlus", ref ActivateBrutalPlus);
        }

        public override void SaveToStream(IStream stream)
        {
            base.SaveToStream(stream);
        }
        public override void LoadFromStream(IStream stream)
        {
            base.LoadFromStream(stream);
        }

        //[Hook(HookType.AresHook, Address = 0x6CE6F6, Size = 5)]
        public static unsafe UInt32 SuperWeaponTypeClass_CTOR(REGISTERS* R)
        {
            var pItem = (Pointer<SuperWeaponTypeClass>)R->EAX;

            SuperWeaponTypeExt.ExtMap.FindOrAllocate(pItem);
            return 0;
        }

        //[Hook(HookType.AresHook, Address = 0x6CEFE0, Size = 8)]
        public static unsafe UInt32 SuperWeaponTypeClass_DTOR(REGISTERS* R)
        {
            var pItem = (Pointer<SuperWeaponTypeClass>)R->ECX;

            SuperWeaponTypeExt.ExtMap.Remove(pItem);
            return 0;
        }

        //[Hook(HookType.AresHook, Address = 0x6CEE50, Size = 0xA)]
        //[Hook(HookType.AresHook, Address = 0x6CEE43, Size = 0xA)]
        public static unsafe UInt32 SuperWeaponTypeClass_LoadFromINI(REGISTERS* R)
        {
            var pItem = (Pointer<SuperWeaponTypeClass>)R->EBP;
            var pINI = R->Stack<Pointer<CCINIClass>>(0x3FC);

            SuperWeaponTypeExt.ExtMap.LoadFromINI(pItem, pINI);
            return 0;
        }

        //[Hook(HookType.AresHook, Address = 0x6CE8D0, Size = 8)]
        //[Hook(HookType.AresHook, Address = 0x6CE800, Size = 0xA)]
        public static unsafe UInt32 SuperWeaponTypeClass_SaveLoad_Prefix(REGISTERS* R)
        {
            var pItem = R->Stack<Pointer<SuperWeaponTypeClass>>(0x4);
            var pStm = R->Stack<Pointer<IStream>>(0x8);
            IStream stream = Marshal.GetObjectForIUnknown(pStm) as IStream;

            SuperWeaponTypeExt.ExtMap.PrepareStream(pItem, stream);
            return 0;
        }

        //[Hook(HookType.AresHook, Address = 0x6CE8BE, Size = 7)]
        public static unsafe UInt32 SuperWeaponTypeClass_Load_Suffix(REGISTERS* R)
        {
            SuperWeaponTypeExt.ExtMap.LoadStatic();
            return 0;
        }

        //[Hook(HookType.AresHook, Address = 0x6CE8EA, Size = 5)]
        public static unsafe UInt32 SuperWeaponTypeClass_Save_Suffix(REGISTERS* R)
        {
            SuperWeaponTypeExt.ExtMap.SaveStatic();
            return 0;
        }
    }
}
