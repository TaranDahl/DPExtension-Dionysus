using DynamicPatcher;
using Extension.Ext;
using InteropUtils;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Extension.Mutators
{
    /// <summary>一个可用突变因子的展示/选择条目。</summary>
    public class MutatorEntry
    {
        /// <summary>因子键名（类名 / SWID 名），用于激活与图标文件。</summary>
        public string ClassName;
        /// <summary>中文名。</summary>
        public string Name;
        /// <summary>文字描述。</summary>
        public string Description;
        /// <summary>强度分值。</summary>
        public int Score;
        /// <summary>是否在 RPG 模式可用。</summary>
        public bool AvailableInRPG;
        /// <summary>是否禁用（被 Banned，或 IsRPG 下 RPG 不可用）：UI 置灰、不参与随机。</summary>
        public bool Disabled;
        /// <summary>代码因子实例（仅代码实现因子非空，用于读取属性）。</summary>
        public Mutator MutatorInstance;
    }

    /// <summary>
    /// 突变因子选择器。
    /// 读取 <see cref="MutatorSelectionConfig"/>，按模式自动启动或显示 Manual 选择 UI，
    /// 并在屏幕右侧以 IconStrip 常驻显示已激活因子的图标。
    /// </summary>
    public static class MutatorSelector
    {
        // ============ 配置与注册表 ============

        public static MutatorSelectionConfig Config { get; private set; }
        public static List<MutatorEntry> Entries { get; private set; } = new List<MutatorEntry>();
        private static readonly Dictionary<string, MutatorEntry> EntryByName = new Dictionary<string, MutatorEntry>();

        // ============ 状态 ============

        private static bool Started = false;
        private static readonly HashSet<string> PendingSelection = new HashSet<string>();
        // INI 实现因子不进入 Mutator.Array，单独记录以在激活条中显示
        private static readonly HashSet<string> ActivatedINIMutators = new HashSet<string>();
        private static List<string> LastStripState;

        // ============ UI 句柄 ============

        private static IntPtr TriggerIcon = IntPtr.Zero;
        private static IntPtr ActiveStrip = IntPtr.Zero;
        private static readonly List<IntPtr> StripItems = new List<IntPtr>();
        private static IntPtr CurrentDialog = IntPtr.Zero;
        private static bool DialogOpen = false;

        // 随机因子数量指示器
        private static int CountValue = 1;
        private static IntPtr CountLabelHandle = IntPtr.Zero;
        private static int MaxSelectableCount;
        // 数量滚轮：隐藏的 1x1 PageView，每页对应一个数量值
        private static IntPtr CountWheelPageView = IntPtr.Zero;
        private static int LastCountWheelPage = -1;

        // 指定因子网格页码
        private static IntPtr GridPageLabel = IntPtr.Zero;
        private static IntPtr GridPageView = IntPtr.Zero;
        private static int LastGridPage = -1;

        // ============ 生命周期 ============

        /// <summary>
        /// 新局初始化：构建注册表、应用随机池过滤，按模式自动启动或显示 Manual UI。
        /// 由 ScenarioExt.NewGameInitOnce 在 MutatorRandomizer.Init() 之后调用。
        /// </summary>
        public static void Start()
        {
            if (Started)
                return;
            Started = true;

            Config = RulesExt.Global()?.MutatorSelection ?? new MutatorSelectionConfig();

            // 应用到随机池：禁选 + RPG 过滤
            MutatorRandomizer.BannedMutators = new HashSet<string>(Config.BannedMutators);
            MutatorRandomizer.FilterRPG = Config.IsRPG;

            BuildRegistry();
            MaxSelectableCount = Math.Max(1, Entries.Count(e => !e.Disabled));

            // 常驻激活条（空时零尺寸不可见）
            OpenActiveStrip();

            Logger.Log("[MutatorSelector] Start: Mode={0}, Entries={1}\n", Config.Mode, Entries.Count);

            switch (Config.Mode)
            {
                case MutatorSelectionMode.LevelRandom:
                    ActivateNames(MutatorRandomizer.BrutalPlusRandom(Config.BrutalLevel));
                    break;
                case MutatorSelectionMode.SimpleRandom:
                    ActivateNames(MutatorRandomizer.SimpleRandom(Config.RandomCount));
                    break;
                case MutatorSelectionMode.Force:
                    ActivateNames(Config.ForcedMutators);
                    break;
                case MutatorSelectionMode.Manual:
                    ShowTriggerIcon();
                    break;
            }

            RefreshActiveStrip();
        }

        /// <summary>
        /// 每帧更新（由主循环钩子调用）：
        /// ① 同步指定因子网格页码（滚轮翻页由框架内部完成，C# 侧只同步标签）；
        /// ② 同步数量滚轮 PageView 的翻页到 CountValue。
        /// </summary>
        public static void Update()
        {
            if (GridPageView != IntPtr.Zero && GridPageLabel != IntPtr.Zero)
            {
                PhobosUIExt.PageView_GetPageIndex(GridPageView, out int index);
                if (index != LastGridPage)
                {
                    LastGridPage = index;
                    UpdateGridPageLabel(GridPageView);
                }
            }

            if (CountWheelPageView != IntPtr.Zero)
            {
                PhobosUIExt.PageView_GetPageIndex(CountWheelPageView, out int page);
                if (page != LastCountWheelPage)
                {
                    LastCountWheelPage = page;
                    CountValue = Math.Max(1, Math.Min(MaxSelectableCount, page + 1));
                    UpdateCountLabel();
                }
            }
        }

        // ============ 注册表 ============

        private static void BuildRegistry()
        {
            Entries.Clear();
            EntryByName.Clear();

            foreach (var kv in MutatorRandomizer.GetAvailableMutators())
            {
                var entry = BuildEntry(kv.Key, kv.Value);
                if (entry == null)
                    continue;

                entry.Disabled = Config.BannedMutators.Contains(kv.Key)
                    || (Config.IsRPG && !entry.AvailableInRPG);

                Entries.Add(entry);
                EntryByName[kv.Key] = entry;
            }

            // 按分数升序、同分按中文名排序，方便浏览
            Entries.Sort((a, b) =>
            {
                int cmp = a.Score.CompareTo(b.Score);
                return cmp != 0 ? cmp : string.CompareOrdinal(a.Name, b.Name);
            });
        }

        /// <summary>归一化 SW 的 UIName：去除 "(：" / "：" 前缀及括号，用于显示与描述查询。</summary>
        private static string NormalizeUIName(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return raw;

            string s = raw.Trim();
            s = s.TrimStart('(').TrimStart('：').TrimStart(':').Trim();
            s = s.TrimEnd(')').Trim();
            return s;
        }

        private static MutatorEntry BuildEntry(string name, (string SWID, int Score) pair)
        {
            var entry = new MutatorEntry { ClassName = name, Score = pair.Score };

            if (pair.SWID == "")
            {
                // 代码实现因子：反射实例化读取属性
                try
                {
                    var type = Type.GetType("Extension.Mutators." + name);
                    if (type == null || type.IsAbstract || !typeof(Mutator).IsAssignableFrom(type))
                    {
                        Logger.Log("[MutatorSelector] {0} is not a usable mutator type.\n", name);
                        return null;
                    }
                    var mutator = (Mutator)Activator.CreateInstance(type, Pointer<HouseClass>.Zero);
                    entry.MutatorInstance = mutator;
                    entry.Name = mutator.UIName;
                    entry.Description = mutator.Description;
                    entry.AvailableInRPG = mutator.IsAvailableInRPG;
                }
                catch (Exception ex)
                {
                    Logger.Log("[MutatorSelector] Create {0} failed: {1}\n", name, ex);
                    return null;
                }
            }
            else
            {
                // INI 实现因子：从 SW 读中文名，描述查 MutatorDesc
                var sw = SuperWeaponTypeClass.ABSTRACTTYPE_ARRAY.Find(pair.SWID);
                if (sw.IsNull)
                {
                    Logger.Log("[MutatorSelector] {0} SW {1} not found.\n", name, pair.SWID);
                    return null;
                }
                string chineseName = NormalizeUIName(sw.Ref.Base.UIName); // UniStringPointer → string
                entry.Name = string.IsNullOrEmpty(chineseName) ? name : chineseName;
                entry.Description = MutatorRandomizer.GetMutatorDescription(entry.Name);
                entry.AvailableInRPG = true;
            }

            return entry;
        }

        /// <summary>按类名对应的 &lt;类名&gt;.pcx 给按钮设置图标；成功返回 true，失败（素材缺失）返回 false。</summary>
        private static bool TrySetButtonIcon(IntPtr button, string className)
        {
            if (button == IntPtr.Zero || string.IsNullOrEmpty(className))
                return false;

            try
            {
                int hr = PhobosUIExt.Button_SetIconFromFile(button, className + ".pcx");
                Logger.Log("[MutatorSelector] SetIconFromFile({0}.pcx) hr=0x{1:X8} ({2})\n", className, hr,
                    hr == HResult.S_OK ? "OK" : (hr == HResult.S_FALSE ? "S_FALSE" : "FAIL"));
                return hr == HResult.S_OK;
            }
            catch (Exception ex)
            {
                Logger.Log("[MutatorSelector] SetIconFromFile({0}.pcx) threw: {1}\n", className, ex);
                return false;
            }
        }

        // ============ 激活 ============

        /// <summary>逐个激活指定因子（复用 MutatorRandomizer.ActiveMutatorByName）。</summary>
        public static void ActivateNames(IEnumerable<string> names)
        {
            if (names == null)
                return;

            foreach (var name in names)
            {
                if (string.IsNullOrEmpty(name))
                    continue;

                if (MutatorRandomizer.ActiveMutatorByName(name))
                {
                    if (EntryByName.TryGetValue(name, out var entry) && entry.MutatorInstance == null)
                        ActivatedINIMutators.Add(name);
                }
                else
                {
                    Logger.Log("[MutatorSelector] Activate {0} failed.\n", name);
                }
            }

            RefreshActiveStrip();
        }

        // ============ 激活条 ============

        private static void OpenActiveStrip()
        {
            IntPtr strip;
            if (PhobosUIExt.CreateIconStrip(0, 0, 60, 48, 4, out strip) != HResult.S_OK || strip == IntPtr.Zero)
                return;

            PhobosUIExt.IconStrip_SetItemSize(strip, 60, 48);
            PhobosUIExt.IconStrip_SetSpacing(strip, 4);
            // 右侧、垂直居中偏下，避开屏幕顶部（PCX 绘制触及屏幕顶部区域会崩）。
            // 不用 Right 锚点：锚点会随激活条高度变化自动重新居中，导致条顶部上下漂移。
            int stripX = DSurface.ViewBounds.Width - 48 - 60;
            int stripY = DSurface.ViewBounds.Height / 2 + 32;
            PhobosUIExt.SetPos(strip, stripX, stripY);
            PhobosUIExt.Open(strip, UIExtModal.None);
            ActiveStrip = strip;
        }

        /// <summary>
        /// 刷新右侧激活条（由选择器激活及主循环钩子调用）。
        /// 仅在激活集合变化时重建，避免每帧重建。
        /// </summary>
        public static void RefreshActiveStrip()
        {
            if (ActiveStrip == IntPtr.Zero)
                return;

            // 快速路径：因子激活/失效都会改变总量，数量未变化视为未变化，避免每帧重建
            int total = Mutator.Array.Count + ActivatedINIMutators.Count;
            if (LastStripState != null && total == LastStripState.Count)
                return;

            // 代码因子以 Mutator.Array 为准（死亡/失效自动移除），INI 因子以激活记录为准
            var names = new HashSet<string>();
            foreach (var mutator in Mutator.Array)
            {
                if (mutator != null)
                    names.Add(mutator.GetType().Name);
            }
            foreach (var iniName in ActivatedINIMutators)
                names.Add(iniName);

            var sorted = names.OrderBy(n => n, StringComparer.Ordinal).ToList();
            if (LastStripState != null && LastStripState.SequenceEqual(sorted))
                return;
            LastStripState = sorted;

            foreach (var item in StripItems)
                PhobosUIExt.RemoveChild(ActiveStrip, item);
            StripItems.Clear();

            foreach (var name in sorted)
            {
                if (!EntryByName.TryGetValue(name, out var entry))
                    continue;

                IntPtr icon;
                if (PhobosUIExt.CreateIconButton(0, 0, 60, 48, out icon) != HResult.S_OK || icon == IntPtr.Zero)
                    continue;

                if (!TrySetButtonIcon(icon, entry.ClassName) && !string.IsNullOrEmpty(entry.Name))
                    PhobosUIExt.SetText(icon, entry.Name[0].ToString());
                PhobosUIExt.SetTooltip(icon, entry.Name, entry.Description);

                PhobosUIExt.AddChild(ActiveStrip, icon);
                StripItems.Add(icon);
            }

            PhobosUIExt.IconStrip_Refresh(ActiveStrip);
        }

        // ============ Manual UI：触发图标 ============

        private static void ShowTriggerIcon()
        {
            IntPtr icon;
            if (PhobosUIExt.CreateIconButton(0, 0, 60, 48, out icon) != HResult.S_OK || icon == IntPtr.Zero)
                return;

            // 触发按钮位于右侧垂直居中，PCX 图标不会触及屏幕顶部区域
            if (!TrySetButtonIcon(icon, "MutStart"))
                PhobosUIExt.SetText(icon, "选");
            PhobosUIExt.SetTooltip(icon, "突变因子选择", "点击打开突变因子选择器");
            PhobosUIExt.SetAnchor(icon, UIExtAnchor.Right, -40, 0); // 右侧垂直居中（PCX 绘制触及屏幕顶部区域会崩）
            PhobosUIExt.Button_SetOnClick(icon, () => OpenModeDialog());
            PhobosUIExt.Open(icon, UIExtModal.None);
            TriggerIcon = icon;
        }

        // ============ Manual UI：对话框通用 ============

        private static int CenterX(int width) => (DSurface.ViewBounds.Width - width) / 2;
        private static int CenterY(int height) => (DSurface.ViewBounds.Height - height) / 2;

        private static void CloseDialog()
        {
            if (CurrentDialog != IntPtr.Zero)
            {
                PhobosUIExt.Close(CurrentDialog);
                CurrentDialog = IntPtr.Zero;
            }

            // 清除仅对话框内使用的句柄，避免每帧 Update 访问已销毁控件
            GridPageView = IntPtr.Zero;
            GridPageLabel = IntPtr.Zero;
            LastGridPage = -1;
            CountLabelHandle = IntPtr.Zero;
            CountWheelPageView = IntPtr.Zero;
            LastCountWheelPage = -1;
        }

        /// <summary>选择完成：关闭触发图标、退出 Manual 模式。</summary>
        private static void FinishSelection()
        {
            DialogOpen = false;
            if (TriggerIcon != IntPtr.Zero)
            {
                PhobosUIExt.Close(TriggerIcon);
                TriggerIcon = IntPtr.Zero;
            }
        }

        // ============ Manual UI：选择模式 ============

        private static void OpenModeDialog()
        {
            if (DialogOpen)
                return;
            DialogOpen = true;

            const int dw = 380, dh = 240;
            int dx = CenterX(dw);
            int dy = CenterY(dh);

            IntPtr dialog;
            if (PhobosUIExt.CreateDialog(dx, dy, dw, dh, "突变因子选择", out dialog) != HResult.S_OK || dialog == IntPtr.Zero)
            {
                DialogOpen = false;
                return;
            }
            CurrentDialog = dialog;

            IntPtr btnBrutal, btnRandom, btnManual;
            PhobosUIExt.CreateButton(0, 0, 300, 44, "残酷+", out btnBrutal);
            PhobosUIExt.CreateButton(0, 0, 300, 44, "随机因子", out btnRandom);
            PhobosUIExt.CreateButton(0, 0, 300, 44, "指定因子", out btnManual);

            PhobosUIExt.SetTooltip(btnBrutal, "残酷+", "随机启用突变因子，直到其分数总和达到一定数值。");
            PhobosUIExt.SetTooltip(btnRandom, "随机因子", "随机启用一定数量的突变因子。");
            PhobosUIExt.SetTooltip(btnManual, "指定因子", "由你自己选择将要挑战的突变因子。");

            int btnX = dx + (dw - 300) / 2;
            int btnY = dy + 20;
            PhobosUIExt.SetPos(btnBrutal, btnX, btnY);
            PhobosUIExt.SetPos(btnRandom, btnX, btnY + 56);
            PhobosUIExt.SetPos(btnManual, btnX, btnY + 112);

            PhobosUIExt.Button_SetOnClick(btnBrutal, () => { CloseDialog(); OpenBrutalLevelDialog(); });
            PhobosUIExt.Button_SetOnClick(btnRandom, () => { CloseDialog(); OpenCountDialog(); });
            PhobosUIExt.Button_SetOnClick(btnManual, () => { CloseDialog(); OpenMutatorGridDialog(); });

            PhobosUIExt.AddChild(dialog, btnBrutal);
            PhobosUIExt.AddChild(dialog, btnRandom);
            PhobosUIExt.AddChild(dialog, btnManual);

            PhobosUIExt.Dialog_SetCloseAction(dialog, () => { DialogOpen = false; CloseDialog(); });
            PhobosUIExt.Open(dialog, UIExtModal.BlockTactical);
        }

        // ============ Manual UI：残酷+ 等级 ============

        private static void OpenBrutalLevelDialog()
        {
            const int dw = 360, dh = 300;
            int dx = CenterX(dw);
            int dy = CenterY(dh);

            IntPtr dialog;
            if (PhobosUIExt.CreateDialog(dx, dy, dw, dh, "选择残酷+等级", out dialog) != HResult.S_OK || dialog == IntPtr.Zero)
                return;
            CurrentDialog = dialog;

            const int bw = 140, bh = 52, gapX = 16, gapY = 12;
            int startX = dx + (dw - (2 * bw + gapX)) / 2;
            int startY = dy + 16;

            for (int i = 0; i < 6; i++)
            {
                int level = i + 1;
                IntPtr btn;
                PhobosUIExt.CreateButton(0, 0, bw, bh, "残酷+" + level, out btn);

                if (MutatorRandomizer.TryGetBrutalPlusParams(level, out var sumMin, out var sumMax, out var countMin, out var countMax))
                {
                    PhobosUIExt.SetTooltip(btn, "残酷+" + level,
                        string.Format("随机启用 {0}-{1} 个突变因子，总分达到 {2}-{3}。", countMin, countMax, sumMin, sumMax));
                }
                else
                {
                    PhobosUIExt.SetTooltip(btn, "残酷+" + level, "未知等级。");
                }

                int col = i % 2, row = i / 2;
                PhobosUIExt.SetPos(btn, startX + col * (bw + gapX), startY + row * (bh + gapY));

                PhobosUIExt.Button_SetOnClick(btn, () =>
                {
                    CloseDialog();
                    ActivateNames(MutatorRandomizer.BrutalPlusRandom(level));
                    FinishSelection();
                });

                PhobosUIExt.AddChild(dialog, btn);
            }

            PhobosUIExt.Dialog_SetCloseAction(dialog, () => { DialogOpen = false; CloseDialog(); });
            PhobosUIExt.Open(dialog, UIExtModal.BlockTactical);
        }

        // ============ Manual UI：随机因子数量 ============

        private static void OpenCountDialog()
        {
            const int dw = 320, dh = 200;
            int dx = CenterX(dw);
            int dy = CenterY(dh);

            CountValue = Math.Max(1, Math.Min(Config.RandomCount, MaxSelectableCount));

            IntPtr dialog;
            if (PhobosUIExt.CreateDialog(dx, dy, dw, dh, "选择随机因子数量", out dialog) != HResult.S_OK || dialog == IntPtr.Zero)
                return;
            CurrentDialog = dialog;

            IntPtr label;
            PhobosUIExt.CreateLabel(0, 0, "", out label);
            PhobosUIExt.SetSize(label, 60, 40);
            PhobosUIExt.SetPos(label, dx + (dw - 60) / 2, dy + 20);
            CountLabelHandle = label;

            // 滚轮支持：API 无通用滚轮回调，复用框架 PageView 的内置滚轮翻页。
            // 在数量指示器区域放一个隐藏的 1x1 分页视图（不绘制），每页对应一个数量值；
            // 滚轮翻页后由每帧 MutatorSelector.Update() 同步回 CountValue。
            IntPtr wheelPv;
            PhobosUIExt.CreatePageView(dx + (dw - 68) / 2, dy + 14, 68, 52, out wheelPv);
            PhobosUIExt.PageView_SetGrid(wheelPv, 1, 1, 1, 1, 0, 0);
            for (int p = 0; p < MaxSelectableCount; p++)
            {
                IntPtr placeholder;
                PhobosUIExt.CreatePanel(0, 0, 1, 1, out placeholder);
                PhobosUIExt.AddChild(wheelPv, placeholder);
            }
            PhobosUIExt.PageView_SetGrid(wheelPv, 1, 1, 1, 1, 0, 0); // 添加完占位后重排分页
            PhobosUIExt.PageView_SetPage(wheelPv, CountValue - 1);
            CountWheelPageView = wheelPv;
            LastCountWheelPage = CountValue - 1;

            IntPtr btnUp, btnDown, btnOk;
            PhobosUIExt.CreateButton(0, 0, 56, 34, "▲", out btnUp);
            PhobosUIExt.CreateButton(0, 0, 56, 34, "▼", out btnDown);
            PhobosUIExt.CreateButton(0, 0, 120, 40, "确定", out btnOk);

            PhobosUIExt.SetPos(btnUp, dx + dw / 2 + 40, dy + 18);
            PhobosUIExt.SetPos(btnDown, dx + dw / 2 + 40, dy + 56);
            PhobosUIExt.SetPos(btnOk, dx + (dw - 120) / 2, dy + dh - 56);

            PhobosUIExt.Button_SetOnClick(btnUp, () => AdjustCount(1));
            PhobosUIExt.Button_SetOnClick(btnDown, () => AdjustCount(-1));
            PhobosUIExt.Button_SetOnClick(btnOk, () =>
            {
                CloseDialog();
                ActivateNames(MutatorRandomizer.SimpleRandom(CountValue));
                FinishSelection();
            });

            PhobosUIExt.AddChild(dialog, label);
            PhobosUIExt.AddChild(dialog, wheelPv);
            PhobosUIExt.AddChild(dialog, btnUp);
            PhobosUIExt.AddChild(dialog, btnDown);
            PhobosUIExt.AddChild(dialog, btnOk);

            UpdateCountLabel();

            PhobosUIExt.Dialog_SetCloseAction(dialog, () => { DialogOpen = false; CloseDialog(); });
            PhobosUIExt.Open(dialog, UIExtModal.BlockTactical);
        }

        private static void AdjustCount(int delta)
        {
            CountValue = Math.Max(1, Math.Min(MaxSelectableCount, CountValue + delta));
            // 同步隐藏滚轮 PageView 的页码，避免按钮与滚轮混用时失步
            if (CountWheelPageView != IntPtr.Zero)
            {
                PhobosUIExt.PageView_SetPage(CountWheelPageView, CountValue - 1);
                LastCountWheelPage = CountValue - 1;
            }
            UpdateCountLabel();
        }

        private static void UpdateCountLabel()
        {
            if (CountLabelHandle == IntPtr.Zero)
                return;
            PhobosUIExt.SetText(CountLabelHandle, CountValue.ToString());
        }

        // ============ Manual UI：指定因子网格 ============

        private static void OpenMutatorGridDialog()
        {
            const int columns = 4, rows = 4, cellW = 86, cellH = 54, gapX = 6, gapY = 6;
            int gridW = columns * cellW + (columns - 1) * gapX;   // 362
            int gridH = rows * cellH + (rows - 1) * gapY;         // 234

            int dw = gridW + 40;
            int dh = gridH + 128;
            int dx = CenterX(dw);
            int dy = CenterY(dh);

            IntPtr dialog;
            if (PhobosUIExt.CreateDialog(dx, dy, dw, dh, "选择突变因子", out dialog) != HResult.S_OK || dialog == IntPtr.Zero)
                return;
            CurrentDialog = dialog;

            IntPtr title;
            PhobosUIExt.CreateLabel(dx + 20, dy + 10, "点击勾选因子，确定后生效", out title);

            // 分页网格（4x4），滚轮翻页由框架 PageView 内置支持
            IntPtr pageView;
            PhobosUIExt.CreatePageView(dx + 20, dy + 36, gridW, gridH, out pageView);
            PhobosUIExt.PageView_SetGrid(pageView, columns, rows, cellW, cellH, gapX, gapY);
            GridPageView = pageView;
            LastGridPage = -1;

            foreach (var entry in Entries)
            {
                IntPtr cell;
                PhobosUIExt.CreatePanel(0, 0, cellW, cellH, out cell);
                PhobosUIExt.SetBackColor(cell, 0, 0, 0, 40);
                PhobosUIExt.SetBorder(cell, true,
                    entry.Disabled ? unchecked((int)0x00707070) : unchecked((int)0x00FFFFFF));
                PhobosUIExt.SetTooltip(cell, entry.Name, entry.Description);

                IntPtr icon;
                PhobosUIExt.CreateIconButton(3, 3, 60, 48, out icon);
                if (!TrySetButtonIcon(icon, entry.ClassName) && !string.IsNullOrEmpty(entry.Name))
                    PhobosUIExt.SetText(icon, entry.Name[0].ToString());

                IntPtr checkBox;
                PhobosUIExt.CreateCheckBox(65, 3, 20, 48, "", out checkBox);
                PhobosUIExt.SetChecked(checkBox, PendingSelection.Contains(entry.ClassName));

                if (entry.Disabled)
                {
                    // 禁用：置灰且不注册任何回调（点击无效）
                    PhobosUIExt.SetEnabled(cell, false);
                }
                else
                {
                    string name = entry.ClassName;
                    PhobosUIExt.Button_SetOnClick(icon, () => ToggleGridCell(name, checkBox));
                    PhobosUIExt.CheckBox_SetOnToggle(checkBox, newState =>
                    {
                        if (newState) PendingSelection.Add(name);
                        else PendingSelection.Remove(name);
                    });
                }

                PhobosUIExt.AddChild(cell, icon);
                PhobosUIExt.AddChild(cell, checkBox);
                PhobosUIExt.AddChild(pageView, cell);
            }

            // 添加完所有格子后重排分页
            PhobosUIExt.PageView_SetGrid(pageView, columns, rows, cellW, cellH, gapX, gapY);

            IntPtr btnPrev, btnNext, btnOk, pageLabel;
            PhobosUIExt.CreateButton(0, 0, 90, 34, "上一页", out btnPrev);
            PhobosUIExt.CreateButton(0, 0, 90, 34, "下一页", out btnNext);
            PhobosUIExt.CreateButton(0, 0, 100, 34, "确定", out btnOk);
            PhobosUIExt.CreateLabel(0, 0, "", out pageLabel);
            GridPageLabel = pageLabel;

            int bottomY = dy + dh - 52;
            PhobosUIExt.SetPos(btnPrev, dx + 20, bottomY);
            PhobosUIExt.SetPos(btnNext, dx + 120, bottomY);
            PhobosUIExt.SetPos(btnOk, dx + dw - 120, bottomY);
            PhobosUIExt.SetPos(pageLabel, dx + 230, bottomY + 4);

            PhobosUIExt.Button_SetOnClick(btnPrev, () =>
            {
                PhobosUIExt.PageView_PrevPage(pageView);
                UpdateGridPageLabel(pageView);
            });
            PhobosUIExt.Button_SetOnClick(btnNext, () =>
            {
                PhobosUIExt.PageView_NextPage(pageView);
                UpdateGridPageLabel(pageView);
            });
            PhobosUIExt.Button_SetOnClick(btnOk, () =>
            {
                CloseDialog();
                ActivateNames(PendingSelection);
                PendingSelection.Clear();
                FinishSelection();
            });

            PhobosUIExt.AddChild(dialog, title);
            PhobosUIExt.AddChild(dialog, pageView);
            PhobosUIExt.AddChild(dialog, btnPrev);
            PhobosUIExt.AddChild(dialog, btnNext);
            PhobosUIExt.AddChild(dialog, btnOk);
            PhobosUIExt.AddChild(dialog, pageLabel);

            UpdateGridPageLabel(pageView);

            PhobosUIExt.Dialog_SetCloseAction(dialog, () =>
            {
                PendingSelection.Clear();
                DialogOpen = false;
                CloseDialog();
            });
            PhobosUIExt.Open(dialog, UIExtModal.BlockTactical);
        }

        private static void ToggleGridCell(string name, IntPtr checkBox)
        {
            if (PendingSelection.Contains(name))
            {
                PendingSelection.Remove(name);
                PhobosUIExt.SetChecked(checkBox, false);
            }
            else
            {
                PendingSelection.Add(name);
                PhobosUIExt.SetChecked(checkBox, true);
            }
        }

        private static void UpdateGridPageLabel(IntPtr pageView)
        {
            if (GridPageLabel == IntPtr.Zero)
                return;
            PhobosUIExt.PageView_GetPageCount(pageView, out int count);
            PhobosUIExt.PageView_GetPageIndex(pageView, out int index);
            LastGridPage = index;
            PhobosUIExt.SetText(GridPageLabel, string.Format("第 {0} / {1} 页", index + 1, count));
        }
    }
}
