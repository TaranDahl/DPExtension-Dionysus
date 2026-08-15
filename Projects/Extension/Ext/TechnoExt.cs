using DynamicPatcher;
using Extension.Decorators;
using Extension.Script;
using Extension.Utilities;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Ext
{
    [Serializable]
    public partial class TechnoExt : Extension<TechnoClass>,
        IDecorative,
        IDecorative<PairDecorator>,
        IDecorativeInterface<IEventDecorator>
    {
        public static Container<TechnoExt, TechnoClass> ExtMap = new Container<TechnoExt, TechnoClass>("TechnoClass");

        internal Lazy<TechnoScriptable> scriptable;
        public TechnoScriptable Scriptable
        {
            get
            {
                if (scriptable.IsValueCreated || Type.Script != null)
                {
                    return scriptable.Value;
                }
                return null;
            }
        }

        ExtensionReference<TechnoTypeExt> type;
        public TechnoTypeExt Type
        {
            get
            {
                if (type.TryGet(out TechnoTypeExt ext) == false)
                {
                    type.Set(OwnerObject.Ref.Type);
                    ext = type.Get();
                }
                return ext;
            }
        }

        public TechnoExt(Pointer<TechnoClass> OwnerObject) : base(OwnerObject)
        {
            scriptable = new Lazy<TechnoScriptable>(() => ScriptManager.GetScriptable(Type.Script, this) as TechnoScriptable);
        }

        DecoratorMap decoratorMap = new DecoratorMap();

        public TDecorator CreateDecorator<TDecorator>(DecoratorId id, string description, params object[] parameters) where TDecorator : Decorator
        {
            TDecorator decorator = decoratorMap.CreateDecorator<TDecorator>(id, description, parameters);
            decorator.Decorative = this;
            return decorator;
        }
        public static Pointer<ObjectClass> CreateObjectWithDecorator<TDecorator>(Pointer<ObjectTypeClass> type, Pointer<HouseClass> owner, DecoratorId id, params object[] decoratorParams) where TDecorator : Decorator
        {
            var obj = type.Ref.CreateObject(owner);
            if ((obj.Ref.Base.AbstractFlags & AbstractFlags.Techno) == AbstractFlags.None)
            {
                Logger.Log("Non-techno type can not attached by decorator!");
            }
            else
            {
                var technoExt = TechnoExt.ExtMap.Find(obj.Convert<TechnoClass>());
                technoExt.CreateDecorator<TDecorator>(id, "", decoratorParams);
            }
            return obj;
        }

        public Decorator Get(DecoratorId id) => decoratorMap.Get(id);

        public void Remove(DecoratorId id) => decoratorMap.Remove(id);

        public void Remove(Decorator decorator) => decoratorMap.Remove(decorator);

        IEnumerable<IEventDecorator> IDecorativeInterface<IEventDecorator>.GetDecorators() => decoratorMap.GetEventDecorators();

        IEnumerable<PairDecorator> IDecorative<PairDecorator>.GetDecorators() => decoratorMap.GetPairDecorators();

        public override void OnDeserialization(object sender)
        {
            base.OnDeserialization(sender);
        }

        [OnSerializing]
        protected void OnSerializing(StreamingContext context) { }

        [OnSerialized]
        protected void OnSerialized(StreamingContext context) { }

        [OnDeserializing]
        protected void OnDeserializing(StreamingContext context) { }

        [OnDeserialized]
        protected void OnDeserialized(StreamingContext context) { }

        public override void SaveToStream(IStream stream)
        {
            base.SaveToStream(stream);
            Scriptable?.SaveToStream(stream);
        }

        public override void LoadFromStream(IStream stream)
        {
            base.LoadFromStream(stream);
            Scriptable?.LoadFromStream(stream);
        }

        static public unsafe UInt32 TechnoClass_CTOR(REGISTERS* R)
        {
            var pItem = (Pointer<TechnoClass>)R->ESI;

            TechnoExt.ExtMap.FindOrAllocate(pItem);
            return 0;
        }

        static public unsafe UInt32 TechnoClass_DTOR(REGISTERS* R)
        {
            var pItem = (Pointer<TechnoClass>)R->ECX;

            TechnoExt.ExtMap.Remove(pItem);
            return 0;
        }

        static public unsafe UInt32 TechnoClass_SaveLoad_Prefix(REGISTERS* R)
        {
            var pItem = R->Stack<Pointer<TechnoClass>>(0x4);
            var pStm = R->Stack<Pointer<IStream>>(0x8);
            IStream stream = Marshal.GetObjectForIUnknown(pStm) as IStream;

            TechnoExt.ExtMap.PrepareStream(pItem, stream);
            return 0;
        }

        static public unsafe UInt32 TechnoClass_Load_Suffix(REGISTERS* R)
        {
            TechnoExt.ExtMap.LoadStatic();
            return 0;
        }

        static public unsafe UInt32 TechnoClass_Save_Suffix(REGISTERS* R)
        {
            TechnoExt.ExtMap.SaveStatic();
            return 0;
        }
    }
}
