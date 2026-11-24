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

        // 상속 관계가 없는 부모 인터페이스끼리 이름이 같은 멤버. 이름 맵에는 넣지 않는다. 대부분의 타입은 null.
        private readonly HashSet<string>? _ambiguousNames;

        internal ProbeMemberSet(Type type, ProbeMember[] members)
        {
            _type = type;
            _members = members;
            _nameToIndex = new Dictionary<string, int>(members.Length, StringComparer.Ordinal);
            foreach (ProbeMember member in members)
            {
                if (!member.IsHidden && !_nameToIndex.TryAdd(member.Name, member.Index))
                    (_ambiguousNames ??= new HashSet<string>(StringComparer.Ordinal)).Add(member.Name);
            }

            if (_ambiguousNames == null)
                return;
            foreach (string name in _ambiguousNames)
                _nameToIndex.Remove(name);
        }

        /// <summary>가려진(IsHidden) 멤버를 포함한 개수.</summary>
        public int Count => _members.Length;

        /// <summary>Index 순서로 찾는다. 범위 밖이면 IndexOutOfRangeException.</summary>
        public ProbeMember this[int index] => _members[index];

        /// <summary>이름으로 찾는다. 없으면 MemberNotFoundException, 모호하면 AmbiguousMemberException.</summary>
        public ProbeMember this[string name]
            => TryGet(name, out ProbeMember? member) ? member : throw LookupFailed(name);

        /// <summary>이름으로 찾는다. 같은 이름이 여러 개면 가장 파생된 멤버. 없거나 모호하면 false.</summary>
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

        /// <summary>이름으로 찾지 못한 이유에 맞는 예외.</summary>
        internal ProbeException LookupFailed(string name)
            => _ambiguousNames?.Contains(name) == true ? new AmbiguousMemberException(_type, name) : new MemberNotFoundException(_type, name);

        /// <summary>이름 + 선언 깊이로 찾지 못한 이유에 맞는 예외.</summary>
        internal ProbeException LookupFailed(string name, int depthOffset)
        {
            if (depthOffset == 0)
                return LookupFailed(name);
            return CountAtDepth(name, depthOffset, out _) > 1 ? new AmbiguousMemberException(_type, name) : new MemberNotFoundException(_type, name);
        }

        /// <summary>
        /// 이름 + 선언 깊이로 찾는다. depthOffset 0 은 TryGet(name) 과 같다 (가장 파생된 멤버).
        /// 1 이상이면 그 깊이에서 선언된 같은 이름 멤버를 찾는다. 가려진(IsHidden) 멤버도 포함. 없거나 그 깊이에 2개 이상이면 false.
        /// 히트 경로가 아니라 선형 탐색한다. (호출 측은 GetIndex 로 인덱스를 받아 두고 쓴다)
        /// </summary>
        public bool TryGet(string name, int depthOffset, [MaybeNullWhen(false)] out ProbeMember member)
        {
            if (depthOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(depthOffset), depthOffset, "0 이상이어야 합니다.");
            if (depthOffset == 0)
                return TryGet(name, out member);

            return CountAtDepth(name, depthOffset, out member) == 1;
        }

        private int CountAtDepth(string name, int depthOffset, out ProbeMember? member)
        {
            int count = 0;
            member = null;
            foreach (ProbeMember candidate in _members)
            {
                if (candidate.DepthOffset != depthOffset || !string.Equals(candidate.Name, name, StringComparison.Ordinal))
                    continue;
                member = candidate;
                count++;
            }

            return count;
        }

        /// <summary>Index 순서로 열거한다.</summary>
        public IEnumerator<ProbeMember> GetEnumerator() => ((IEnumerable<ProbeMember>)_members).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
