using System;

namespace ReflectProbe.Internal
{
    /// <summary>
    /// (Type, ProbeScope) → TypeProbe 영구 캐시.
    /// ConcurrentDictionary&lt;(Type, ProbeScope), Lazy&lt;TypeProbe&gt;&gt; + LazyThreadSafetyMode.ExecutionAndPublication 으로
    /// 동시에 최초 생성이 들어와도 IL 생성은 1회만 일어난다. (FastMember: Hashtable 2개 + 서로 다른 락 → 경쟁 가능)
    /// </summary>
    internal static class ProbeCache
    {
        public static TypeProbe GetOrCreate(Type type, ProbeScope scope) => throw new NotImplementedException();
    }
}
