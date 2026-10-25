using System;
using System.Collections;
using System.Collections.Generic;

namespace ReflectProbe
{
    /// <summary>
    /// TypeProbe 가 수집한 멤버 목록. 이름 → 인덱스 맵과 같은 목록에서 만들어져 둘이 어긋나지 않는다.
    /// 순서는 Index 순서와 같다.
    /// </summary>
    public sealed class ProbeMemberSet : IReadOnlyList<ProbeMember>
    {
        internal ProbeMemberSet(ProbeMember[] members)
        {
            throw new NotImplementedException();
        }

        public int Count => throw new NotImplementedException();

        public ProbeMember this[int index] => throw new NotImplementedException();

        /// <summary>이름으로 찾는다. 없으면 MemberNotFoundException.</summary>
        public ProbeMember this[string name] => throw new NotImplementedException();

        public bool TryGet(string name, out ProbeMember member) => throw new NotImplementedException();

        public IEnumerator<ProbeMember> GetEnumerator() => throw new NotImplementedException();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
