using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Decorators
{
    [Serializable]
    public abstract class Decorator
    {
        public string Description { get; set; }
        public DecoratorId ID { get; set; }
        public IDecorative Decorative { get; internal set; }
        // 优先级，get 时高优先级的排前面，会被先执行
        public virtual int Priority => 0;
    }

    [Serializable]
    public abstract class PairDecorator : Decorator
    {
        public virtual object Key { get; set; }
        public virtual object Value { get; set; }
        internal PairDecorator(object key, object val)
        {
            Key = key;
            Value = val;
        }
    }

    [Serializable]
    public class PairDecorator<TKey, TValue> : PairDecorator
    {
        private ValueTuple<TKey, TValue> pair;
        internal PairDecorator(TKey key, TValue val) : base(key, val)
        {
        }
        public override object Key { get => pair.Item1; set => pair.Item1 = (TKey)value; }
        public override object Value { get => pair.Item2; set => pair.Item2 = (TValue)value; }
    }

    // 事件装饰器接口
    public interface IEventDecorator
    {
        void OnUpdate();
        void OnReceiveDamage(Pointer<int> pDamage, int DistanceFromEpicenter, Pointer<WarheadTypeClass> pWH,
            Pointer<ObjectClass> pAttacker, bool IgnoreDefenses, bool PreventPassengerEscape, Pointer<HouseClass> pAttackingHouse);
        void OnFire(Pointer<AbstractClass> pTarget, int weaponIndex);
        DamageState AfterReceiveDamage(Pointer<int> pDamage, int DistanceFromEpicenter, Pointer<WarheadTypeClass> pWH,
            Pointer<ObjectClass> pAttacker, bool IgnoreDefenses, bool PreventPassengerEscape, Pointer<HouseClass> pAttackingHouse, DamageState result, int damageTaken);
        DamageState OnDealDamage(Pointer<int> pDamage, int DistanceFromEpicenter, Pointer<WarheadTypeClass> pWH,
            Pointer<TechnoClass> pVictim, bool IgnoreDefenses, bool PreventPassengerEscape, Pointer<HouseClass> pAttackingHouse, DamageState result, int damageDealt);
    }

    // 渲染装饰器接口
    public interface IRenderDecorator
    {
        void OnRender();
    }

    // 向后兼容的抽象基类，旧代码继承此类无需任何修改
    [Serializable]
    public abstract class EventDecorator : Decorator, IEventDecorator
    {
        public virtual void OnUpdate() { }
        public virtual void OnReceiveDamage(Pointer<int> pDamage, int DistanceFromEpicenter, Pointer<WarheadTypeClass> pWH,
            Pointer<ObjectClass> pAttacker, bool IgnoreDefenses, bool PreventPassengerEscape, Pointer<HouseClass> pAttackingHouse)
        { }
        public virtual void OnFire(Pointer<AbstractClass> pTarget, int weaponIndex) { }
        public virtual DamageState AfterReceiveDamage(Pointer<int> pDamage, int DistanceFromEpicenter, Pointer<WarheadTypeClass> pWH,
            Pointer<ObjectClass> pAttacker, bool IgnoreDefenses, bool PreventPassengerEscape, Pointer<HouseClass> pAttackingHouse, DamageState result, int damageTaken)
        { return result; }
        public virtual DamageState OnDealDamage(Pointer<int> pDamage, int DistanceFromEpicenter, Pointer<WarheadTypeClass> pWH,
            Pointer<TechnoClass> pVictim, bool IgnoreDefenses, bool PreventPassengerEscape, Pointer<HouseClass> pAttackingHouse, DamageState result, int damageDealt)
        { return result; }
    }

    // 向后兼容的抽象基类
    [Serializable]
    public abstract class RenderDecorator : Decorator, IRenderDecorator
    {
        public virtual void OnRender() { }
    }

    // 同时具备事件和渲染能力的组合基类，新代码使用此类
    [Serializable]
    public abstract class EventRenderDecorator : Decorator, IEventDecorator, IRenderDecorator
    {
        public virtual void OnUpdate() { }
        public virtual void OnReceiveDamage(Pointer<int> pDamage, int DistanceFromEpicenter, Pointer<WarheadTypeClass> pWH,
            Pointer<ObjectClass> pAttacker, bool IgnoreDefenses, bool PreventPassengerEscape, Pointer<HouseClass> pAttackingHouse)
        { }
        public virtual void OnFire(Pointer<AbstractClass> pTarget, int weaponIndex) { }
        public virtual DamageState AfterReceiveDamage(Pointer<int> pDamage, int DistanceFromEpicenter, Pointer<WarheadTypeClass> pWH,
            Pointer<ObjectClass> pAttacker, bool IgnoreDefenses, bool PreventPassengerEscape, Pointer<HouseClass> pAttackingHouse, DamageState result, int damageTaken)
        { return result; }
        public virtual DamageState OnDealDamage(Pointer<int> pDamage, int DistanceFromEpicenter, Pointer<WarheadTypeClass> pWH,
            Pointer<TechnoClass> pVictim, bool IgnoreDefenses, bool PreventPassengerEscape, Pointer<HouseClass> pAttackingHouse, DamageState result, int damageDealt)
        { return result; }
        public virtual void OnRender() { }
    }
}
