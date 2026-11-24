using System.Reflection;
using System.Text;
using FastMember;

namespace ReflectProbe.Benchmarks.Workloads;

// 숫자 필드 감시 (Exam3). 객체 1개의 숫자 필드를 읽어 한 줄씩 적는다.

internal static class WatchFormat
{
    public static bool IsNumeric(Type t)
        => t == typeof(int) || t == typeof(float) || t == typeof(double)
        || t == typeof(long) || t == typeof(short) || t == typeof(byte)
        || t == typeof(decimal) || t == typeof(uint) || t == typeof(ulong);

    public static void Append(StringBuilder sb, bool isPrivate, string name, object? value, Type type)
    {
        if (isPrivate)
            sb.Append("[private] ");
        sb.AppendLine($"{name}: {value} (타입: {type.Name})");
    }
}

/// <summary>원본 방식 : 객체마다 GetFields 를 다시 부른다.</summary>
internal sealed class ReflectionWatcher(BindingFlags flags)
{
    public void Watch(object target, StringBuilder sb)
    {
        foreach (FieldInfo field in target.GetType().GetFields(flags))
        {
            if (WatchFormat.IsNumeric(field.FieldType))
                WatchFormat.Append(sb, field.IsPrivate, field.Name, field.GetValue(target), field.FieldType);
        }
    }
}

/// <summary>필드 목록만 타입별로 캐싱한 Reflection (단일 스레드).</summary>
internal sealed class CachedReflectionWatcher(BindingFlags flags)
{
    private readonly Dictionary<Type, FieldInfo[]> _cache = new();

    public void Watch(object target, StringBuilder sb)
    {
        Type type = target.GetType();
        if (!_cache.TryGetValue(type, out FieldInfo[]? fields))
        {
            fields = type.GetFields(flags);
            _cache.Add(type, fields);
        }

        foreach (FieldInfo field in fields)
        {
            if (WatchFormat.IsNumeric(field.FieldType))
                WatchFormat.Append(sb, field.IsPrivate, field.Name, field.GetValue(target), field.FieldType);
        }
    }
}

internal sealed class FastMemberWatcher
{
    public void Watch(object target, StringBuilder sb)
    {
        TypeAccessor accessor = TypeAccessor.Create(target.GetType());
        MemberSet members = accessor.GetMembers();
        for (int i = 0; i < members.Count; i++)
        {
            Member member = members[i];
            if (WatchFormat.IsNumeric(member.Type))
                WatchFormat.Append(sb, false, member.Name, accessor[target, member.Name], member.Type);
        }
    }
}

internal sealed class ProbeWatcher(ProbeScope scope)
{
    public void Watch(object target, StringBuilder sb)
    {
        TypeProbe probe = TypeProbe.Of(target.GetType(), scope);
        ProbeMemberSet members = probe.Members;
        for (int i = 0; i < members.Count; i++)
        {
            ProbeMember member = members[i];
            if (member.Info is FieldInfo field && WatchFormat.IsNumeric(member.Type))
                WatchFormat.Append(sb, field.IsPrivate, member.Name, probe.GetValue(target, i), member.Type);
        }
    }
}
