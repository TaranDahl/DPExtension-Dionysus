using DynamicPatcher;
using Extension.AttackWave;
using Extension.Decorators;
using Extension.Mutators;
using Extension.Script;
using Extension.Utilities;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Ext
{
    [Serializable]
    public partial class ScenarioExt : Extension<ScenarioClass>,
        IDecorative,
        IDecorative<PairDecorator>,
        IDecorativeInterface<IEventDecorator>,
        IDecorativeInterface<IRenderDecorator>
    {
        private readonly static bool Testing = false;
        private readonly static int StdFramePerSec = Testing ? 2 : 30;
        private readonly static double StdFundExchangeRate = 2.0;
        public static int TimeToFrame(double min, double sec, int Frame = 0)
        {
            var result = (int)((min * 60 + sec) * StdFramePerSec) + Frame;
            if (result == 0 && (min != 0 || sec != 0 || Frame != 0))
            {
                result = 1;
            }
            return result;
        }
        public static int ExchangeCurrency(int mineral, int vespenGas, double supply = 0.0)
        {
            return (int)(StdFundExchangeRate * (mineral + vespenGas * 3 + supply * 12.5));
        }
        public static int ExchangeCurrency(int resourceTotal, bool considerGas = true)
        {
            return (int)(StdFundExchangeRate * resourceTotal * (considerGas ? 1.5 : 1.0));
        }

        private static ScenarioExt Instance;

        public static Container<ScenarioExt, ScenarioClass> ExtMap = new Container<ScenarioExt, ScenarioClass>("ScenarioClass");

        // Decorators
        DecoratorMap decoratorMap = new DecoratorMap();
        public TDecorator CreateDecorator<TDecorator>(DecoratorId id, string description, params object[] parameters) where TDecorator : Decorator
        {
            TDecorator decorator = decoratorMap.CreateDecorator<TDecorator>(id, description, parameters);
            decorator.Decorative = this;
            return decorator;
        }
        public Decorator Get(DecoratorId id) => decoratorMap.Get(id);
        public void Remove(DecoratorId id) => decoratorMap.Remove(id);
        public void Remove(Decorator decorator) => decoratorMap.Remove(decorator);
        IEnumerable<PairDecorator> IDecorative<PairDecorator>.GetDecorators() => decoratorMap.GetPairDecorators();
        IEnumerable<IEventDecorator> IDecorativeInterface<IEventDecorator>.GetDecorators() => decoratorMap.GetEventDecorators();
        IEnumerable<IRenderDecorator> IDecorativeInterface<IRenderDecorator>.GetDecorators() => decoratorMap.GetDrawableDecorators();

        // 属性
        private int scenarioDecoratorCount = 100000;
        public DecoratorId FetchScenarioDecoratorID
        {
            get
            {
                scenarioDecoratorCount++;
                return new DecoratorId(scenarioDecoratorCount);
            }
        }
        public AttackWaveManager AttackWaveManager;
        public MutatorCacheManager MutatorCacheManager;
        public ScenarioExt(Pointer<ScenarioClass> OwnerObject) : base(OwnerObject)
        {
            AttackWaveManager = new AttackWaveManager();
            MutatorCacheManager = new MutatorCacheManager();
        }

        public static ScenarioExt Global()
        {
            return Instance;
        }
        protected override void LoadFromINIFile(Pointer<CCINIClass> pINI)
        {
            INI_EX exINI = new INI_EX(pINI);
            INIReader reader = new INIReader(exINI);

            AttackWaveManager.LoadFromINI();
        }

        public override void SaveToStream(IStream stream)
        {
            base.SaveToStream(stream);
        }
        public override void LoadFromStream(IStream stream)
        {
            base.LoadFromStream(stream);
        }
        static public void TacticalClass_Render()
        {
            IDecorativeInterface<IRenderDecorator> decorative = Global();
            foreach (var decorator in decorative.GetDecorators())
            {
                decorator.OnRender();
            }
        }

        static public unsafe UInt32 ScenarioClass_CTOR(REGISTERS* R)
        {
            var pItem = (Pointer<ScenarioClass>)R->EAX;
            ScenarioExt.Instance = new ScenarioExt(pItem);
            return 0;
        }

        static public unsafe UInt32 ScenarioClass_DTOR(REGISTERS* R)
        {
            var pItem = (Pointer<ScenarioClass>)R->ESI;

            ScenarioExt.ExtMap.Remove(pItem);
            ScenarioExt.Instance = null;
            return 0;
        }

        static public unsafe UInt32 ScenarioClass_LoadFromINI(REGISTERS* R)
        {
            var pINI = (Pointer<CCINIClass>)R->EDI;

            ScenarioExt.Global().LoadFromINI(pINI);
            ScenarioExt.Global().AttackWaveManager.OnInit();
            return 0;
        }

        static public unsafe UInt32 ScenarioClass_Update(REGISTERS* R)
        {
            Instance.AttackWaveManager.OnUpdate();
            IDecorativeInterface<IEventDecorator> decorative = Instance;
            foreach (var decorator in decorative.GetDecorators())
            {
                decorator.OnUpdate();
            }
            return 0;
        }

        static public unsafe UInt32 ScenarioClass_SaveLoad_Prefix(REGISTERS* R)
        {
            var pStm = R->Stack<Pointer<IStream>>(0x4);
            IStream stream = Marshal.GetObjectForIUnknown(pStm) as IStream;

            ScenarioExt.ExtMap.PrepareStream(ScenarioClass.instance, stream);
            return 0;
        }

        static public unsafe UInt32 ScenarioClass_Load_Suffix(REGISTERS* R)
        {
            ScenarioExt.ExtMap.LoadStatic();
            return 0;
        }

        static public unsafe UInt32 ScenarioClass_Save_Suffix(REGISTERS* R)
        {
            ScenarioExt.ExtMap.SaveStatic();
            return 0;
        }
    }
}
