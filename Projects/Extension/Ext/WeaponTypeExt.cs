using InteropUtils;
using PatcherYRpp;
using System;

namespace Extension.Ext
{
    [Serializable]
    public class WeaponTypeExt
    {
        /// <summary>
        /// 在指定位置以 Limbo 方式简单发射一枚子弹。
        /// 子弹会被创建、设置武器类型与开火方阵营，然后 Limbo 并 MoveTo 到开火位置。
        /// </summary>
        /// <param name="pWeapon">武器类型。</param>
        /// <param name="pTarget">目标。</param>
        /// <param name="pOwner">发射者（可为 Zero，表示无单位发射）。</param>
        /// <param name="pFirerHouse">开火方阵营（用于 SetFirerOwner）。</param>
        /// <param name="spawnCoord">子弹的生成坐标（即开火位置）。</param>
        /// <returns>创建的子弹指针，失败时为 Zero。</returns>
        public static Pointer<BulletClass> SimpleFire(
            Pointer<WeaponTypeClass> pWeapon,
            Pointer<AbstractClass> pTarget,
            Pointer<TechnoClass> pOwner,
            Pointer<HouseClass> pFirerHouse,
            CoordStruct spawnCoord)
        {
            if (pWeapon.IsNull)
                return Pointer<BulletClass>.Zero;

            var pBullet = pWeapon.Ref.Projectile.Ref.CreateBullet(pTarget, pOwner, pWeapon);
            if (pBullet.IsNull)
                return Pointer<BulletClass>.Zero;

            PhobosBulletExt.SetFirerOwner(pBullet, pFirerHouse);
            pBullet.Ref.Range = pWeapon.Ref.Range;
            pBullet.Ref.Base.Remove();
            pBullet.Ref.MoveTo(spawnCoord);

            return pBullet;
        }

        /// <summary>
        /// 在指定坐标原地引爆武器，并设置抛射体的开火作战方。
        /// </summary>
        /// <param name="pWeapon">武器类型。</param>
        /// <param name="crd">引爆坐标。</param>
        /// <param name="pTarget">目标（用于伤害归因）。</param>
        /// <param name="pFirer">发射者（可为 Zero）。</param>
        /// <param name="pFirerHouse">开火方阵营（用于 SetFirerOwner）。</param>
        public static void Detonate(
            Pointer<WeaponTypeClass> pWeapon,
            CoordStruct crd,
            Pointer<AbstractClass> pTarget,
            Pointer<TechnoClass> pFirer,
            Pointer<HouseClass> pFirerHouse)
        {
            if (pWeapon.IsNull)
                return;

            var pBullet = pWeapon.Ref.Projectile.Ref.CreateBullet(pTarget, pFirer, pWeapon);
            if (pBullet.IsNull)
                return;

            PhobosBulletExt.SetFirerOwner(pBullet, pFirerHouse);

            pBullet.Ref.Base.SetLocation(crd);
            pBullet.Ref.Explode(true);
            pBullet.Ref.Base.UnInit();
        }
    }
}
