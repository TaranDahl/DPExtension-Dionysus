using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Decorators
{
    public interface IDecorative
    {
        public TDecorator CreateDecorator<TDecorator>(DecoratorId id, string description, params object[] parameters) where TDecorator : Decorator;
        public Decorator Get(DecoratorId id);
        public void Remove(DecoratorId id);
        public void Remove(Decorator decorator);
    }

    // 用于 Decorator 子类（PairDecorator 等），保留原有约束
    public interface IDecorative<TDecorator> where TDecorator : Decorator
    {
        public IEnumerable<TDecorator> GetDecorators();
    }

    // 用于接口类型（IEventDecorator、IRenderDecorator），无 Decorator 约束
    public interface IDecorativeInterface<TInterface>
    {
        public IEnumerable<TInterface> GetDecorators();
    }
}
