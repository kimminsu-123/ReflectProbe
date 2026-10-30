using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace ReflectProbe.Internal
{
    /// <summary>
    /// (Type, ProbeScope) → TypeProbe 영구 캐시.
    /// ConcurrentDictionary&lt;(Type, ProbeScope), Lazy&lt;TypeProbe&gt;&gt; + LazyThreadSafetyMode.ExecutionAndPublication 으로
    /// 동시에 최초 생성이 들어와도 IL 생성은 1회만 일어난다. (FastMember: Hashtable 2개 + 서로 다른 락 → 경쟁 가능)
    /// </summary>
    internal static class ProbeCache
    {
        private static readonly ConcurrentDictionary<(Type, ProbeScope), Lazy<TypeProbe>> _cache
            = new ConcurrentDictionary<(Type, ProbeScope), Lazy<TypeProbe>>();

        public static TypeProbe GetOrCreate(Type type, ProbeScope scope)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            if (_cache.TryGetValue((type, scope), out Lazy<TypeProbe>? lazy))
                return lazy.Value;

            // Lazy 는 팩토리 예외도 영구 캐싱하므로 TypeProbe<T> 로 만들 수 없는 타입은 캐시에 넣기 전에 막는다.
            if (type.ContainsGenericParameters || type.IsByRef || type.IsPointer || type.IsByRefLike || type == typeof(void))
                throw new ArgumentException($"'{type}' 은(는) TypeProbe 대상이 될 수 없습니다.", nameof(type));

            return _cache.GetOrAdd((type, scope), static key => new Lazy<TypeProbe>(
                () => Create(key.Item1, key.Item2), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        }

        /// <summary>TypeProbe&lt;T&gt; 를 만든다. 타입당 1회라 생성자 Invoke 비용은 문제되지 않는다.</summary>
        private static TypeProbe Create(Type type, ProbeScope scope)
        {
            ConstructorInfo constructor = typeof(TypeProbe<>).MakeGenericType(type).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(ProbeScope), typeof(ProbeBuilder) }, null)!;

            try
            {
                return (TypeProbe)constructor.Invoke(new object[] { scope, ProbeBuilder.Current });
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                throw;
            }
        }
    }
}
