using System;
using System.Runtime.InteropServices;

namespace InteropUtils
{
    public static class PhobosEventExt
    {
        // 对应 Phobos: HRESULT EventExt_AddEvent(EventExt* pEventExt)
        // CallingConvention 设定为 StdCall 对应 C++ 端的 __stdcall
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int EventExt_AddEvent(IntPtr pEventExt);

        /// <summary>
        /// 调用 Phobos 的事件添加逻辑。
        /// 对应 Phobos Interop API：<c>EventExt_AddEvent(EventExt* pEventExt)</c>。
        /// </summary>
        /// <param name="pEventExt">C++ 端的 EventExt 或相应结构体指针。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示 <c>AddEvent</c> 返回 true；
        /// <see cref="HResult.S_FALSE"/> 表示 <c>AddEvent</c> 返回 false；
        /// <see cref="HResult.E_POINTER"/> 表示 <paramref name="pEventExt"/> 为空。
        /// </returns>
        public static int AddEvent(IntPtr pEventExt)
        {
            if (pEventExt == IntPtr.Zero)
                return HResult.E_POINTER;

            return EventExt_AddEvent(pEventExt);
        }
    }
}
