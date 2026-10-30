using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace ReflectProbe
{
    /// <summary>
    /// TypeProbe 가 수집한 멤버 목록. 이름 → 인덱스 맵과 같은 목록에서 만들어져 둘이 어긋나지 않는다.
    /// 순서는 Index 순서와 같다.
    /// </summary>
    public sealed class ProbeMemberSet : IReadOnlyList<ProbeMember>
    {
        private readonly Type _type;
        private readonly ProbeMember[] _members;

        // TypeProbe 의 이름 기반 API 도 이 맵 하나를 쓴다.
        private readonly Dictionary<string, int> _nameToIndex;

        internal ProbeMemberSet(Type type, ProbeMember[] members)
        {
            _type = type;
            _members = members;
            _nameToIndex = new Dictionary<string, int>(members.Length, StringComparer.Ordinal);
            foreach (ProbeMember member in members)
            {
                if (!member.IsHidden)
                    _nameToIndex.Add(member.Name, member.Index);
            }
        }

        public int Count => _members.Length;

        public ProbeMember this[int index] => _members[index];

        /// <summary>이름으로 찾는다. 없으면 MemberNotFoundException.</summary>
        public ProbeMember this[string name]
            => TryGet(name, out ProbeMember? member) ? member : throw new MemberNotFoundException(_type, name);

        public bool TryGet(string name, [MaybeNullWhen(false)] out ProbeMember member)
        {
            if (_nameToIndex.TryGetValue(name, out int index))
            {
                member = _members[index];
                return true;
            }

            member = null;
            return false;
        }

        internal bool TryGetIndex(string name, out int index) => _nameToIndex.TryGetValue(name, out index);

        public IEnumerator<ProbeMember> GetEnumerator() => ((IEnumerable<ProbeMember>)_members).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
