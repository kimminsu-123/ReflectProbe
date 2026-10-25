using System;
using System.Collections.Generic;

namespace ReflectProbe.Internal
{
    /// <summary>
    /// 범위에 맞는 필드·프로퍼티를 수집해 멤버 목록과 이름 → 인덱스 맵을 함께 만든다.
    /// - 인덱서 제외, [CompilerGenerated] 백킹 필드 제외
    /// - NonPublic 이면 BaseType 체인을 올라가며 부모의 private 필드까지 수집
    /// - 같은 이름은 가장 파생된 타입의 멤버만 맵에 등록, 나머지는 IsHidden = true
    /// </summary>
    internal static class MemberCollector
    {
        public static ProbeMember[] Collect(Type type, ProbeScope scope, out Dictionary<string, int> nameToIndex)
            => throw new NotImplementedException();
    }
}
