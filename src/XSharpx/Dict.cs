using System;

namespace XSharpx
{
    // Based on: https://github.com/NICTA/xsharpx/blob/free/src/XSharpx/PureIO.cs

    public abstract class DictTerm<K, V, A>
    {
        public abstract X Fold<X>(
            Func<K, V, A, X> add
            , Func<K, Func<V, A>, X> get
            );

        internal class Add : DictTerm<K, V, A>
        {
            private readonly K key;
            private readonly V value;
            private readonly A a;

            public Add(K key, V value, A a)
            {
                this.key = key;
                this.value = value;
                this.a = a;
            }

            public override X Fold<X>(
                Func<K, V, A, X> add
                , Func<K, Func<V, A>, X> get)
            {
                return add(key, value, a);
            }
        }

        internal class Get : DictTerm<K, V, A>
        {
            private readonly K key;
            private readonly Func<V, A> next;

            public Get(K key, Func<V, A> next)
            {
                this.key = key;
                this.next = next;
            }

            public override X Fold<X>(
                Func<K, V, A, X> add
                , Func<K, Func<V, A>, X> get)
            {
                return get(key, next);
            }
        }
    }

    public static class DictTermFunctor
    {
        public static DictTerm<K, V, B> Select<K, V, A, B>(this DictTerm<K, V, A> d, Func<A, B> f)
        {
            return d.Fold<DictTerm<K,V,B>>( 
                           (k, v, a) => new DictTerm<K, V, B>.Add(k, v, f(a))
                         , (k, next) => new DictTerm<K, V, B>.Get(k, next.Select(f)));
        }
    }

    public abstract class Dict<A>
    {

    }
}