using System;
using System.Collections.Generic;

namespace Extension.Mutators
{
    /// <summary>
    /// 突变因子选择模式（对应 [Basic]MutatorSelectionMode 枚举）。
    /// </summary>
    public enum MutatorSelectionMode
    {
        /// <summary>读取 BrutalLevel 启动相应等级的残酷+。</summary>
        LevelRandom,
        /// <summary>读取 RandomCount 随机启用指定数量的因子。</summary>
        SimpleRandom,
        /// <summary>读取 ForcedMutators 启动其中指定的因子。</summary>
        Force,
        /// <summary>让玩家自己选择（显示右侧选择器 UI）。</summary>
        Manual
    }

    /// <summary>
    /// [Basic] 节下突变因子选择器配置，由 <see cref="Ext.RulesExt"/> 在读取 rulesmd.ini 时填充。
    /// </summary>
    [Serializable]
    public class MutatorSelectionConfig
    {
        /// <summary>是否为 RPG 模式。为 true 时 RPG 不可用因子不会被随机出来，UI 中显示禁用。</summary>
        public bool IsRPG = false;

        /// <summary>禁用的突变因子名称列表：不会随机，UI 中显示禁用。</summary>
        public HashSet<string> BannedMutators = new HashSet<string>();

        /// <summary>选择模式，默认 Manual。</summary>
        public MutatorSelectionMode Mode = MutatorSelectionMode.Manual;

        /// <summary>残酷+等级（1-6），LevelRandom 时生效。</summary>
        public int BrutalLevel = 1;

        /// <summary>随机启用因子数量（任意正数），SimpleRandom 时生效。</summary>
        public int RandomCount = 1;

        /// <summary>强制启用的因子名称列表，Force 时生效。</summary>
        public List<string> ForcedMutators = new List<string>();
    }
}
