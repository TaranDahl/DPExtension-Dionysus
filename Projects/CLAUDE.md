# CLAUDE.md

本项目是一个基于 **DynamicPatcher** 框架的 C# Mod 扩展，运行于 **Mental Omega**（红色警戒 2：尤里的复仇的大型 Mod）之上。通过直接操作游戏内存结构、Hook 游戏逻辑来实现自定义功能（Mutator 随机因子、攻击波次、装饰器等）。目标框架为 **.NET Framework 4.8**，大量使用 `unsafe` 指针代码。

---

## Solution 结构

`Projects.sln` 包含三个项目，存在依赖关系：

```
Projects/
├── YRpp/           → PatcherYRpp.dll   游戏引擎 C# 绑定层（基础层，被另外两个项目引用）
├── InteropUtils/   → InteropUtils.dll  外部模块互操作桥接层
└── Extension/      → Extension.dll     主扩展逻辑（最终产物）
```

编译顺序：`YRpp` → `InteropUtils` → `Extension`。

---

## 核心系统说明

### 一、PatcherYRpp（`YRpp/`）

游戏引擎结构的 C# 映射层。每个类是用 `[StructLayout(LayoutKind.Explicit)]` 按内存偏移精确布局的 struct，通过函数指针调用游戏原生函数。

**主要文件：**

- `TechnoClass.cs`、`BulletClass.cs`、`HouseClass.cs` 等 —— 游戏对象内存布局
- `Helpers/Pointer.cs` —— 替代裸指针的核心工具类（贯穿整个项目）
- `GeneralDefinitions.cs`、`BasicStructures.cs` —— 公共类型和枚举定义
- `YRMemory.cs` —— 内存操作工具

**`Pointer<T>` 用法：**

```csharp
pTechno.Ref.Health;                        // 访问成员：用 .Ref
pTechno.Convert<FootClass>();              // 类型转换：用 .Convert<T>()
if (pTechno.IsNull) return;               // 判空
Pointer<T>.AsPointer(ref obj);            // 取地址
```

**添加新类或成员时的约束（常见坑）：**

- `bool` 字段必须用 `byte` 或 `Bool` 类型，不可直接写 `bool`
- 不可在 struct 中直接写 `string`，改用 `AnsiStringPointer` / `UniStringPointer`
- 泛型嵌套导致循环引用时，改用 `byte` 字段 + property 包装
- 虚函数调用用 `Helpers.GetVirtualFunctionPointer(ptr, virtualIndex)`
- Fastcall 函数调用用 `ASM.FastCallTransferStation`，不能用 Thiscall 调用约定

---

### 二、InteropUtils（`InteropUtils/`）

桥接外部模块的互操作层。目前只有一个外部模块：**Phobos.dll**（另一个游戏扩展，提供额外引擎功能）。

**主要文件（`Phobos/` 目录）：**

- `TechnoExt.cs` —— 包装 Phobos 的单位相关功能（如 `ConvertToType`：将单位转换为另一种类型）
- `AttachEffect.cs`、`BulletExt.cs`、`EventExt.cs` —— 其他 Phobos 功能封装

**使用注意：**

- 所有对 Phobos 的调用都通过 `[DllImport]` P/Invoke 进行
- 向 Phobos 注册回调委托时，**必须用静态字段持有委托引用**，防止 GC 回收后非托管层调用时崩溃（Access Violation）：

```csharp
// ✅ 正确
private static MyDelegate _instance;
_instance = new MyDelegate(MyMethod);
RegisterCallback(_instance);

// ❌ 错误 —— 委托会被 GC 回收
RegisterCallback(new MyDelegate(MyMethod));
```

如需接入新的外部模块，在此项目下新建对应子目录，按 `Phobos/` 的模式封装即可。

---

### 三、Extension（`Extension/`）

主扩展逻辑，包含以下子系统：

