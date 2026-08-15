using DynamicPatcher;
using Extension.Decorators;
using Extension.Ext;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Extension.Mutators
{
    [Serializable]
    public class PowerOverwhelming : Mutator
    {
        // Mutator 元数据
        public override string UIName => "灵能爆表";
        public override string Description => "所有敌方单位拥有能量并且使用随机技能。";
        public override bool IsAvailableInRPG => true;
        public override int Score => 5;

        public PowerOverwhelming(Pointer<HouseClass> owner) : base(owner) { }

        // ========== 技能定义结构（由你后续填写具体参数） =========
        [Serializable]
        public enum SkillKind
        {
            SimpleFire = 0,           // 原来的 Weapon.SimpleFire
            DetonateSelfWeapon = 1,   // Weapon.DetonateAtSelf(techno)
        }

        [Serializable]
        public struct SkillDef
        {
            public string Name;       // 资源名（用于在对应 Array 中查找）
            public int EnergyCost;    // 能量消耗（整数单位）
            public Range SkillRange;  // 技能射程（使用项目中的 Range 结构）
            public SkillKind Kind;    // 技能类型
            public Pointer<WeaponTypeClass> Weapon; // 对应的武器类型指针（仅用于 SimpleFire 或 DetonateSelfWeapon）

            public SkillDef(string name, int cost, Range range)
            {
                Name = name;
                EnergyCost = cost;
                SkillRange = range;
                Kind = SkillKind.SimpleFire;
                Weapon = Pointer<WeaponTypeClass>.Zero;
            }

            public SkillDef(string name, int cost, Range range, SkillKind kind)
            {
                Name = name;
                EnergyCost = cost;
                SkillRange = range;
                Kind = kind;
                Weapon = Pointer<WeaponTypeClass>.Zero;
            }
        }

        // 全局技能列表（已按你提供的数据初始化）
        // 注意：为需要特殊行为的技能使用对应资源名（Real / SW），并设置 SkillKind
        public static List<SkillDef> Skills { get; } = new List<SkillDef>
        {
            // 对应表（新名称/真实资源 -> 能量消耗 -> 射程 -> 可选 Kind）
            new SkillDef("MutPowerEMPBeam",             20,  4),
            new SkillDef("MutPowerMatrixReal",          20,  6,  SkillKind.DetonateSelfWeapon),
            new SkillDef("MutPowerHunterBomb",          40, 10),
            new SkillDef("MutPowerRadiation",           45,  9),
            new SkillDef("MutPowerDeployDron",          20,  9,  SkillKind.DetonateSelfWeapon),
            new SkillDef("MutPowerShieldAttach",        20, 10,  SkillKind.DetonateSelfWeapon),
            new SkillDef("MutPowerZorb",                20,  9),
            new SkillDef("MutPowerTimeWarp",            30,  9),
            new SkillDef("MutPowerPsiStorm",            40,  9),
            new SkillDef("MutPowerDisWeb",              40,  9),
            new SkillDef("MutPowerFungal",              40, 10),
            new SkillDef("MutPowerPsiOrb",              45, 10),
            new SkillDef("MutPowerBlackhole",          150,  9)
        };

        // ========== 能量池实现（两个池） =========
        [Serializable]
        private class EnergyPool
        {
            public int Current;          // 当前能量（整数）
            public int Threshold;        // 基础能量（阈值）：只有当 Current >= Threshold 时才允许触发技能
            public int MaxValue;         // 最大能量（上限）
            public int RegenPerSecond;   // 每秒恢复量（整数）

            public void Cap()
            {
                if (Current > MaxValue) Current = MaxValue;
                if (Current < 0) Current = 0;
            }

            public bool IsTriggerReady() => Current >= Threshold;

            public bool Consume(int amount)
            {
                if (Current >= amount)
                {
                    Current -= amount;
                    return true;
                }
                return false;
            }
        }

        private readonly EnergyPool PoolA = new EnergyPool();
        private readonly EnergyPool PoolB = new EnergyPool();

        // 为了按秒恢复，记录一秒对应的帧数
        private int framesPerSecond = 0;

        // 缓存当前阶段，避免每帧重复应用参数
        private Phase currentPhaseCached = Phase.Start;

        // 控制触发的最小目标数量
        private const int TargetPickCount = 10;

        public override void Init(bool isInitial = true)
        {
            base.Init(isInitial);

            // 初始化帧率（TimeToFrame(0,1) 返回1秒对应的帧数）
            framesPerSecond = Mutator.TimeToFrame(0, 1);

            // 初始化并缓存当前阶段参数（避免每帧重复应用）
            currentPhaseCached = GetCurrentPhase();
            ApplyPhaseParameters(currentPhaseCached);
        }

        public override void Uninit()
        {
            base.Uninit();
        }

        public override bool Update()
        {
            // 基类检查（因子失效处理）
            if (!base.Update())
                return false;

            // 根据游戏时间阶段决定是否需要切换参数（仅在阶段变化时应用）
            var phase = GetCurrentPhase();
            if (phase != currentPhaseCached)
            {
                currentPhaseCached = phase;
                ApplyPhaseParameters(currentPhaseCached);
            }

            // 每秒恢复能量：使用 Game.CurrentFrame 取模判断是否为整秒，避免额外计数器
            if (Game.CurrentFrame % framesPerSecond == 0)
            {
                PoolA.Current += PoolA.RegenPerSecond;
                PoolB.Current += PoolB.RegenPerSecond;
                PoolA.Cap();
                PoolB.Cap();

                // 当能量达到阈值时触发（注意：阈值由 Threshold 表示，而非必须到达 MaxValue）
                if (PoolA.IsTriggerReady())
                {
                    TryTriggerFromPool(PoolA);
                }
                if (PoolB.IsTriggerReady())
                {
                    TryTriggerFromPool(PoolB);
                }
            }

            return true;
        }

        // ========== 阶段与参数 =========
        private enum Phase
        {
            Start,
            After5Min,
            After8Min,
            After10Min
        }

        private Phase GetCurrentPhase()
        {
            // 使用 Mutator.TimeToFrame 转换分钟为帧数
            int f5 = Mutator.TimeToFrame(5, 0);
            int f8 = Mutator.TimeToFrame(8, 0);
            int f10 = Mutator.TimeToFrame(10, 0);

            int cur = Game.CurrentFrame;
            if (cur >= f10) return Phase.After10Min;
            if (cur >= f8) return Phase.After8Min;
            if (cur >= f5) return Phase.After5Min;
            return Phase.Start;
        }

        private void ApplyPhaseParameters(Phase phase)
        {
            // 表格中的参数（对应每个阶段：最大，阈值，回复速率(能量/秒)）
            switch (phase)
            {
                case Phase.Start:
                    SetPoolParameters(90, 80, 1);
                    break;
                case Phase.After5Min:
                    SetPoolParameters(110, 80, 4);
                    break;
                case Phase.After8Min:
                    SetPoolParameters(140, 80, 6);
                    break;
                case Phase.After10Min:
                    SetPoolParameters(290, 150, 10);
                    break;
            }
        }

        private void SetPoolParameters(int max, int threshold, int regenPerSecond)
        {
            // 对两个池设置相同的参数（若需要不同参数可以修改此处）
            PoolA.MaxValue = max;
            PoolA.Threshold = threshold;
            PoolA.RegenPerSecond = regenPerSecond;
            if (PoolA.Current < 0) PoolA.Current = 0;
            if (PoolA.Current > PoolA.MaxValue) PoolA.Current = PoolA.MaxValue;

            PoolB.MaxValue = max;
            PoolB.Threshold = threshold;
            PoolB.RegenPerSecond = regenPerSecond;
            if (PoolB.Current < 0) PoolB.Current = 0;
            if (PoolB.Current > PoolB.MaxValue) PoolB.Current = PoolB.MaxValue;
        }

        // ========== 触发逻辑（简要实现） =========
        private void TryTriggerFromPool(EnergyPool pool)
        {
            // 1) 遍历一次 TechnoClass.Array，分别收集“他们那一边”的候选目标和“我方可施法单位”（并缓存）
            var candidates = new List<Pointer<TechnoClass>>();
            var ourCasters = new List<Pointer<TechnoClass>>();

            foreach (var techno in TechnoClass.Array)
            {
                // 跳过不在地图上的单位
                if (techno.Ref.Base.InLimbo || !techno.Ref.IsInPlayfield)
                    continue;

                var owner = techno.Ref.BaseAbstract.GetOwningHouse();
                if (IsOnTheirSide(owner))
                {
                    candidates.Add(techno);
                }
                else if (IsOnOurSide(owner) && techno.Ref.BaseAbstract.WhatAmI() != AbstractType.Building)
                {
                    // 施法单位也必须在地图上（已通过 InLimbo/IsInPlayfield 过滤）
                    ourCasters.Add(techno);
                }
            }

            if (candidates.Count == 0 || ourCasters.Count == 0)
                return;

            // 2) 随机抽取最多 TargetPickCount 个目标（从 candidates 中抽取不重复项）
            var picks = new List<Pointer<TechnoClass>>();
            var maxPick = Math.Min(TargetPickCount, candidates.Count);
            while (picks.Count < maxPick)
            {
                var selected = ScenarioClass.GetRandomInList(candidates);
                if (!picks.Contains(selected))
                {
                    candidates.Remove(selected);
                    picks.Add(selected);
                }
            }

            // 3) 对每个目标随机选择一个技能并尝试释放
            var rnd = ScenarioClass.Instance.Random;
            for (int i = 0; i < picks.Count; i++)
            {
                var target = picks[i];

                if (Skills.Count == 0)
                    break; // 技能列表为空，留给后续填写参数

                // 使用索引以便在需要时把解析到的指针缓存回 Skills 列表
                int skillIndex = rnd.RandomRanged(0, Skills.Count - 1);
                var skill = Skills[skillIndex];

                // 根据 SkillKind 分别解析所需的指针并缓存
                if (skill.Kind == SkillKind.SimpleFire || skill.Kind == SkillKind.DetonateSelfWeapon)
                {
                    if (skill.Weapon.IsNull)
                    {
                        var found = WeaponTypeClass.ABSTRACTTYPE_ARRAY.Find(skill.Name);
                        skill.Weapon = found;
                        Skills[skillIndex] = skill; // 把解析到的指针缓存回列表
                    }
                }

                // 预先转换目标抽象，减少重复 Convert 调用
                var targetAbs = target.Convert<AbstractClass>();

                // 先收集所有在射程内的可用我方施法单位
                var validCasters = new List<Pointer<TechnoClass>>();
                foreach (var our in ourCasters)
                {
                    // 计算距离（单位为 Cells）
                    double dist = our.Ref.BaseAbstract.DistanceFrom(targetAbs);
                    if (dist <= (double)skill.SkillRange)
                        validCasters.Add(our);
                }

                // 如果有多个有效施法单位，随机取一个进行施法
                if (validCasters.Count > 0)
                {
                    var chosen = ScenarioClass.GetRandomInList(validCasters);

                    // 尝试从能量池消耗能量并施法（仅从当前池）
                    if (TryConsumeEnergyFromPool(pool, skill.EnergyCost))
                    {
                        TryCastSkill(chosen, target, skill);
                    }
                }
            }
        }

        // 从指定池中消耗能量（不跨池）
        private bool TryConsumeEnergyFromPool(EnergyPool pool, int amount)
        {
            if (pool.Current < amount) return false;
            pool.Current -= amount;
            pool.Cap();
            return true;
        }

        // 真实施法：根据 SkillKind 选择不同的执行路径
        private void TryCastSkill(Pointer<TechnoClass> caster, Pointer<TechnoClass> target, SkillDef skill)
        {
            if (caster.IsNull || target.IsNull)
                return;

            var casterAbs = caster.Convert<AbstractClass>();

            switch (skill.Kind)
            {
                case SkillKind.SimpleFire:
                    if (skill.Weapon.IsNull)
                    {
                        Logger.Log("PowerOverwhelming: 无法找到技能对应的武器类型: {0}", skill.Name);
                        return;
                    }

                    // 使用 WeaponType 的简单开火工具发射（以 caster 的坐标为生成点，caster 所属阵营作为开火阵营）
                    var spawnCrd = caster.Ref.BaseAbstract.GetCoords();
                    var firerHouse = caster.Ref.BaseAbstract.GetOwningHouse();
                    WeaponTypeExt.SimpleFire(skill.Weapon, target.Convert<AbstractClass>(), caster, firerHouse, spawnCrd);
                    break;

                case SkillKind.DetonateSelfWeapon:
                    if (skill.Weapon.IsNull)
                    {
                        Logger.Log("PowerOverwhelming: 无法找到需引爆的武器类型: {0}", skill.Name);
                        return;
                    }
                    // 在施法单位自身位置引爆该武器
                    skill.Weapon.Ref.DetonateAtSelf(caster);
                    break;
            }
        }
    }
}
