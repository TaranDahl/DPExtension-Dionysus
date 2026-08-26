using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace InteropUtils
{
    /// <summary>
    /// Phobos 通用 UI 框架（UIExt）的锚点类型。
    /// 与 Phobos C++ 侧的 <c>UIExt::Anchor</c> 枚举值保持一致。
    /// </summary>
    public enum UIExtAnchor
    {
        /// <summary>无锚点，控件位置保持绝对坐标。</summary>
        None = 0,
        /// <summary>屏幕中心。</summary>
        Center = 1,
        /// <summary>屏幕左缘。</summary>
        Left = 2,
        /// <summary>屏幕右缘。</summary>
        Right = 3,
        /// <summary>屏幕上缘。</summary>
        Top = 4,
        /// <summary>屏幕左上角。</summary>
        TopLeft = 5,
        /// <summary>屏幕右上角。</summary>
        TopRight = 6,
        /// <summary>屏幕下缘。</summary>
        Bottom = 7,
        /// <summary>屏幕左下角。</summary>
        BottomLeft = 8,
        /// <summary>屏幕右下角。</summary>
        BottomRight = 9,
    }

    /// <summary>
    /// Phobos 通用 UI 框架（UIExt）的模态级别。
    /// 与 Phobos C++ 侧的 <c>UIExt::ModalLevel</c> 枚举值保持一致。
    /// </summary>
    public enum UIExtModal
    {
        /// <summary>不阻断输入。</summary>
        None = 0,
        /// <summary>阻断根控件覆盖区域的输入。</summary>
        BlockArea = 1,
        /// <summary>阻断战术地图 / 侧边栏等游戏世界操作。</summary>
        BlockTactical = 2,
        /// <summary>阻断除当前全屏 UI 外的所有输入。</summary>
        BlockFullScreen = 3,
    }

    /// <summary>
    /// Phobos 通用 UI 框架（UIExt）的 SHP 背景对齐方式。
    /// 与 Phobos C++ 侧的 <c>UIExt::Panel::ShpAlign</c> 枚举值保持一致。
    /// </summary>
    public enum UIExtShpAlign
    {
        /// <summary>素材贴在左上角。</summary>
        TopLeft = 0,
        /// <summary>素材贴在左下角。</summary>
        BottomLeft = 1,
        /// <summary>素材贴在右上角。</summary>
        TopRight = 2,
        /// <summary>素材贴在右下角。</summary>
        BottomRight = 3,
        /// <summary>素材居中。</summary>
        Center = 4,
    }

    /// <summary>
    /// Phobos 通用 UI 框架（UIExt）的 C# 封装。
    /// 对应 Phobos Interop API：src/Interop/UIExt/UIExtApi.h（前缀 <c>UIExt_</c>）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 所有控件句柄均为不透明指针（<see cref="IntPtr"/>），由 Phobos 框架管理生命周期，
    /// 调用方不得自行释放，也不得在 <see cref="Close"/> / <see cref="CloseAll"/> 之后继续使用。
    /// </para>
    /// <para>
    /// 标准生命周期：
    /// 1. <c>Create*</c> 系列创建控件获得句柄（此时控件"未挂载"，由 Phobos 侧暂存）；
    /// 2. <see cref="AddChild"/> 组装控件树；
    /// 3. <see cref="Open"/> 打开根控件（Panel / Dialog 等），框架接管所有权并开始渲染；
    /// 4. <see cref="Close"/> / <see cref="CloseAll"/> 关闭并销毁。
    /// </para>
    /// <para>
    /// 注意：AddChild / Open 之后子控件句柄所有权已转移，直接 Close 根控件即可销毁整棵树；
    /// <see cref="Close"/> 也可以销毁一个尚未 AddChild / Open 的"未挂载"控件。
    /// </para>
    /// </remarks>
    public static class PhobosUIExt
    {
        // ============ 回调委托类型 ============

        /// <summary>
        /// 与 Phobos 中定义的按钮点击 / 对话框关闭回调类型一致。
        /// 对应 C++: <c>typedef void(__stdcall* UIExtActionCallback)(void* userData)</c>
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void UIExtActionCallback(IntPtr userData);

        /// <summary>
        /// 与 Phobos 中定义的复选框切换回调类型一致。
        /// 对应 C++: <c>typedef void(__stdcall* UIExtToggleCallback)(int checked, void* userData)</c>
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void UIExtToggleCallback(int @checked, IntPtr userData);

        /// <summary>
        /// 每个控件句柄对应的回调委托集合。
        /// 静态持有所有注册到 Phobos 的委托引用，防止 GC 回收后非托管层调用时出现 Access Violation。
        /// </summary>
        private class CallbackHolders
        {
            public UIExtActionCallback OnClick;
            public UIExtActionCallback OnRightClick;
            public UIExtToggleCallback OnToggle;
            public UIExtActionCallback OnDialogClose;
        }

        // 静态字典持有全部回调委托，防止委托被 GC 回收（CLAUDE.md 强制约定）
        private static readonly Dictionary<IntPtr, CallbackHolders> _callbacks =
            new Dictionary<IntPtr, CallbackHolders>();

        private static CallbackHolders GetHolders(IntPtr control, bool create)
        {
            if (!_callbacks.TryGetValue(control, out var holders))
            {
                if (!create)
                    return null;

                holders = new CallbackHolders();
                _callbacks.Add(control, holders);
            }

            return holders;
        }

        // ============ 屏幕 / 根管理 ============

        // 对应 Phobos: HRESULT UIExt_Open(void* pRoot, int modal)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_Open(IntPtr pRoot, int modal);

        /// <summary>
        /// 打开一个已创建的根控件，框架接管其所有权。
        /// 对应 Phobos Interop API：<c>UIExt_Open(void* pRoot, int modal)</c>。
        /// </summary>
        /// <param name="root">根控件句柄（Panel / Dialog / IconStrip 等），必须尚未挂载。</param>
        /// <param name="modal">模态级别，控制打开后阻断哪些输入。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示 modal 越界或句柄已被挂载/打开过。
        /// </returns>
        public static int Open(IntPtr root, UIExtModal modal)
        {
            if (root == IntPtr.Zero)
                return HResult.E_POINTER;

            return UIExt_Open(root, (int)modal);
        }

        // 对应 Phobos: HRESULT UIExt_Close(void* pControl)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_Close(IntPtr pControl);

        /// <summary>
        /// 关闭一个已打开的根控件（延迟到下一帧销毁），或销毁一个尚未挂载的未挂载控件。
        /// 对应 Phobos Interop API：<c>UIExt_Close(void* pControl)</c>。
        /// </summary>
        /// <param name="control">控件句柄。</param>
        /// <returns><see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空。</returns>
        /// <remarks>同时清理 C# 侧为该控件持有的回调委托引用。</remarks>
        public static int Close(IntPtr control)
        {
            if (control == IntPtr.Zero)
                return HResult.E_POINTER;

            _callbacks.Remove(control);
            return UIExt_Close(control);
        }

        // 对应 Phobos: HRESULT UIExt_CloseAll()
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_CloseAll();

        /// <summary>
        /// 关闭并销毁所有已打开的屏幕和所有未挂载控件。
        /// 对应 Phobos Interop API：<c>UIExt_CloseAll()</c>。
        /// </summary>
        /// <returns><see cref="HResult.S_OK"/>。</returns>
        /// <remarks>同时清理 C# 侧持有的全部回调委托引用。</remarks>
        public static int CloseAll()
        {
            _callbacks.Clear();
            return UIExt_CloseAll();
        }

        // ============ 控件创建 ============

        // 对应 Phobos: HRESULT UIExt_CreatePanel(int x, int y, int width, int height, void** ppControl)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_CreatePanel(int x, int y, int width, int height, out IntPtr ppControl);

        /// <summary>
        /// 创建一个 Panel（容器面板）。
        /// 对应 Phobos Interop API：<c>UIExt_CreatePanel</c>。
        /// </summary>
        /// <param name="x">左上角 X 坐标。</param>
        /// <param name="y">左上角 Y 坐标。</param>
        /// <param name="width">宽度。</param>
        /// <param name="height">高度。</param>
        /// <param name="control">输出：控件句柄。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；
        /// <see cref="HResult.E_OUTOFMEMORY"/> 表示创建失败。
        /// </returns>
        public static int CreatePanel(int x, int y, int width, int height, out IntPtr control)
        {
            return UIExt_CreatePanel(x, y, width, height, out control);
        }

        // 对应 Phobos: HRESULT UIExt_CreateDialog(int x, int y, int width, int height, const wchar_t* title, void** ppControl)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_CreateDialog(
            int x, int y, int width, int height,
            [MarshalAs(UnmanagedType.LPWStr)] string title,
            out IntPtr ppControl);

        /// <summary>
        /// 创建一个 Dialog（带标题栏和自带关闭按钮的对话框）。
        /// 对应 Phobos Interop API：<c>UIExt_CreateDialog</c>。
        /// </summary>
        /// <param name="x">左上角 X 坐标。</param>
        /// <param name="y">左上角 Y 坐标。</param>
        /// <param name="width">宽度。</param>
        /// <param name="height">高度。</param>
        /// <param name="title">标题文本，可为 null。</param>
        /// <param name="control">输出：控件句柄。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；
        /// <see cref="HResult.E_OUTOFMEMORY"/> 表示创建失败。
        /// </returns>
        public static int CreateDialog(int x, int y, int width, int height, string title, out IntPtr control)
        {
            return UIExt_CreateDialog(x, y, width, height, title, out control);
        }

        // 对应 Phobos: HRESULT UIExt_CreateButton(int x, int y, int width, int height, const wchar_t* text, void** ppControl)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_CreateButton(
            int x, int y, int width, int height,
            [MarshalAs(UnmanagedType.LPWStr)] string text,
            out IntPtr ppControl);

        /// <summary>
        /// 创建一个 Button（文本按钮）。
        /// 对应 Phobos Interop API：<c>UIExt_CreateButton</c>。
        /// </summary>
        /// <param name="x">左上角 X 坐标。</param>
        /// <param name="y">左上角 Y 坐标。</param>
        /// <param name="width">宽度。</param>
        /// <param name="height">高度。</param>
        /// <param name="text">按钮文本，可为 null。</param>
        /// <param name="control">输出：控件句柄。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；
        /// <see cref="HResult.E_OUTOFMEMORY"/> 表示创建失败。
        /// </returns>
        public static int CreateButton(int x, int y, int width, int height, string text, out IntPtr control)
        {
            return UIExt_CreateButton(x, y, width, height, text, out control);
        }

        // 对应 Phobos: HRESULT UIExt_CreateIconButton(int x, int y, int width, int height, void** ppControl)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_CreateIconButton(
            int x, int y, int width, int height, out IntPtr ppControl);

        /// <summary>
        /// 创建一个 IconButton（图标按钮）。
        /// 对应 Phobos Interop API：<c>UIExt_CreateIconButton</c>。
        /// </summary>
        /// <param name="x">左上角 X 坐标。</param>
        /// <param name="y">左上角 Y 坐标。</param>
        /// <param name="width">宽度。</param>
        /// <param name="height">高度。</param>
        /// <param name="control">输出：控件句柄。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；
        /// <see cref="HResult.E_OUTOFMEMORY"/> 表示创建失败。
        /// </returns>
        /// <remarks>图标创建后通过 <see cref="Button_SetIconFromFile"/> 设置 PCX 素材。</remarks>
        public static int CreateIconButton(int x, int y, int width, int height, out IntPtr control)
        {
            return UIExt_CreateIconButton(x, y, width, height, out control);
        }

        // 对应 Phobos: HRESULT UIExt_CreateCheckBox(int x, int y, int width, int height, const wchar_t* text, void** ppControl)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_CreateCheckBox(
            int x, int y, int width, int height,
            [MarshalAs(UnmanagedType.LPWStr)] string text,
            out IntPtr ppControl);

        /// <summary>
        /// 创建一个 CheckBox（复选框）。
        /// 对应 Phobos Interop API：<c>UIExt_CreateCheckBox</c>。
        /// </summary>
        /// <param name="x">左上角 X 坐标。</param>
        /// <param name="y">左上角 Y 坐标。</param>
        /// <param name="width">宽度。</param>
        /// <param name="height">高度。</param>
        /// <param name="text">复选框文本，可为 null。</param>
        /// <param name="control">输出：控件句柄。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；
        /// <see cref="HResult.E_OUTOFMEMORY"/> 表示创建失败。
        /// </returns>
        public static int CreateCheckBox(int x, int y, int width, int height, string text, out IntPtr control)
        {
            return UIExt_CreateCheckBox(x, y, width, height, text, out control);
        }

        // 对应 Phobos: HRESULT UIExt_CreateLabel(int x, int y, const wchar_t* text, void** ppControl)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_CreateLabel(
            int x, int y,
            [MarshalAs(UnmanagedType.LPWStr)] string text,
            out IntPtr ppControl);

        /// <summary>
        /// 创建一个 Label（静态文本标签，尺寸由文本自动决定）。
        /// 对应 Phobos Interop API：<c>UIExt_CreateLabel</c>。
        /// </summary>
        /// <param name="x">左上角 X 坐标。</param>
        /// <param name="y">左上角 Y 坐标。</param>
        /// <param name="text">文本内容，可为 null。</param>
        /// <param name="control">输出：控件句柄。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；
        /// <see cref="HResult.E_OUTOFMEMORY"/> 表示创建失败。
        /// </returns>
        public static int CreateLabel(int x, int y, string text, out IntPtr control)
        {
            return UIExt_CreateLabel(x, y, text, out control);
        }

        // 对应 Phobos: HRESULT UIExt_CreateIconStrip(int x, int y, int itemWidth, int itemHeight, int spacing, void** ppControl)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_CreateIconStrip(
            int x, int y, int itemWidth, int itemHeight, int spacing, out IntPtr ppControl);

        /// <summary>
        /// 创建一个 IconStrip（水平图标条，子控件自动横向排列）。
        /// 对应 Phobos Interop API：<c>UIExt_CreateIconStrip</c>。
        /// </summary>
        /// <param name="x">左上角 X 坐标。</param>
        /// <param name="y">左上角 Y 坐标。</param>
        /// <param name="itemWidth">单个图标宽度。</param>
        /// <param name="itemHeight">单个图标高度。</param>
        /// <param name="spacing">图标间距。</param>
        /// <param name="control">输出：控件句柄。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；
        /// <see cref="HResult.E_OUTOFMEMORY"/> 表示创建失败。
        /// </returns>
        public static int CreateIconStrip(int x, int y, int itemWidth, int itemHeight, int spacing, out IntPtr control)
        {
            return UIExt_CreateIconStrip(x, y, itemWidth, itemHeight, spacing, out control);
        }

        // 对应 Phobos: HRESULT UIExt_CreateListGrid(int x, int y, int width, int height, void** ppControl)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_CreateListGrid(int x, int y, int width, int height, out IntPtr ppControl);

        /// <summary>
        /// 创建一个 ListGrid（网格列表容器，子控件按列自动布局）。
        /// 对应 Phobos Interop API：<c>UIExt_CreateListGrid</c>。
        /// </summary>
        /// <param name="x">左上角 X 坐标。</param>
        /// <param name="y">左上角 Y 坐标。</param>
        /// <param name="width">宽度。</param>
        /// <param name="height">高度。</param>
        /// <param name="control">输出：控件句柄。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；
        /// <see cref="HResult.E_OUTOFMEMORY"/> 表示创建失败。
        /// </returns>
        public static int CreateListGrid(int x, int y, int width, int height, out IntPtr control)
        {
            return UIExt_CreateListGrid(x, y, width, height, out control);
        }

        // 对应 Phobos: HRESULT UIExt_CreatePageView(int x, int y, int width, int height, void** ppControl)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_CreatePageView(int x, int y, int width, int height, out IntPtr ppControl);

        /// <summary>
        /// 创建一个 PageView（分页视图容器，配合 <see cref="PageView_SetGrid"/> 实现翻页网格）。
        /// 对应 Phobos Interop API：<c>UIExt_CreatePageView</c>。
        /// </summary>
        /// <param name="x">左上角 X 坐标。</param>
        /// <param name="y">左上角 Y 坐标。</param>
        /// <param name="width">宽度。</param>
        /// <param name="height">高度。</param>
        /// <param name="control">输出：控件句柄。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；
        /// <see cref="HResult.E_OUTOFMEMORY"/> 表示创建失败。
        /// </returns>
        public static int CreatePageView(int x, int y, int width, int height, out IntPtr control)
        {
            return UIExt_CreatePageView(x, y, width, height, out control);
        }

        // ============ 控件树管理 ============

        // 对应 Phobos: HRESULT UIExt_AddChild(void* pParent, void* pChild)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_AddChild(IntPtr pParent, IntPtr pChild);

        /// <summary>
        /// 把子控件挂到父控件下。挂载后子控件所有权转移给父控件。
        /// 对应 Phobos Interop API：<c>UIExt_AddChild</c>。
        /// </summary>
        /// <param name="parent">父控件句柄。</param>
        /// <param name="child">子控件句柄，必须尚未挂载。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示 child 不是未挂载控件。
        /// </returns>
        /// <remarks>若父控件已处于打开状态，子控件会自动注册到游戏 Gadget 系统。</remarks>
        public static int AddChild(IntPtr parent, IntPtr child)
        {
            if (parent == IntPtr.Zero || child == IntPtr.Zero)
                return HResult.E_POINTER;

            return UIExt_AddChild(parent, child);
        }

        // 对应 Phobos: HRESULT UIExt_RemoveChild(void* pParent, void* pChild)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_RemoveChild(IntPtr pParent, IntPtr pChild);

        /// <summary>
        /// 从父控件移除子控件并销毁之。
        /// 对应 Phobos Interop API：<c>UIExt_RemoveChild</c>。
        /// </summary>
        /// <param name="parent">父控件句柄。</param>
        /// <param name="child">要移除并销毁的子控件句柄。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空。
        /// </returns>
        public static int RemoveChild(IntPtr parent, IntPtr child)
        {
            if (parent == IntPtr.Zero || child == IntPtr.Zero)
                return HResult.E_POINTER;

            _callbacks.Remove(child);
            return UIExt_RemoveChild(parent, child);
        }

        // ============ 通用属性 ============

        // 对应 Phobos: HRESULT UIExt_SetPos(void* pControl, int x, int y)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_SetPos(IntPtr pControl, int x, int y);

        /// <summary>
        /// 设置控件位置。
        /// 对应 Phobos Interop API：<c>UIExt_SetPos</c>。
        /// </summary>
        /// <param name="control">控件句柄。</param>
        /// <param name="x">X 坐标。</param>
        /// <param name="y">Y 坐标。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空。
        /// </returns>
        public static int SetPos(IntPtr control, int x, int y)
        {
            if (control == IntPtr.Zero)
                return HResult.E_POINTER;

            return UIExt_SetPos(control, x, y);
        }

        // 对应 Phobos: HRESULT UIExt_SetSize(void* pControl, int width, int height)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_SetSize(IntPtr pControl, int width, int height);

        /// <summary>
        /// 设置控件尺寸。
        /// 对应 Phobos Interop API：<c>UIExt_SetSize</c>。
        /// </summary>
        /// <param name="control">控件句柄。</param>
        /// <param name="width">宽度。</param>
        /// <param name="height">高度。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空。
        /// </returns>
        public static int SetSize(IntPtr control, int width, int height)
        {
            if (control == IntPtr.Zero)
                return HResult.E_POINTER;

            return UIExt_SetSize(control, width, height);
        }

        // 对应 Phobos: HRESULT UIExt_SetVisible(void* pControl, int visible)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_SetVisible(IntPtr pControl, int visible);

        /// <summary>
        /// 设置控件可见性。
        /// 对应 Phobos Interop API：<c>UIExt_SetVisible</c>。
        /// </summary>
        /// <param name="control">控件句柄。</param>
        /// <param name="visible">是否可见。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空。
        /// </returns>
        public static int SetVisible(IntPtr control, bool visible)
        {
            if (control == IntPtr.Zero)
                return HResult.E_POINTER;

            return UIExt_SetVisible(control, visible ? 1 : 0);
        }

        // 对应 Phobos: HRESULT UIExt_SetEnabled(void* pControl, int enabled)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_SetEnabled(IntPtr pControl, int enabled);

        /// <summary>
        /// 设置控件可用性（禁用后不响应输入）。
        /// 对应 Phobos Interop API：<c>UIExt_SetEnabled</c>。
        /// </summary>
        /// <param name="control">控件句柄。</param>
        /// <param name="enabled">是否可用。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空。
        /// </returns>
        public static int SetEnabled(IntPtr control, bool enabled)
        {
            if (control == IntPtr.Zero)
                return HResult.E_POINTER;

            return UIExt_SetEnabled(control, enabled ? 1 : 0);
        }

        // 对应 Phobos: HRESULT UIExt_SetText(void* pControl, const wchar_t* text)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_SetText(
            IntPtr pControl, [MarshalAs(UnmanagedType.LPWStr)] string text);

        /// <summary>
        /// 设置控件文本（Button / Label / CheckBox 文本，或 Dialog 标题）。
        /// 对应 Phobos Interop API：<c>UIExt_SetText</c>。
        /// </summary>
        /// <param name="control">控件句柄。</param>
        /// <param name="text">文本内容，可为 null（视为空串）。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示句柄不是文本类控件。
        /// </returns>
        public static int SetText(IntPtr control, string text)
        {
            if (control == IntPtr.Zero)
                return HResult.E_POINTER;

            return UIExt_SetText(control, text);
        }

        // 对应 Phobos: HRESULT UIExt_SetChecked(void* pControl, int checked)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_SetChecked(IntPtr pControl, int @checked);

        /// <summary>
        /// 设置 CheckBox 勾选状态。
        /// 对应 Phobos Interop API：<c>UIExt_SetChecked</c>。
        /// </summary>
        /// <param name="control">CheckBox 句柄。</param>
        /// <param name="checked">是否勾选。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示句柄不是 CheckBox。
        /// </returns>
        public static int SetChecked(IntPtr control, bool @checked)
        {
            if (control == IntPtr.Zero)
                return HResult.E_POINTER;

            return UIExt_SetChecked(control, @checked ? 1 : 0);
        }

        // 对应 Phobos: HRESULT UIExt_SetAnchor(void* pControl, int anchor, int offsetX, int offsetY)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_SetAnchor(IntPtr pControl, int anchor, int offsetX, int offsetY);

        /// <summary>
        /// 设置控件锚点（相对屏幕边缘定位，常用于分辨率自适应）。
        /// 对应 Phobos Interop API：<c>UIExt_SetAnchor</c>。
        /// </summary>
        /// <param name="control">控件句柄。</param>
        /// <param name="anchor">锚点类型。</param>
        /// <param name="offsetX">相对锚点的 X 偏移。</param>
        /// <param name="offsetY">相对锚点的 Y 偏移。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示 anchor 越界。
        /// </returns>
        public static int SetAnchor(IntPtr control, UIExtAnchor anchor, int offsetX, int offsetY)
        {
            if (control == IntPtr.Zero)
                return HResult.E_POINTER;

            return UIExt_SetAnchor(control, (int)anchor, offsetX, offsetY);
        }

        // 对应 Phobos: HRESULT UIExt_SetTooltip(void* pControl, const wchar_t* title, const wchar_t* text)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_SetTooltip(
            IntPtr pControl,
            [MarshalAs(UnmanagedType.LPWStr)] string title,
            [MarshalAs(UnmanagedType.LPWStr)] string text);

        /// <summary>
        /// 设置控件悬停 Tooltip（标题 + 正文）。
        /// 对应 Phobos Interop API：<c>UIExt_SetTooltip</c>。
        /// </summary>
        /// <param name="control">控件句柄。</param>
        /// <param name="title">Tooltip 标题，可为 null。</param>
        /// <param name="text">Tooltip 正文，可为 null。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空。
        /// </returns>
        public static int SetTooltip(IntPtr control, string title, string text)
        {
            if (control == IntPtr.Zero)
                return HResult.E_POINTER;

            return UIExt_SetTooltip(control, title, text);
        }

        // 对应 Phobos: HRESULT UIExt_SetBackColor(void* pControl, int r, int g, int b, int opacity)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_SetBackColor(IntPtr pControl, int r, int g, int b, int opacity);

        /// <summary>
        /// 设置 Panel / Dialog 背景色和透明度。
        /// 对应 Phobos Interop API：<c>UIExt_SetBackColor</c>。
        /// </summary>
        /// <param name="control">Panel / Dialog 句柄。</param>
        /// <param name="r">红色分量（0-255）。</param>
        /// <param name="g">绿色分量（0-255）。</param>
        /// <param name="b">蓝色分量（0-255）。</param>
        /// <param name="opacity">不透明度（0-255）。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示句柄不是 Panel / Dialog。
        /// </returns>
        public static int SetBackColor(IntPtr control, int r, int g, int b, int opacity)
        {
            if (control == IntPtr.Zero)
                return HResult.E_POINTER;

            return UIExt_SetBackColor(control, r, g, b, opacity);
        }

        // 对应 Phobos: HRESULT UIExt_SetBorder(void* pControl, int enabled, int color)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_SetBorder(IntPtr pControl, int enabled, int color);

        /// <summary>
        /// 设置 Panel / Dialog 边框。
        /// 对应 Phobos Interop API：<c>UIExt_SetBorder</c>。
        /// </summary>
        /// <param name="control">Panel / Dialog 句柄。</param>
        /// <param name="enabled">是否显示边框。</param>
        /// <param name="color">边框颜色（COLORREF，0x00BBGGRR）。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示句柄不是 Panel / Dialog。
        /// </returns>
        public static int SetBorder(IntPtr control, bool enabled, int color)
        {
            if (control == IntPtr.Zero)
                return HResult.E_POINTER;

            return UIExt_SetBorder(control, enabled ? 1 : 0, color);
        }

        // ============ Panel / Dialog SHP 背景 ============

        // 对应 Phobos: HRESULT UIExt_Panel_SetShpBackground(void* pPanel, const char* shpFile, const char* paletteFile,
        //     int frame, int offsetX, int offsetY, int align)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_Panel_SetShpBackground(
            IntPtr pPanel,
            [MarshalAs(UnmanagedType.LPStr)] string shpFile,
            [MarshalAs(UnmanagedType.LPStr)] string paletteFile,
            int frame, int offsetX, int offsetY, int align);

        /// <summary>
        /// 给 Panel / Dialog 设置一个 SHP 背景装饰素材。
        /// 对应 Phobos Interop API：<c>UIExt_Panel_SetShpBackground</c>。
        /// </summary>
        /// <param name="panel">Panel / Dialog 句柄。</param>
        /// <param name="shpFile">游戏目录下的 SHP 文件名（ANSI，如 "SIDEBAR.SHP"）。</param>
        /// <param name="paletteFile">PAL 文件名；传 null 或空串时使用默认 ANIM_PAL。</param>
        /// <param name="frame">要绘制的帧号，越界时自动归 0。</param>
        /// <param name="offsetX">在对齐基础上追加的 X 偏移。</param>
        /// <param name="offsetY">在对齐基础上追加的 Y 偏移。</param>
        /// <param name="align">素材对齐方式。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.S_FALSE"/> 表示素材加载失败；
        /// <see cref="HResult.E_POINTER"/> 表示句柄或 shpFile 为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示句柄不是 Panel / Dialog，或 align 越界。
        /// </returns>
        public static int Panel_SetShpBackground(
            IntPtr panel, string shpFile, string paletteFile, int frame, int offsetX, int offsetY, UIExtShpAlign align)
        {
            if (panel == IntPtr.Zero)
                return HResult.E_POINTER;

            return UIExt_Panel_SetShpBackground(panel, shpFile, paletteFile, frame, offsetX, offsetY, (int)align);
        }

        // ============ Button ============

        // 对应 Phobos: HRESULT UIExt_Button_SetOnClick(void* pButton, UIExtActionCallback callback, void* userData)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_Button_SetOnClick(
            IntPtr pButton, UIExtActionCallback callback, IntPtr userData);

        /// <summary>
        /// 设置按钮左键点击回调。传入 null 表示清除回调。
        /// 对应 Phobos Interop API：<c>UIExt_Button_SetOnClick</c>。
        /// </summary>
        /// <param name="button">Button 句柄。</param>
        /// <param name="callback">点击时执行的回调；状态通过闭包捕获。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示句柄不是 Button。
        /// </returns>
        /// <remarks>委托由本类静态持有，调用方无需自行保存引用；控件被关闭时自动清理。</remarks>
        public static int Button_SetOnClick(IntPtr button, Action callback)
        {
            if (button == IntPtr.Zero)
                return HResult.E_POINTER;

            if (callback == null)
            {
                var holders = GetHolders(button, false);
                if (holders != null)
                    holders.OnClick = null;
                return UIExt_Button_SetOnClick(button, null, IntPtr.Zero);
            }

            var wrapper = new UIExtActionCallback(_ => callback());
            GetHolders(button, true).OnClick = wrapper;
            return UIExt_Button_SetOnClick(button, wrapper, IntPtr.Zero);
        }

        // 对应 Phobos: HRESULT UIExt_Button_SetOnRightClick(void* pButton, UIExtActionCallback callback, void* userData)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_Button_SetOnRightClick(
            IntPtr pButton, UIExtActionCallback callback, IntPtr userData);

        /// <summary>
        /// 设置按钮右键点击回调。传入 null 表示清除回调。
        /// 对应 Phobos Interop API：<c>UIExt_Button_SetOnRightClick</c>。
        /// </summary>
        /// <param name="button">Button 句柄。</param>
        /// <param name="callback">右键点击时执行的回调。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示句柄不是 Button。
        /// </returns>
        /// <remarks>委托由本类静态持有，调用方无需自行保存引用；控件被关闭时自动清理。</remarks>
        public static int Button_SetOnRightClick(IntPtr button, Action callback)
        {
            if (button == IntPtr.Zero)
                return HResult.E_POINTER;

            if (callback == null)
            {
                var holders = GetHolders(button, false);
                if (holders != null)
                    holders.OnRightClick = null;
                return UIExt_Button_SetOnRightClick(button, null, IntPtr.Zero);
            }

            var wrapper = new UIExtActionCallback(_ => callback());
            GetHolders(button, true).OnRightClick = wrapper;
            return UIExt_Button_SetOnRightClick(button, wrapper, IntPtr.Zero);
        }

        // 对应 Phobos: HRESULT UIExt_Button_SetShortcut(void* pButton, int key)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_Button_SetShortcut(IntPtr pButton, int key);

        /// <summary>
        /// 设置按钮快捷键。
        /// 对应 Phobos Interop API：<c>UIExt_Button_SetShortcut</c>。
        /// </summary>
        /// <param name="button">Button 句柄。</param>
        /// <param name="key">快捷键虚拟键码（VK_*）。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示句柄不是 Button。
        /// </returns>
        public static int Button_SetShortcut(IntPtr button, int key)
        {
            if (button == IntPtr.Zero)
                return HResult.E_POINTER;

            return UIExt_Button_SetShortcut(button, key);
        }

        // 对应 Phobos: HRESULT UIExt_Button_Click(void* pButton)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_Button_Click(IntPtr pButton);

        /// <summary>
        /// 程序化触发按钮点击（等效于用户点击，会执行已注册的 OnClick 回调）。
        /// 对应 Phobos Interop API：<c>UIExt_Button_Click</c>。
        /// </summary>
        /// <param name="button">Button 句柄。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示句柄不是 Button。
        /// </returns>
        public static int Button_Click(IntPtr button)
        {
            if (button == IntPtr.Zero)
                return HResult.E_POINTER;

            return UIExt_Button_Click(button);
        }

        // 对应 Phobos: HRESULT UIExt_Button_SetIconFromFile(void* pButton, const char* filename)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_Button_SetIconFromFile(
            IntPtr pButton, [MarshalAs(UnmanagedType.LPStr)] string filename);

        /// <summary>
        /// 按文件名直接把 PCX 素材设置为按钮图标（由 Phobos 侧 PCX::Instance 加载并缓存）。
        /// 对应 Phobos Interop API：<c>UIExt_Button_SetIconFromFile</c>。
        /// </summary>
        /// <param name="button">Button / IconButton 句柄。</param>
        /// <param name="filename">PCX 文件名（ANSI，如 "Blizzard.pcx"）。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.S_FALSE"/> 表示素材加载失败；
        /// <see cref="HResult.E_POINTER"/> 表示句柄为空；<see cref="HResult.E_INVALIDARG"/> 表示句柄不是 Button。
        /// </returns>
        public static int Button_SetIconFromFile(IntPtr button, string filename)
        {
            if (button == IntPtr.Zero)
                return HResult.E_POINTER;

            return UIExt_Button_SetIconFromFile(button, filename);
        }

        // ============ CheckBox ============

        // 对应 Phobos: HRESULT UIExt_CheckBox_SetOnToggle(void* pCheckBox, UIExtToggleCallback callback, void* userData)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_CheckBox_SetOnToggle(
            IntPtr pCheckBox, UIExtToggleCallback callback, IntPtr userData);

        /// <summary>
        /// 设置复选框勾选状态变化回调。传入 null 表示清除回调。
        /// 对应 Phobos Interop API：<c>UIExt_CheckBox_SetOnToggle</c>。
        /// </summary>
        /// <param name="checkBox">CheckBox 句柄。</param>
        /// <param name="callback">勾选状态变化时执行的回调，参数为新状态。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示句柄不是 CheckBox。
        /// </returns>
        /// <remarks>委托由本类静态持有，调用方无需自行保存引用；控件被关闭时自动清理。</remarks>
        public static int CheckBox_SetOnToggle(IntPtr checkBox, Action<bool> callback)
        {
            if (checkBox == IntPtr.Zero)
                return HResult.E_POINTER;

            if (callback == null)
            {
                var holders = GetHolders(checkBox, false);
                if (holders != null)
                    holders.OnToggle = null;
                return UIExt_CheckBox_SetOnToggle(checkBox, null, IntPtr.Zero);
            }

            var wrapper = new UIExtToggleCallback((@checked, _) => callback(@checked != 0));
            GetHolders(checkBox, true).OnToggle = wrapper;
            return UIExt_CheckBox_SetOnToggle(checkBox, wrapper, IntPtr.Zero);
        }

        // ============ Dialog ============

        // 对应 Phobos: HRESULT UIExt_Dialog_SetCloseAction(void* pDialog, UIExtActionCallback callback, void* userData)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_Dialog_SetCloseAction(
            IntPtr pDialog, UIExtActionCallback callback, IntPtr userData);

        /// <summary>
        /// 绑定 Dialog 自带关闭按钮的回调。传入 null 表示清除回调。
        /// 对应 Phobos Interop API：<c>UIExt_Dialog_SetCloseAction</c>。
        /// </summary>
        /// <param name="dialog">Dialog 句柄。</param>
        /// <param name="callback">点击自带关闭按钮时执行的回调。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示句柄不是 Dialog。
        /// </returns>
        /// <remarks>委托由本类静态持有，调用方无需自行保存引用；控件被关闭时自动清理。</remarks>
        public static int Dialog_SetCloseAction(IntPtr dialog, Action callback)
        {
            if (dialog == IntPtr.Zero)
                return HResult.E_POINTER;

            if (callback == null)
            {
                var holders = GetHolders(dialog, false);
                if (holders != null)
                    holders.OnDialogClose = null;
                return UIExt_Dialog_SetCloseAction(dialog, null, IntPtr.Zero);
            }

            var wrapper = new UIExtActionCallback(_ => callback());
            GetHolders(dialog, true).OnDialogClose = wrapper;
            return UIExt_Dialog_SetCloseAction(dialog, wrapper, IntPtr.Zero);
        }

        // ============ PageView ============

        // 对应 Phobos: HRESULT UIExt_PageView_SetGrid(void* pPageView, int columns, int rows, int itemWidth, int itemHeight, int gapX, int gapY)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_PageView_SetGrid(
            IntPtr pPageView, int columns, int rows, int itemWidth, int itemHeight, int gapX, int gapY);

        /// <summary>
        /// 设置 PageView 的分页网格布局参数。
        /// 对应 Phobos Interop API：<c>UIExt_PageView_SetGrid</c>。
        /// </summary>
        /// <param name="pageView">PageView 句柄。</param>
        /// <param name="columns">每页列数。</param>
        /// <param name="rows">每页行数。</param>
        /// <param name="itemWidth">格子宽度。</param>
        /// <param name="itemHeight">格子高度。</param>
        /// <param name="gapX">水平间隙。</param>
        /// <param name="gapY">垂直间隙。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示句柄不是 PageView。
        /// </returns>
        public static int PageView_SetGrid(
            IntPtr pageView, int columns, int rows, int itemWidth, int itemHeight, int gapX, int gapY)
        {
            if (pageView == IntPtr.Zero)
                return HResult.E_POINTER;

            return UIExt_PageView_SetGrid(pageView, columns, rows, itemWidth, itemHeight, gapX, gapY);
        }

        // 对应 Phobos: HRESULT UIExt_PageView_SetPage(void* pPageView, int page)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_PageView_SetPage(IntPtr pPageView, int page);

        /// <summary>
        /// 跳转到指定页（从 0 开始）。
        /// 对应 Phobos Interop API：<c>UIExt_PageView_SetPage</c>。
        /// </summary>
        /// <param name="pageView">PageView 句柄。</param>
        /// <param name="page">目标页索引。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示句柄不是 PageView。
        /// </returns>
        public static int PageView_SetPage(IntPtr pageView, int page)
        {
            if (pageView == IntPtr.Zero)
                return HResult.E_POINTER;

            return UIExt_PageView_SetPage(pageView, page);
        }

        // 对应 Phobos: HRESULT UIExt_PageView_NextPage(void* pPageView)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_PageView_NextPage(IntPtr pPageView);

        /// <summary>
        /// 跳转到下一页。
        /// 对应 Phobos Interop API：<c>UIExt_PageView_NextPage</c>。
        /// </summary>
        /// <param name="pageView">PageView 句柄。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示句柄不是 PageView。
        /// </returns>
        public static int PageView_NextPage(IntPtr pageView)
        {
            if (pageView == IntPtr.Zero)
                return HResult.E_POINTER;

            return UIExt_PageView_NextPage(pageView);
        }

        // 对应 Phobos: HRESULT UIExt_PageView_PrevPage(void* pPageView)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_PageView_PrevPage(IntPtr pPageView);

        /// <summary>
        /// 跳转到上一页。
        /// 对应 Phobos Interop API：<c>UIExt_PageView_PrevPage</c>。
        /// </summary>
        /// <param name="pageView">PageView 句柄。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示句柄不是 PageView。
        /// </returns>
        public static int PageView_PrevPage(IntPtr pageView)
        {
            if (pageView == IntPtr.Zero)
                return HResult.E_POINTER;

            return UIExt_PageView_PrevPage(pageView);
        }

        // 对应 Phobos: HRESULT UIExt_PageView_GetPageCount(void* pPageView, int* pCount)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_PageView_GetPageCount(IntPtr pPageView, out int pCount);

        /// <summary>
        /// 获取总页数。
        /// 对应 Phobos Interop API：<c>UIExt_PageView_GetPageCount</c>。
        /// </summary>
        /// <param name="pageView">PageView 句柄。</param>
        /// <param name="count">输出：总页数。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示句柄不是 PageView。
        /// </returns>
        public static int PageView_GetPageCount(IntPtr pageView, out int count)
        {
            if (pageView == IntPtr.Zero)
            {
                count = 0;
                return HResult.E_POINTER;
            }

            return UIExt_PageView_GetPageCount(pageView, out count);
        }

        // 对应 Phobos: HRESULT UIExt_PageView_GetPageIndex(void* pPageView, int* pIndex)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_PageView_GetPageIndex(IntPtr pPageView, out int pIndex);

        /// <summary>
        /// 获取当前页索引（从 0 开始）。
        /// 对应 Phobos Interop API：<c>UIExt_PageView_GetPageIndex</c>。
        /// </summary>
        /// <param name="pageView">PageView 句柄。</param>
        /// <param name="index">输出：当前页索引。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示句柄不是 PageView。
        /// </returns>
        public static int PageView_GetPageIndex(IntPtr pageView, out int index)
        {
            if (pageView == IntPtr.Zero)
            {
                index = 0;
                return HResult.E_POINTER;
            }

            return UIExt_PageView_GetPageIndex(pageView, out index);
        }

        // ============ ListGrid ============

        // 对应 Phobos: HRESULT UIExt_ListGrid_SetColumns(void* pListGrid, int columns, int itemWidth, int itemHeight, int gapX, int gapY)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_ListGrid_SetColumns(
            IntPtr pListGrid, int columns, int itemWidth, int itemHeight, int gapX, int gapY);

        /// <summary>
        /// 设置 ListGrid 的列数和格子尺寸。
        /// 对应 Phobos Interop API：<c>UIExt_ListGrid_SetColumns</c>。
        /// </summary>
        /// <param name="listGrid">ListGrid 句柄。</param>
        /// <param name="columns">列数。</param>
        /// <param name="itemWidth">格子宽度。</param>
        /// <param name="itemHeight">格子高度。</param>
        /// <param name="gapX">水平间隙。</param>
        /// <param name="gapY">垂直间隙。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示句柄不是 ListGrid。
        /// </returns>
        public static int ListGrid_SetColumns(
            IntPtr listGrid, int columns, int itemWidth, int itemHeight, int gapX, int gapY)
        {
            if (listGrid == IntPtr.Zero)
                return HResult.E_POINTER;

            return UIExt_ListGrid_SetColumns(listGrid, columns, itemWidth, itemHeight, gapX, gapY);
        }

        // 对应 Phobos: HRESULT UIExt_ListGrid_Refresh(void* pListGrid)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_ListGrid_Refresh(IntPtr pListGrid);

        /// <summary>
        /// 触发 ListGrid 重新布局子控件。
        /// 对应 Phobos Interop API：<c>UIExt_ListGrid_Refresh</c>。
        /// </summary>
        /// <param name="listGrid">ListGrid 句柄。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示句柄不是 ListGrid。
        /// </returns>
        public static int ListGrid_Refresh(IntPtr listGrid)
        {
            if (listGrid == IntPtr.Zero)
                return HResult.E_POINTER;

            return UIExt_ListGrid_Refresh(listGrid);
        }

        // ============ IconStrip ============

        // 对应 Phobos: HRESULT UIExt_IconStrip_SetItemSize(void* pIconStrip, int width, int height)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_IconStrip_SetItemSize(IntPtr pIconStrip, int width, int height);

        /// <summary>
        /// 设置 IconStrip 的图标尺寸。
        /// 对应 Phobos Interop API：<c>UIExt_IconStrip_SetItemSize</c>。
        /// </summary>
        /// <param name="iconStrip">IconStrip 句柄。</param>
        /// <param name="width">图标宽度。</param>
        /// <param name="height">图标高度。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示句柄不是 IconStrip。
        /// </returns>
        public static int IconStrip_SetItemSize(IntPtr iconStrip, int width, int height)
        {
            if (iconStrip == IntPtr.Zero)
                return HResult.E_POINTER;

            return UIExt_IconStrip_SetItemSize(iconStrip, width, height);
        }

        // 对应 Phobos: HRESULT UIExt_IconStrip_SetSpacing(void* pIconStrip, int spacing)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_IconStrip_SetSpacing(IntPtr pIconStrip, int spacing);

        /// <summary>
        /// 设置 IconStrip 的图标间距。
        /// 对应 Phobos Interop API：<c>UIExt_IconStrip_SetSpacing</c>。
        /// </summary>
        /// <param name="iconStrip">IconStrip 句柄。</param>
        /// <param name="spacing">图标间距。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示句柄不是 IconStrip。
        /// </returns>
        public static int IconStrip_SetSpacing(IntPtr iconStrip, int spacing)
        {
            if (iconStrip == IntPtr.Zero)
                return HResult.E_POINTER;

            return UIExt_IconStrip_SetSpacing(iconStrip, spacing);
        }

        // 对应 Phobos: HRESULT UIExt_IconStrip_Refresh(void* pIconStrip)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_IconStrip_Refresh(IntPtr pIconStrip);

        /// <summary>
        /// 触发 IconStrip 重新布局并调整高度。
        /// 对应 Phobos Interop API：<c>UIExt_IconStrip_Refresh</c>。
        /// </summary>
        /// <param name="iconStrip">IconStrip 句柄。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示句柄为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示句柄不是 IconStrip。
        /// </returns>
        public static int IconStrip_Refresh(IntPtr iconStrip)
        {
            if (iconStrip == IntPtr.Zero)
                return HResult.E_POINTER;

            return UIExt_IconStrip_Refresh(iconStrip);
        }

        // ============ 布局工具 ============

        // 对应 Phobos: HRESULT UIExt_Layout_ArrangeRow(void* const* ppItems, int count, int x, int y, int spacing)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_Layout_ArrangeRow(
            IntPtr[] ppItems, int count, int x, int y, int spacing);

        /// <summary>
        /// 把一组控件横向排成一行（从左到右，自动修改各控件位置）。
        /// 对应 Phobos Interop API：<c>UIExt_Layout_ArrangeRow</c>。
        /// </summary>
        /// <param name="items">控件句柄数组，可为 null 或空数组。</param>
        /// <param name="x">起始 X 坐标。</param>
        /// <param name="y">Y 坐标。</param>
        /// <param name="spacing">控件间距。</param>
        /// <returns><see cref="HResult.S_OK"/> 表示成功（无实际效果时也返回 S_OK）。</returns>
        public static int Layout_ArrangeRow(IntPtr[] items, int x, int y, int spacing)
        {
            return UIExt_Layout_ArrangeRow(items, items?.Length ?? 0, x, y, spacing);
        }

        // 对应 Phobos: HRESULT UIExt_Layout_ArrangeColumn(void* const* ppItems, int count, int x, int y, int spacing)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_Layout_ArrangeColumn(
            IntPtr[] ppItems, int count, int x, int y, int spacing);

        /// <summary>
        /// 把一组控件纵向排成一列（从上到下，自动修改各控件位置）。
        /// 对应 Phobos Interop API：<c>UIExt_Layout_ArrangeColumn</c>。
        /// </summary>
        /// <param name="items">控件句柄数组，可为 null 或空数组。</param>
        /// <param name="x">X 坐标。</param>
        /// <param name="y">起始 Y 坐标。</param>
        /// <param name="spacing">控件间距。</param>
        /// <returns><see cref="HResult.S_OK"/> 表示成功。</returns>
        public static int Layout_ArrangeColumn(IntPtr[] items, int x, int y, int spacing)
        {
            return UIExt_Layout_ArrangeColumn(items, items?.Length ?? 0, x, y, spacing);
        }

        // 对应 Phobos: HRESULT UIExt_Layout_ArrangeGrid(void* const* ppItems, int count, int x, int y, int columns,
        //     int itemWidth, int itemHeight, int gapX, int gapY)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int UIExt_Layout_ArrangeGrid(
            IntPtr[] ppItems, int count, int x, int y, int columns,
            int itemWidth, int itemHeight, int gapX, int gapY);

        /// <summary>
        /// 把一组控件按网格排列（自动修改各控件位置）。
        /// 对应 Phobos Interop API：<c>UIExt_Layout_ArrangeGrid</c>。
        /// </summary>
        /// <param name="items">控件句柄数组，可为 null 或空数组。</param>
        /// <param name="x">起始 X 坐标。</param>
        /// <param name="y">起始 Y 坐标。</param>
        /// <param name="columns">列数。</param>
        /// <param name="itemWidth">格子宽度。</param>
        /// <param name="itemHeight">格子高度。</param>
        /// <param name="gapX">水平间隙。</param>
        /// <param name="gapY">垂直间隙。</param>
        /// <returns><see cref="HResult.S_OK"/> 表示成功。</returns>
        public static int Layout_ArrangeGrid(
            IntPtr[] items, int x, int y, int columns,
            int itemWidth, int itemHeight, int gapX, int gapY)
        {
            return UIExt_Layout_ArrangeGrid(
                items, items?.Length ?? 0, x, y, columns, itemWidth, itemHeight, gapX, gapY);
        }
    }
}