```
Extension/
├── AttackWave/     攻击波次系统（★ 重点，见下文详述）
├── Decorators/     装饰器系统（可复用行为组件，挂载在 Techno/Bullet 上）
├── Ext/            游戏对象扩展数据（Container 模式，为游戏对象附加额外状态）
├── Mutators/       Mutator 随机因子系统（★ 重点，见下文详述）
├── Script/         脚本系统（事件驱动行为，附加到 Techno/Bullet，支持热重载）
└── Utilities/      工具类（INIParser、MiscHelpers、TechnoTypeHelpers、ExtensionReference 等）
```

#### Ext / Container 系统

`Extension<T>` —— 为游戏对象附加额外状态数据的容器，与游戏对象的生命周期（CTOR/DTOR/Save/Load）同步。

- `TechnoExt` —— 扩展 TechnoClass，最常用；通过 `TechnoExt.ExtMap.Find(pTechno)` 获取
- `ScenarioExt` —— 扩展 ScenarioClass，全局单例，通过 `ScenarioExt.Global()` 获取；**持有 `AttackWaveManager` 和 `MutatorCacheManager` 两个全局管理器**
- 扩展数据类必须继承 `Extension<T>` 且标记 `[Serializable]`
- `[NonSerialized]` 用于标记不需要序列化的字段（如 `OwnerObject` 指针本身）

持久引用游戏对象用 `ExtensionReference<TExt>`，而不是直接存裸 `Pointer<T>`（游戏对象可能随时被销毁，存档后指针也会失效）：

```csharp
// 声明（必须在 [Serializable] 的类中）
private ExtensionReference<TechnoExt> targetRef;

// 赋值
targetRef = new ExtensionReference<TechnoExt>(TechnoExt.ExtMap.Find(pTechno));

// 安全读取
if (targetRef.TryGet(out TechnoExt ext))
{
    // ext 有效，使用 ext.OwnerObject
}
```

**前提条件**：被引用的对象类型必须已有对应的 `Container`/`ExtMap`（如 `TechnoExt.ExtMap`）。没有 ExtMap 的类型不能使用此工具。它同时处理了存档/读档时的指针 swizzle，字段本身需标记为 `[Serializable]`。

#### Decorator 系统

`Decorator` / `IEventDecorator` —— 可复用行为组件，挂载到实现了 `IDecorative` 接口的对象（如 `TechnoExt`）。支持 `OnUpdate`、`OnFire`、`OnReceiveDamage`、`AfterReceiveDamage` 等事件回调。

- 所有 Decorator 子类需标记 `[Serializable]`
- `Priority` 属性控制执行顺序（值越大越先执行）

---

## Mutator 随机因子系统（★）

这是项目的核心玩法系统，**经常需要添加或修改**。Mutator 是游戏中随机选取并生效的难度修饰器，每局游戏从池中抽取若干因子同时作用。

### 类层次结构

```
Mutator（抽象基类）
├── TargetTechnoMutator   以特定单位为目标的因子
├── TargetCellMutator     以地图格子为目标的因子
└── BuffMutator           向单位施加持续 Buff（Decorator）的因子
```

可以直接继承 `Mutator`，也可以继承三个子类之一。子类提供了针对各自场景的 helper，但不是强制的。

### Mutator 基类：必须实现的成员

```csharp
public abstract string UIName { get; }           // 显示名（中文）
public abstract string Description { get; }      // 描述文本（中文）
public abstract bool IsAvailableInRPG { get; }   // 是否在 RPG 模式中可用
public abstract int Score { get; }               // 因子强度分值（1=最弱，10=最强）

// 必须提供此签名的构造函数
public MyMutator(Pointer<HouseClass> owner) : base(owner) { }
```

### Mutator 基类：生命周期虚方法

```csharp
public virtual void Init(bool isInitial = true) { }  // 因子生效时调用（开局或中途激活）
public virtual void Uninit() { }                      // 因子失效时调用
public virtual bool Update() { }                      // 每帧调用，返回 false 表示该因子应被移除
```

调用 `base.XXX()` 保留基类行为；`Update()` 返回 `false` 时，因子会从 `Mutator.Array` 中删除。

### Mutator 基类：常用 Helper

