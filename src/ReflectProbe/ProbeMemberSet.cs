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

        /// <summary>
        /// 이름 + 선언 깊이로 찾는다. depthOffset 0 은 TryGet(name) 과 같다 (가장 파생된 멤버).
        /// 1 이상이면 그 깊이에서 선언된 같은 이름 멤버를 찾는다. 가려진(IsHidden) 멤버도 포함.
        /// 히트 경로가 아니라 선형 탐색한다. (호출 측은 GetIndex 로 인덱스를 받아 두고 쓴다)
        /// </summary>
        public bool TryGet(string name, int depthOffset, [MaybeNullWhen(false)] out ProbeMember member)
        {
            if (depthOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(depthOffset), depthOffset, "0 이상이어야 합니다.");
            if (depthOffset == 0)
                return TryGet(name, out member);

            foreach (ProbeMember candidate in _members)
            {
                if (candidate.DepthOffset == depthOffset && string.Equals(candidate.Name, name, StringComparison.Ordinal))
                {
                    member = candidate;
                    return true;
                }
            }

            member = null;
            return false;
        }

        public IEnumerator<ProbeMember> GetEnumerator() => ((IEnumerable<ProbeMember>)_members).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
