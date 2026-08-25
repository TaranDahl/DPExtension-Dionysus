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

        // Phobos 式：存读档直接使用的类级静态流（不走 ExtMap 容器，避免整图 BinaryFormatter 序列化）
        public static IStream g_pStm;

        // 新局初始化标志（对齐 Phobos 的“LoadFromINI 置标志 + LogicClass_Update_BeforeAll 每帧执行一次”模式）
        // LoadFromINI 只在开新局时触发、读档不触发，因此该标志天然区分新局/读档：
        // 读档后不会再次置位，每帧钩子也不会重复执行每局初始化。
        private static bool NewGameInitPending = false;

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

            // 波次类型表是静态配置，在这里填充（每局一次）
            AttackWaveManager.LoadFromINI();
        }

        // 新局初始化：世界已就绪后只执行一次（对齐 Phobos：LoadFromINI 置标志 + LogicClass_Update_BeforeAll 每帧执行）
        // 依赖世界：TechnoTypeClass 类型表 / TerrainClass 矿物 / MapClass 格子缓存 / HouseClass 阵营 / SuperWeaponTypeClass
        static public void NewGameInitOnce()
        {
            if (!NewGameInitPending)
            {
                return;
            }
            NewGameInitPending = false;

            Logger.Log("[ScenarioExt] NewGameInit: begin\n");

            var global = Global();

            // 派生缓存：每局重建，不入档（读档后由后续逻辑重建）
            MutatorRandomizer.Init(); // 进程级一次：检查因子类型/SWID 有效性，填充 AvailableMutators（幂等）
            MutatorCacheManager.OnGameStart(); // 构建单位等级表（遍历 TechnoTypeClass）
            TargetCellMutator.OnUsableAreaChange(); // 缓存地图格子/安全区（遍历 TerrainClass/MapClass）
            global.AttackWaveManager.OnInit(); // 按 AI 阵营选择本局攻击波次类型

            Logger.Log("[ScenarioExt] NewGameInit: done. TypeArray.Count={0}, Array.Count={1}\n",
                global.AttackWaveManager.TypeArray.Count, global.AttackWaveManager.Array.Count);
        }

        public override void SaveToStream(IStream stream)
        {
            Logger.Log("[ScenarioExt] SaveToStream: entering. decoratorMap.Count={0}\n", decoratorMap != null ? 1 : 0);
            base.SaveToStream(stream);
            Logger.Log("[ScenarioExt] SaveToStream: done.\n");
        }
        public override void LoadFromStream(IStream stream)
        {
            Logger.Log("[ScenarioExt] LoadFromStream: entering.\n");
            base.LoadFromStream(stream);
            Logger.Log("[ScenarioExt] LoadFromStream: done.\n");
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
            Logger.Log("[ScenarioExt] CTOR: creating singleton. pItem(EAX)=0x{0:X}\n", (int)pItem);

            // Scenario 是全局单例，直接创建保存引用（对齐 Phobos 的 ScenarioExt::Data）。
            // 不用 ExtMap 容器的整图序列化做存读档，因此这里无需注册进 ExtMap。
            ScenarioExt.Instance = new ScenarioExt(pItem);

            Logger.Log("[ScenarioExt] CTOR: singleton created.\n");
            return 0;
        }

        static public unsafe UInt32 ScenarioClass_DTOR(REGISTERS* R)
        {
            var pItem = (Pointer<ScenarioClass>)R->ESI;
            Logger.Log("[ScenarioExt] DTOR: clearing singleton. pItem(ESI)=0x{0:X}\n", (int)pItem);

            ScenarioExt.Instance = null;
            Logger.Log("[ScenarioExt] DTOR: singleton cleared.\n");
            return 0;
        }

        static public unsafe UInt32 ScenarioClass_LoadFromINI(REGISTERS* R)
        {
            var pINI = (Pointer<CCINIClass>)R->EDI;
            Logger.Log("[ScenarioExt] LoadFromINI: entering, pINI=0x{0:X}\n", (int)pINI);

            ScenarioExt.Global().LoadFromINI(pINI);

            // 开新局信号：置位后由 LogicClass_Update_BeforeAll 在每帧里执行一次新局初始化（世界已就绪）。
            // 读档不经过 LoadFromINI，因此不会再次置位，读档后不会重复初始化。
            NewGameInitPending = true;
            Logger.Log("[ScenarioExt] LoadFromINI: NewGameInitPending set.\n");
            return 0;
        }

        static public unsafe UInt32 ScenarioClass_Update(REGISTERS* R)
        {
            if (Game.CurrentFrame <= 2 || Game.CurrentFrame % 900 == 0)
            {
                Logger.Log("[ScenarioExt] Update: frame={0}, Instance={1}\n", Game.CurrentFrame, Instance != null ? "ok" : "NULL");
            }

            Instance.AttackWaveManager.OnUpdate();
            IDecorativeInterface<IEventDecorator> decorative = Instance;
            foreach (var decorator in decorative.GetDecorators())
            {
                decorator.OnUpdate();
            }
            return 0;
        }

        // LogicClass::Update 开头（每帧、世界已就绪）。对齐 Phobos：在这里消费 LoadFromINI 置位的新局初始化标志。
        static public unsafe UInt32 LogicClass_Update_BeforeAll(REGISTERS* R)
        {
            NewGameInitOnce();
            return 0;
        }

        static public unsafe UInt32 ScenarioClass_SaveLoad_Prefix(REGISTERS* R)
        {
            var pStm = R->Stack<Pointer<IStream>>(0x4);
            IStream stream = Marshal.GetObjectForIUnknown(pStm) as IStream;

            // 类级静态流，供后缀 hook 使用（对齐 Phobos 的 ScenarioExt::g_pStm）
            g_pStm = stream;
            Logger.Log("[ScenarioExt] SaveLoad_Prefix: g_pStm=0x{0:X}\n", (int)pStm);
            return 0;
        }

        static public unsafe UInt32 ScenarioClass_Load_Suffix(REGISTERS* R)
        {
            // 用 WriteObject/ReadObject（BinaryFormatter）整图序列化 ScenarioExt 单例，与 TechnoExt 同机制。
            // 注意：ScenarioExt 对象图里部分字段（AttackWaveManager/MutatorCacheManager/decoratorMap 内部）
            // 尚未做完序列化适配，Save 时可能抛 SerializationException（由 hook 层捕获打印），
            // 据此逐个处理有问题的字段。
            if (g_pStm != null)
            {
                g_pStm.ReadObject(out ScenarioExt loaded);
                loaded.OwnerObject = (Pointer<ScenarioClass>)ScenarioClass.instance;
                ScenarioExt.Instance = loaded;
                loaded.LoadFromStream(g_pStm);
                Logger.Log("[ScenarioExt] Load_Suffix: ReadObject done.\n");
            }
            else
            {
                Logger.Log("[ScenarioExt] Load_Suffix: g_pStm is null.\n");
            }
            return 0;
        }

        static public unsafe UInt32 ScenarioClass_Save_Suffix(REGISTERS* R)
        {
            // 整图 WriteObject（BinaryFormatter），与 TechnoExt 的 Container.SaveKey 同机制
            if (g_pStm != null)
            {
                g_pStm.WriteObject(Instance);
                Instance.SaveToStream(g_pStm);
                Logger.Log("[ScenarioExt] Save_Suffix: WriteObject done.\n");
            }
            else
            {
                Logger.Log("[ScenarioExt] Save_Suffix: g_pStm is null.\n");
            }
            return 0;
        }
    }
}