```csharp
TimeToFrame(double min, double sec)           // 时间转帧数（会根据 Testing 标志自动缩放）
IsOnOurSide(Pointer<HouseClass> h)            // 判断是否为友方（Mutator 拥有者一侧）
IsOnTheirSide(Pointer<HouseClass> h)          // 判断是否为敌方（玩家一侧）
GetRandomHouseOnOurSide()                     // 随机取一个友方 House
CreateTeamAtCrd(...)                          // 在指定坐标创建一组单位
IsBuildable(type) / IsHero(type) / GetLevel(type)  // 单位类型判断工具
```

### TargetTechnoMutator

用于需要选取具体单位作为目标的因子（示例：`AggressiveDeployment`、`BoomBots`）。

```csharp
// 必须实现：判断某个 Techno 是否为有效目标
protected abstract bool IsTechnoValid(Pointer<TechnoClass> techno);
```

额外 helper：`GetKeepAliveAbility(techno)` —— 返回单位的"存活价值"等级（ConYard=4，Factory=3，Building=2，Foot=1，Insignificant=0），可用于判断是否值得针对该玩家发起进攻。

### TargetCellMutator

用于需要选取地图格子作为目标的因子（示例：`OrbitalStrike`、`LaserDrill`）。

预先计算好的格子缓存（来自 `MutatorCacheManager`，开局初始化一次）：

```csharp
UsableCells          // 地图上可用的格子
NonSafeZoneCells     // 非安全区格子（安全区以矿柱为中心，半径20格）
EdgeCells            // 所有边缘格子
LeftEdgeCells / RightEdgeCells / UpEdgeCells / DownEdgeCells  // 各方向边缘
```

内置 shot 追踪：基类的 `Update()` 会自动维护 `ActivatedShots` 列表，子类只需向列表添加 `ShotWithPreImpactAnim` 实例即可。

### BuffMutator

用于向单位施加持续 Buff（通过 Decorator 实现）的因子（示例：`BlackDeath`、`Fear`、`EvasiveManeuvers`）。

```csharp
// 必须实现：决定 Buff 挂在哪一方的单位上
// "Enemy" 是相对于 Mutator 拥有者（即 AI 方）而言的，即玩家方
// - Buff 是减益效果（削弱玩家）→ true，Buff 挂在玩家单位上
// - Buff 是增益效果（增强 AI）→ false，Buff 挂在 AI 单位上
// - 效果不明确时，询问用户再决定
protected abstract bool IsBuffEnemy { get; }

// 必须实现：给指定 Techno 添加/移除 Buff
protected abstract void BuffTechno(Pointer<TechnoClass> techno);
protected abstract void UnbuffTechno(Pointer<TechnoClass> techno);
```

可选回调（因子激活期间自动被系统调用）：

```csharp
public virtual void OnTechnoCTOR(Pointer<TechnoClass> techno) { }               // 有新单位生成时
public virtual void OnTechnoChangeOwner(Pointer<TechnoClass> techno, ...) { }   // 单位易主时
```

**标准套路**：`Init()` 遍历现有单位并调用 `BuffTechno`，`Uninit()` 遍历并调用 `UnbuffTechno`，`OnTechnoCTOR` 给新生成的单位补 Buff。

### 注册新 Mutator 的步骤

1. 继承合适的抽象子类（或直接继承 `Mutator`），加 `[Serializable]`
2. 实现所有 abstract 成员
3. 在 `MutatorRandomizer.cs` 的 `MutatorDataBase` 字典中新增一条记录（类名、SWID、分数；`SWID` 为空字符串表示代码实现，非空表示 INI 实现）。先检查该字典末尾的注释区域，如果已有同名条目，分数从那里获取（注释条目由用户在实现完毕后手动取消注释，不要代劳）。`MutatorDesc` 是所有因子中文名到描述的参考字典，新建 Mutator 时从中查找对应的描述，填入 `Description` 属性
4. 如果用到了新的挂在 Techno 上的 Decorator 类型，在 `TechnoDecoratorIDs` 枚举中注册一个新 ID。突变因子附加的 Decorator 通常不会叠加，每种 Decorator 对应一个固定 ID 即可。新 ID 取当前枚举最大值 +1

---

## AttackWave 攻击波次系统（★）

