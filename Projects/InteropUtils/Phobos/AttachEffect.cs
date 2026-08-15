using System;
using System.Reflection;
using System.Runtime.InteropServices;
using PatcherYRpp;

namespace InteropUtils
{
    public static class PhobosAttachEffect
    {
        /// <summary>
        /// RecreationDelay 的"不覆盖"哨兵值，与 Phobos C++ 中
        /// <c>RecreationDelay_NoOverride = INT_MIN</c> 保持一致。
        /// 传此值表示不修改 AttachEffectType 自身的 RecreationDelay。
        /// </summary>
        public const int RecreationDelay_NoOverride = int.MinValue;

        // 对应 Phobos:
        // HRESULT AE_Attach(TechnoClass* pTarget, HouseClass* pInvokerHouse, TechnoClass* pInvoker,
        //                    AbstractClass* pSource, const char** effectTypeNames, int typeCount,
        //                    int durationOverride, int delay, int initialDelay, int recreationDelay,
        //                    int* pAttachedCount)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int AE_Attach(
            IntPtr pTarget,
            IntPtr pInvokerHouse,
            IntPtr pInvoker,
            IntPtr pSource,
            [In] string[] effectTypeNames,
            int typeCount,
            int durationOverride,
            int delay,
            int initialDelay,
            int recreationDelay,
            out int pAttachedCount
        );

