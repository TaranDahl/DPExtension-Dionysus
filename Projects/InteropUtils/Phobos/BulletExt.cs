using System;
using System.Runtime.InteropServices;
using PatcherYRpp;

namespace InteropUtils
{
    /// <summary>
    /// 子弹扩展相关的 Interop 方法
    /// </summary>
    public static class PhobosBulletExt
    {
        // 对应 Phobos: HRESULT Bullet_SetFirerOwner(BulletClass* pBullet, HouseClass* pHouse)
        [DllImport("Phobos.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int Bullet_SetFirerOwner(
            IntPtr pBullet,
            IntPtr pHouse
        );

        /// <summary>
        /// 设置子弹的作战方。
        /// 对应 Phobos Interop API：<c>Bullet_SetFirerOwner(BulletClass* pBullet, HouseClass* pHouse)</c>。
        /// </summary>
        /// <param name="pBullet">指向 BulletClass 实例的指针，不可为零。</param>
        /// <param name="pHouse">指向 HouseClass 实例的指针，可为 <see cref="Pointer{HouseClass}.Zero"/>（表示清除）。</param>
        /// <returns>
        /// <see cref="HResult.S_OK"/> 表示成功找到 BulletExt 并完成更新；
        /// <see cref="HResult.E_POINTER"/> 表示 <paramref name="pBullet"/> 为空；
        /// <see cref="HResult.E_UNEXPECTED"/> 表示 <paramref name="pBullet"/> 没有 BulletExt 扩展数据。
        /// </returns>
        public static int SetFirerOwner(Pointer<BulletClass> pBullet, Pointer<HouseClass> pHouse)
        {
            if (pBullet.IsNull)
                return HResult.E_POINTER;

            return Bullet_SetFirerOwner(pBullet, pHouse);
        }
    }
}