这是游戏的 AI 攻击波次系统，**经常需要添加或修改**。整个系统在 `AttackWave/` 目录下，全局状态由 `ScenarioExt.Global().AttackWaveManager` 持有。

### 核心类

**`AttackWaveManager`**（单例，通过 `AttackWaveManager.Instance` 访问）

- `Schedule`：攻击波次时间表（`List<AttackWaveScheduleNode>`），定义何时、以何种规格派遣进攻
- `TypeArray`：所有可用的攻击波次类型（`List<AttackWaveType>`）
- `Array`：当前活跃的波次实例（`List<AttackWave>`）
- `LoadFromINI()`：初始化 Schedule 和 TypeArray（**目前为硬编码**，TODO 从 INI 读取）

**`AttackWaveScheduleNode`**（时间表节点）

定义一次派遣任务的参数：

```csharp
new AttackWaveScheduleNode(
    spawningFrame: ScenarioExt.TimeToFrame(2, 0),   // 触发时间（2分0秒）
    loopCount: -1,                                   // 循环次数，-1=无限循环
    loopDelay: ScenarioExt.TimeToFrame(2, 0),        // 每次循环间隔
    techLevel: 1,       // 单位科技等级（1-7，传0则按游戏时长自动计算）
    amountLevel: 1,     // 单位数量等级
    script: AttackWaveScriptNode.DefaultScript()     // 生成后的行为脚本
)
```

**`AttackWaveType`**

定义一种进攻波的单位构成，按科技等级（1-7）分层，每层是一组 `TypeRecordData`：

```csharp
new AttackWaveType()
{
    UIName = "美国 空军",
    SideIdx = 0,  // 所属阵营
    TypeDataTable = new List<List<TypeRecordData>>()
    {
        // Tech Level 1
        new List<TypeRecordData>() { new TypeRecordData("E1") },
        // Tech Level 2
        new List<TypeRecordData>() { new TypeRecordData("E1"), new TypeRecordData("ENFO") },
        // ...以此类推，共7档
    }
}
```

**`AttackWaveScriptNode`** 和 **`AttackWaveScriptAction`**

定义波次生成后的行为序列：

```csharp
AttackRandomPlayerBase  // 进攻随机玩家基地（目标被消灭后自动停止）
AttackNearestPlayerBase // 进攻最近玩家基地
GuardNearestAIBase      // 守卫最近 AI 基地
GoHunting               // 全图搜索作战（不会自动停止）
GuardCurrentPosition    // 原地守卫
Retreat                 // 撤退（不会自动停止）

// 默认脚本：先进攻随机玩家，目标消灭后全图搜索
AttackWaveScriptNode.DefaultScript()
// => [AttackRandomPlayerBase, GoHunting]
```

**`AttackWave`**（波次实例）

实际管理单位生成、移动和脚本执行的类。由 `AttackWaveManager` 按 Schedule 自动创建，一般不需要手动实例化。

### 修改攻击波次的常见操作

- **添加新攻击波次类型**：在 `AttackWaveManager.LoadFromINI()` 的 `TypeArray` 初始化列表中添加新的 `AttackWaveType`
- **添加新时间表节点**：在 `AttackWaveManager.LoadFromINI()` 的 `Schedule` 初始化列表中添加新的 `AttackWaveScheduleNode`

---

## 构建与部署

> ⚠️ 此部分只需读取参考，不要修改构建配置或部署脚本。

用 Visual Studio 打开 `Projects.sln`，使用 **Debug** 配置编译。

`Mutators/deploy.bat` 负责将 `*Script.cs` 文件同步到游戏目录，并清理 Build/Packages 缓存，路径硬编码在 bat 中，换机器需要手动修改。

---

## 通用编码约定

- 所有持久化数据类（Mutator、Decorator、Extension 子类等）必须标记 `[Serializable]`，否则存档读档会崩溃
- 不要在 Ext/Container 之外长期存裸 `Pointer<T>`；需要持久引用游戏对象时用 `ExtensionReference<TExt>`
- 使用任何 `Pointer` 前先检查 `.IsNull`
- 手动放置单位前 `++Game.IKnowWhatImDoing`，放置后 `--`，这是绕过游戏内部检查的标准做法
