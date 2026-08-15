using PatcherYRpp;
using System;
using System.Runtime.InteropServices;

namespace InteropUtils
{
    public static class PhobosTechnoExt
    {
        // ============ ConvertToType ============
        // 对应 Phobos: HRESULT ConvertToType_Phobos(FootClass* pThis, TechnoTypeClass* toType)
        // 注意：Phobos 当前实现只接受 FootClass（Infantry/Unit/Aircraft），传入 Building 等非 Foot 类型会被拒绝并返回 E_INVALIDARG。
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ConvertToType_Phobos(IntPtr pThis, IntPtr toType);

        /// <summary>
        /// 将单位转换为另一种类型（Phobos 功能）。
        /// 对应 Phobos Interop API：<c>ConvertToType_Phobos(FootClass* pThis, TechnoTypeClass* toType)</c>。
        /// </summary>
        /// <param name="pTechno">要转换的单位，必须是 FootClass（Infantry/Unit/Aircraft）。</param>
        /// <param name="toType">目标单位类型，其 WhatAmI 必须与原单位一致。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 指针为空；
        /// <see cref="HResult.E_INVALIDARG"/> 类型不兼容（包含非 FootClass、目标类型与原类型不同 WhatAmI 等情形）。
        /// </returns>
        public static int ConvertToType(Pointer<FootClass> pTechno, Pointer<TechnoTypeClass> toType)
        {
            if (pTechno.IsNull || toType.IsNull)
                return HResult.E_POINTER;

            return ConvertToType_Phobos(pTechno, toType);
        }

        // ============ CalculateExtraThreatCallback ============

        /// <summary>
        /// 与 Phobos 中定义的计算额外威胁值的回调函数类型一致。
        /// 对应 C++: <c>typedef double (*CalculateExtraThreatCallback)(TechnoClass* pThis, ObjectClass* pTarget, double originalThreat)</c>
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate double CalculateExtraThreatCallback(IntPtr pThis, IntPtr pTarget, double originalThreat);

        // 对应 Phobos: HRESULT RegisterCalculateExtraThreatCallback(CalculateExtraThreatCallback callback)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int RegisterCalculateExtraThreatCallback(CalculateExtraThreatCallback callback);

        // 静态引用，防止委托被 GC 回收导致非托管层调用时出现 Access Violation (AV 报错)
        private static CalculateExtraThreatCallback _calculateExtraThreatCallbackInstance;

        /// <summary>
        /// 注册计算额外威胁值的回调函数。
        /// 对应 Phobos Interop API：<c>RegisterCalculateExtraThreatCallback(CalculateExtraThreatCallback callback)</c>。
        /// </summary>
        /// <param name="callback">
        /// 回调函数，接收单位指针、目标指针和原始威胁值，返回修改后的威胁值。
        /// Phobos 调用约定：<c>totalThreat = cb(pThis, pTarget, totalThreat)</c>。
        /// </param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示 <paramref name="callback"/> 为 null。
        /// </returns>
        public static int RegisterCalculateExtraThreatProvider(Func<Pointer<TechnoClass>, Pointer<ObjectClass>, double, double> callback)
        {
            if (callback == null)
                return HResult.E_POINTER;

            // 包装一层以适配底层 IntPtr 调用
            _calculateExtraThreatCallbackInstance = (IntPtr pThis, IntPtr pTarget, double originalThreat) =>
            {
                return callback(pThis, pTarget, originalThreat);
            };

            // 将此静态引用注册给 Phobos
            return RegisterCalculateExtraThreatCallback(_calculateExtraThreatCallbackInstance);
        }

        // ============ CalculateSightCallback ============

        /// <summary>
        /// 与 Phobos 中定义的计算视野的回调函数类型一致。
        /// 对应 C++: <c>typedef double (*CalculateSightCallback)(TechnoClass* pThis, double originalSight)</c>
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate double CalculateSightCallback(IntPtr pThis, double originalSight);

        // 对应 Phobos: HRESULT RegisterCalculateSightCallback(CalculateSightCallback callback)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int RegisterCalculateSightCallback(CalculateSightCallback callback);

        // 静态引用，防止委托被 GC 回收导致非托管层调用时出现 Access Violation (AV 报错)
        private static CalculateSightCallback _calculateSightCallbackInstance;

        /// <summary>
        /// 注册计算视野的回调函数。
        /// 对应 Phobos Interop API：<c>RegisterCalculateSightCallback(CalculateSightCallback callback)</c>。
        /// </summary>
        /// <param name="callback">
        /// 回调函数，接收单位指针和原始视野值，返回修改后的视野值。
        /// </param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示 <paramref name="callback"/> 为 null。
        /// </returns>
        public static int RegisterCalculateSightProvider(Func<Pointer<TechnoClass>, double, double> callback)
        {
            if (callback == null)
                return HResult.E_POINTER;

            // 包装一层以适配底层 IntPtr 调用
            _calculateSightCallbackInstance = (IntPtr pThis, double originalSight) =>
            {
                return callback(pThis, originalSight);
            };

            // 将此静态引用注册给 Phobos
            return RegisterCalculateSightCallback(_calculateSightCallbackInstance);
        }
    }
}
