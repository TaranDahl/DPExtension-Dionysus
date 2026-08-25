using DynamicPatcher;
using Extension.Utilities;
using PatcherYRpp;
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Extension.Ext
{
    /// <summary>
    /// RulesClass 扩展：全局单例（对齐 ScenarioExt / Phobos RulesExt 的静态指针单例模式）。
    /// 承载 rulesmd.ini 的全局配置字段（[General] / [AudioVisual] / [CombatDamage] 等）。
    /// 逻辑实现位于此处；带 [Hook] 地址的注册在 DynamicPatcher/ExtensionHooks/RulesExt.cs。
    /// </summary>
    [Serializable]
    public partial class RulesExt : Extension<RulesClass>
    {
        private static RulesExt Instance;

        // Phobos 式：存读档直接使用的类级静态流（对齐 ScenarioExt::g_pStm）
        public static IStream g_pStm;

        // ===== 示例字段：原版 [General]BuildSpeed（浮点数，建造速度倍率，默认 1.0） =====
        public float BuildSpeed;

        public RulesExt(Pointer<RulesClass> OwnerObject) : base(OwnerObject)
        {
            BuildSpeed = 1.0f;
        }

        public static RulesExt Global()
        {
            return Instance;
        }

        protected override void LoadFromINIFile(Pointer<CCINIClass> pINI)
        {
            INI_EX exINI = new INI_EX(pINI);
            INIReader reader = new INIReader(exINI);

            // 示例字段：从 [General]BuildSpeed 读取（原版字段，rulesmd.ini 里存在）
            reader.ReadNormal(RulesClass.SectionGeneral, "BuildSpeed", ref BuildSpeed);

            Logger.Log("[RulesExt] LoadFromINIFile: BuildSpeed={0}\n", BuildSpeed);
        }

        public override void SaveToStream(IStream stream)
        {
            Logger.Log("[RulesExt] SaveToStream: entering. BuildSpeed={0}\n", BuildSpeed);
            base.SaveToStream(stream);
            Logger.Log("[RulesExt] SaveToStream: done.\n");
        }
        public override void LoadFromStream(IStream stream)
        {
            Logger.Log("[RulesExt] LoadFromStream: entering.\n");
            base.LoadFromStream(stream);
            Logger.Log("[RulesExt] LoadFromStream: done.\n");
        }

        // ===== 单例生命周期钩子入口（供 ExtensionHooks 调用） =====

        static public unsafe UInt32 RulesClass_CTOR(REGISTERS* R)
        {
            var pItem = (Pointer<RulesClass>)R->ESI;
            Logger.Log("[RulesExt] CTOR: creating singleton. pItem(ESI)=0x{0:X}\n", (int)pItem);

            // Rules 是全局单例，直接创建保存引用（对齐 Phobos 的 RulesExt::Data），不进 ExtMap 容器
            RulesExt.Instance = new RulesExt(pItem);

            Logger.Log("[RulesExt] CTOR: singleton created.\n");
            return 0;
        }

        static public unsafe UInt32 RulesClass_DTOR(REGISTERS* R)
        {
            var pItem = (Pointer<RulesClass>)R->ECX;
            Logger.Log("[RulesExt] DTOR: clearing singleton. pItem(ECX)=0x{0:X}\n", (int)pItem);

            RulesExt.Instance = null;
            Logger.Log("[RulesExt] DTOR: singleton cleared.\n");
            return 0;
        }

        // RulesClass::Read_File（0x668BF0，加载 rulesmd.ini 时触发，对齐 Phobos RulesClass_Addition）
        static public unsafe UInt32 RulesClass_LoadFromINI(REGISTERS* R)
        {
            var pItem = (Pointer<RulesClass>)R->ECX;
            var pINI = R->Stack<Pointer<CCINIClass>>(0x4);
            Logger.Log("[RulesExt] LoadFromINI: entering. pItem=0x{0:X}, pINI=0x{1:X}\n", (int)pItem, (int)pINI);

            // 兜底：CTOR 钩子未触发时懒创建，避免空引用（首跑调试用）
            if (RulesExt.Global() == null)
            {
                Logger.Log("[RulesExt] LoadFromINI: Global() is null, lazy creating.\n");
                RulesExt.Instance = new RulesExt(pItem);
            }

            RulesExt.Global().LoadFromINI(pINI);

            Logger.Log("[RulesExt] LoadFromINI: done. BuildSpeed={0}\n", RulesExt.Global().BuildSpeed);
            return 0;
        }

        // ===== 存读档入口（对齐 ScenarioExt 的 g_pStm + WriteObject/ReadObject 整图序列化） =====

        static public unsafe UInt32 RulesClass_SaveLoad_Prefix(REGISTERS* R)
        {
            var pStm = R->Stack<Pointer<IStream>>(0x4);
            IStream stream = Marshal.GetObjectForIUnknown(pStm) as IStream;

            // 类级静态流，供后缀 hook 使用（对齐 Phobos 的 RulesExt::g_pStm）
            g_pStm = stream;
            Logger.Log("[RulesExt] SaveLoad_Prefix: g_pStm=0x{0:X}\n", (int)pStm);
            return 0;
        }

        static public unsafe UInt32 RulesClass_Load_Suffix(REGISTERS* R)
        {
            // 用 WriteObject/ReadObject 整图序列化 RulesExt 单例，与 ScenarioExt 同机制
            if (g_pStm != null)
            {
                g_pStm.ReadObject(out RulesExt loaded);
                loaded.OwnerObject = RulesClass.Instance;
                RulesExt.Instance = loaded;
                loaded.LoadFromStream(g_pStm);
                Logger.Log("[RulesExt] Load_Suffix: ReadObject done. BuildSpeed={0}\n", loaded.BuildSpeed);
            }
            else
            {
                Logger.Log("[RulesExt] Load_Suffix: g_pStm is null.\n");
            }
            return 0;
        }

        static public unsafe UInt32 RulesClass_Save_Suffix(REGISTERS* R)
        {
            // 整图 WriteObject，与 ScenarioExt 的 Save_Suffix 同机制
            if (g_pStm != null)
            {
                g_pStm.WriteObject(Instance);
                Instance.SaveToStream(g_pStm);
                Logger.Log("[RulesExt] Save_Suffix: WriteObject done. BuildSpeed={0}\n", Instance.BuildSpeed);
            }
            else
            {
                Logger.Log("[RulesExt] Save_Suffix: g_pStm is null.\n");
            }
            return 0;
        }
    }
}
