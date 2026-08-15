using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Decorators
{
    [Serializable]
    class DecoratorMap
    {
        public DecoratorMap()
        {
            dictionary = new Dictionary<DecoratorId, Decorator>();

            pairs = new EnumerableBuffer<PairDecorator>(this);
            events = new InterfaceEnumerableBuffer<IEventDecorator>(this);
            drawables = new InterfaceEnumerableBuffer<IRenderDecorator>(this);
        }

        public TDecorator CreateDecorator<TDecorator>(DecoratorId id, string description, params object[] parameters) where TDecorator : Decorator
        {
            var decorator = Activator.CreateInstance(typeof(TDecorator), parameters) as TDecorator;
            decorator.Description = description;
            decorator.ID = id;

            this.Add(decorator);

            return decorator;
        }

        public Decorator Get(DecoratorId id)
        {
            if (this.TryGet(id, out Decorator decorator))
            {
                return decorator;
            }
            return null;
        }

        public bool TryGet(DecoratorId id, out Decorator decorator)
        {
            return dictionary.TryGetValue(id, out decorator);
        }

        public void Add(Decorator decorator)
        {
            dictionary.Add(decorator.ID, decorator);
            NotifyChanged();
        }

        public void Remove(Decorator decorator)
        {
            dictionary.Remove(decorator.ID);
            NotifyChanged();
        }

        public void Remove(DecoratorId id)
        {
            Decorator decorator = this.Get(id);
            if (decorator != null)
            {
                this.Remove(decorator);
            }
        }

        private Action NotifyChanged;

        public IEnumerable<PairDecorator> GetPairDecorators() => pairs.Get();
        public IEnumerable<IEventDecorator> GetEventDecorators() => events.Get();
        public IEnumerable<IRenderDecorator> GetDrawableDecorators() => drawables.Get();

        Dictionary<DecoratorId, Decorator> dictionary;

        [Serializable]
        class EnumerableBuffer<TDecorator> where TDecorator : Decorator
        {
            DecoratorMap map;
            List<TDecorator> list;
            public bool hasChanged = true;

            public EnumerableBuffer(DecoratorMap map)
            {
                this.map = map;
                map.NotifyChanged += () => hasChanged = true;
            }

            public IEnumerable<TDecorator> Get()
            {
                if (hasChanged)
                {
                    list = (from d in map.dictionary.Values where d is TDecorator select d as TDecorator)
                        .OrderByDescending(decorator => decorator.Priority)
                        .ToList();
                    hasChanged = false;
                }
                return list;
            }
        }

        [Serializable]
        class InterfaceEnumerableBuffer<TInterface>
        {
            DecoratorMap map;
            List<TInterface> list;
            public bool hasChanged = true;

            public InterfaceEnumerableBuffer(DecoratorMap map)
            {
                this.map = map;
                map.NotifyChanged += () => hasChanged = true;
            }

            public IEnumerable<TInterface> Get()
            {
                if (hasChanged)
                {
                    list = (from d in map.dictionary.Values where d is TInterface select (TInterface)(object)d)
                        .OrderByDescending(d => (d as Decorator)?.Priority ?? 0)
                        .ToList();
                    hasChanged = false;
                }
                return list;
            }
        }

        EnumerableBuffer<PairDecorator> pairs;
        InterfaceEnumerableBuffer<IEventDecorator> events;
        InterfaceEnumerableBuffer<IRenderDecorator> drawables;
    }
}