        // 对应 Phobos:
        // HRESULT AE_Detach(TechnoClass* pTarget, const char** effectTypeNames, int typeCount, int* pRemovedCount)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int AE_Detach(
            IntPtr pTarget,
            [In] string[] effectTypeNames,
            int typeCount,
            out int pRemovedCount
        );

        // 对应 Phobos:
        // HRESULT AE_DetachByGroups(TechnoClass* pTarget, const char** groupNames, int groupCount, int* pRemovedCount)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int AE_DetachByGroups(
            IntPtr pTarget,
            [In] string[] groupNames,
            int groupCount,
            out int pRemovedCount
        );

        // 对应 Phobos: HRESULT AE_TransferEffects(TechnoClass* pSource, TechnoClass* pTarget)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int AE_TransferEffects(
            IntPtr pSource,
            IntPtr pTarget
        );

        /// <summary>
        /// 向目标单位附加 AttachEffect。
        /// 对应 Phobos Interop API：<c>AE_Attach</c>。
        /// </summary>
        /// <param name="targetPtr">目标单位。</param>
        /// <param name="invokerHousePtr">施法方 House，可为 <see cref="Pointer{HouseClass}.Zero"/>。</param>
        /// <param name="invokerPtr">施法单位，可为 <see cref="Pointer{TechnoClass}.Zero"/>。</param>
        /// <param name="sourcePtr">来源对象，可为 <see cref="Pointer{AbstractClass}.Zero"/>。</param>
        /// <param name="effectTypes">要附加的 AttachEffect 类型名数组。</param>
        /// <param name="attachedCount">输出：实际成功附加的效果数量。</param>
        /// <param name="durationOverride">非 0 时覆盖持续时间。</param>
        /// <param name="delay">≥0 时覆盖延迟。</param>
        /// <param name="initialDelay">≥0 时覆盖初始延迟。</param>
        /// <param name="recreationDelay">≠ <see cref="RecreationDelay_NoOverride"/> 时覆盖重建延迟，默认不覆盖。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.S_FALSE"/> 表示未找到任何有效效果类型名（<paramref name="attachedCount"/> 为 0）；
        /// <see cref="HResult.E_POINTER"/> 表示 <paramref name="targetPtr"/> 或 <paramref name="effectTypes"/> 为空；
        /// <see cref="HResult.E_INVALIDARG"/> 表示 <paramref name="effectTypes"/> 长度 ≤ 0。
        /// </returns>
        public static int Attach(
            Pointer<TechnoClass> targetPtr,
            Pointer<HouseClass> invokerHousePtr,
            Pointer<TechnoClass> invokerPtr,
            Pointer<AbstractClass> sourcePtr,
            string[] effectTypes,
            out int attachedCount,
            int durationOverride = 0,
            int delay = 0,
            int initialDelay = 0,
            int recreationDelay = RecreationDelay_NoOverride)
        {
            if (targetPtr.IsNull || effectTypes == null)
            {
                attachedCount = 0;
                return HResult.E_POINTER;
            }

            if (effectTypes.Length <= 0)
            {
                attachedCount = 0;
                return HResult.E_INVALIDARG;
            }

            return AE_Attach(
                targetPtr,
                invokerHousePtr,
                invokerPtr,
                sourcePtr,
                effectTypes,
                effectTypes.Length,
                durationOverride,
                delay,
                initialDelay,
                recreationDelay,
                out attachedCount
            );
        }

        /// <summary>
        /// 从目标单位按效果类型名移除 AttachEffect。
        /// 对应 Phobos Interop API：<c>AE_Detach</c>。
        /// </summary>
        /// <param name="targetPtr">目标单位。</param>
        /// <param name="effectTypes">要移除的 AttachEffect 类型名数组。</param>
        /// <param name="removedCount">输出：实际移除的效果数量。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.S_FALSE"/> 表示未找到匹配效果（<paramref name="removedCount"/> 为 0）；
        /// <see cref="HResult.E_POINTER"/> 表示参数为空；<see cref="HResult.E_INVALIDARG"/> 表示数组长度 ≤ 0。
        /// </returns>
        public static int Detach(Pointer<TechnoClass> targetPtr, string[] effectTypes, out int removedCount)
        {
            if (targetPtr.IsNull || effectTypes == null)
            {
                removedCount = 0;
                return HResult.E_POINTER;
            }

            if (effectTypes.Length <= 0)
            {
                removedCount = 0;
                return HResult.E_INVALIDARG;
            }

            return AE_Detach(targetPtr, effectTypes, effectTypes.Length, out removedCount);
        }

        /// <summary>
        /// 从目标单位按组名移除 AttachEffect。
        /// 对应 Phobos Interop API：<c>AE_DetachByGroups</c>。
        /// </summary>
        /// <param name="targetPtr">目标单位。</param>
        /// <param name="groupNames">要移除的组名数组。</param>
        /// <param name="removedCount">输出：实际移除的效果数量。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.S_FALSE"/> 表示未找到匹配组（<paramref name="removedCount"/> 为 0）；
        /// <see cref="HResult.E_POINTER"/> 表示参数为空；<see cref="HResult.E_INVALIDARG"/> 表示数组长度 ≤ 0。
        /// </returns>
        public static int DetachByGroups(Pointer<TechnoClass> targetPtr, string[] groupNames, out int removedCount)
        {
            if (targetPtr.IsNull || groupNames == null)
            {
                removedCount = 0;
                return HResult.E_POINTER;
            }

            if (groupNames.Length <= 0)
            {
                removedCount = 0;
                return HResult.E_INVALIDARG;
            }

            return AE_DetachByGroups(targetPtr, groupNames, groupNames.Length, out removedCount);
        }

        /// <summary>
        /// 将一个单位身上的所有 AttachEffect 转移到另一个单位。
        /// 对应 Phobos Interop API：<c>AE_TransferEffects</c>。
        /// </summary>
        /// <param name="sourcePtr">来源单位。</param>
        /// <param name="targetPtr">目标单位。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功；<see cref="HResult.E_POINTER"/> 表示任一指针为空。
        /// </returns>
        public static int TransferEffects(Pointer<TechnoClass> sourcePtr, Pointer<TechnoClass> targetPtr)
        {
            if (sourcePtr.IsNull || targetPtr.IsNull)
                return HResult.E_POINTER;

            return AE_TransferEffects(sourcePtr, targetPtr);
        }
    }
}
