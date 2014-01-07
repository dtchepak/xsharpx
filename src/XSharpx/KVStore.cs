using System;
using System.Collections.Generic;

namespace XSharpx
{
    // Based on: https://github.com/NICTA/xsharpx/blob/free/src/XSharpx/PureIO.cs

    public abstract class KVStoreTerm<K, V, A>
    {
        public abstract X Fold<X>(
            Func<K, V, A, X> add
            , Func<K, Func<Option<V>, A>, X> get
            );

        internal class Add : KVStoreTerm<K, V, A>
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
                , Func<K, Func<Option<V>, A>, X> get)
            {
                return add(key, value, a);
            }
        }

        internal class Get : KVStoreTerm<K, V, A>
        {
            private readonly K key;
            private readonly Func<Option<V>, A> next;

            public Get(K key, Func<Option<V>, A> next)
            {
                this.key = key;
                this.next = next;
            }

            public override X Fold<X>(
                Func<K, V, A, X> add
                , Func<K, Func<Option<V>, A>, X> get)
            {
                return get(key, next);
            }
        }
    }

    public static class KVStoreTermFunctor
    {
        public static KVStoreTerm<K, V, B> Select<K, V, A, B>(this KVStoreTerm<K, V, A> d, Func<A, B> f)
        {
            return d.Fold<KVStoreTerm<K, V, B>>(
                           (k, v, a) => new KVStoreTerm<K, V, B>.Add(k, v, f(a))
                         , (k, next) => new KVStoreTerm<K, V, B>.Get(k, next.Select(f)));
        }
    }

    public abstract class KVStore<K, V, A>
    {
        public abstract X Fold<X>(Func<A, X> done, Func<KVStoreTerm<K, V, KVStore<K, V, A>>, X> more);

        internal class Pure : KVStore<K, V, A>
        {
            private readonly A a;
            public Pure(A a) { this.a = a; }
            public override X Fold<X>(Func<A, X> done, Func<KVStoreTerm<K, V, KVStore<K, V, A>>, X> more)
            {
                return done(a);
            }
        }

        internal class More : KVStore<K, V, A>
        {
            private readonly KVStoreTerm<K, V, KVStore<K, V, A>> more;
            public More(KVStoreTerm<K, V, KVStore<K, V, A>> more) { this.more = more; }

            public override X Fold<X>(Func<A, X> done, Func<KVStoreTerm<K, V, KVStore<K, V, A>>, X> more)
            {
                return more(this.more);
            }
        }
    }

    public class KVStore
    {
        public static KVStore<K, V, Unit> add<K, V>(K key, V value)
        {
            return new KVStore<K, V, Unit>.More(new KVStoreTerm<K, V, KVStore<K, V, Unit>>.Add(key, value, new KVStore<K, V, Unit>.Pure(new Unit())));
        }

        public static KVStore<K, V, Option<V>> get<K, V>(K key)
        {
            return new KVStore<K, V, Option<V>>
                .More(new KVStoreTerm<K, V, KVStore<K, V, Option<V>>>.Get(key, v => new KVStore<K, V, Option<V>>.Pure(v)));
        }
    }

    public static class KVStoreFunctor
    {
        public static KVStore<K, V, B> Select<K, V, A, B>(this KVStore<K, V, A> d, Func<A, B> f)
        {
            return d.Fold<KVStore<K, V, B>>(
                x => new KVStore<K, V, B>.Pure(f(x))
                , term => new KVStore<K, V, B>.More(term.Select(x => x.Select(f))));
        }

        public static KVStore<K, V, B> SelectMany<K, V, A, B>(this KVStore<K, V, A> d, Func<A, KVStore<K, V, B>> f)
        {
            return d.Fold(f, term => new KVStore<K, V, B>.More(term.Select(x => x.SelectMany(f))));
        }

        public static KVStore<K, V, C> SelectMany<K, V, A, B, C>(this KVStore<K, V, A> d, Func<A, KVStore<K, V, B>> f,
            Func<A, B, C> selector)
        {
            return SelectMany(d, a => Select(f(a), b => selector(a, b)));
        }
    }

    public static class KVStoreInterpreter
    {
        public static A InterpretWithSideEffects<K, V, A>(this KVStore<K, V, A> s)
        {
            return s.InterpretWithDictionary(new Dictionary<K, V>());
        }

        private static A InterpretWithDictionary<K, V, A>(this KVStore<K, V, A> s, Dictionary<K, V> acc)
        {
            return s.Fold(
                done => done
                , more => more.Fold(
                    (k, v, n) =>
                    {
                        acc.Add(k, v);
                        return InterpretWithDictionary(n, acc);
                    }
                    , (k, n) =>
                    {
                        var v = acc.ContainsKey(k) ? Option.Some(acc[k]) : Option.Empty;
                        return InterpretWithDictionary(n(v), acc);
                    })
                    );
        }
    }
}