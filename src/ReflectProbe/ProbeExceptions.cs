using System;

namespace ReflectProbe
{
    /// <summary>ReflectProbe 가 던지는 예외의 기반 타입. 한 번에 catch 할 때 쓴다.</summary>
    public class ProbeException : Exception
    {
        /// <summary>대상 타입 · 멤버 이름 · 메시지로 만든다.</summary>
        public ProbeException(Type type, string memberName, string message)
            : base(message)
        {
            Type = type;
            MemberName = memberName;
        }

        /// <summary>접근하려던 타입.</summary>
        public Type Type { get; }

        /// <summary>접근하려던 멤버의 이름 또는 시그니처.</summary>
        public string MemberName { get; }
    }

    /// <summary>범위 안에 그 이름(또는 시그니처)의 멤버가 없다.</summary>
    public sealed class MemberNotFoundException : ProbeException
    {
        /// <summary>찾지 못한 타입과 멤버 이름(시그니처)으로 만든다.</summary>
        public MemberNotFoundException(Type type, string memberName)
            : base(type, memberName, $"'{type.FullName}' 에 '{memberName}' 멤버가 없습니다.")
        {
        }
    }

    /// <summary>멤버는 있지만 읽을 수 없다. (getter 없는 프로퍼티)</summary>
    public sealed class MemberNotReadableException : ProbeException
    {
        /// <summary>읽을 수 없는 멤버의 타입과 이름으로 만든다.</summary>
        public MemberNotReadableException(Type type, string memberName)
            : base(type, memberName, $"'{type.FullName}.{memberName}' 은(는) 읽을 수 없습니다.")
        {
        }
    }

    /// <summary>멤버는 있지만 쓸 수 없다. (setter 없는 프로퍼티, static readonly 필드)</summary>
    public sealed class MemberNotWritableException : ProbeException
    {
        /// <summary>쓸 수 없는 멤버의 타입과 이름으로 만든다.</summary>
        public MemberNotWritableException(Type type, string memberName)
            : base(type, memberName, $"'{type.FullName}.{memberName}' 은(는) 쓸 수 없습니다.")
        {
        }
    }

    /// <summary>이름만으로 메서드를 찾았는데 오버로드가 2개 이상이다. 매개변수 타입을 함께 지정해야 한다.</summary>
    public sealed class AmbiguousMemberException : ProbeException
    {
        /// <summary>모호한 메서드의 타입과 이름으로 만든다.</summary>
        public AmbiguousMemberException(Type type, string memberName)
            : base(type, memberName, $"'{type.FullName}.{memberName}' 의 오버로드가 여러 개입니다. 매개변수 타입을 지정하세요.")
        {
        }
    }
}
