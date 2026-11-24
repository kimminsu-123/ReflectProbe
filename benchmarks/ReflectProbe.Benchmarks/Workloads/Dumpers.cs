using System.Collections;
using System.Reflection;
using System.Text;
using FastMember;

namespace ReflectProbe.Benchmarks.Workloads;

// 재귀 오브젝트 덤퍼 (Exam2). 출력 형식은 같고 "멤버 목록을 얻고 값을 읽는" 부분만 다르다.
// 각 라이브러리를 사용자가 쓰는 방식 그대로 쓴다. 멤버 순서는 FastMember 만 이름순이라 줄 순서가 다를 수 있다.

internal abstract class Dumper
{
    public string Dump(object obj)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{obj.GetType().Name} {{");
        AppendObject(obj, sb, 1);
        sb.AppendLine("}");
        return sb.ToString();
    }

    protected abstract void AppendObject(object obj, StringBuilder sb, int tab);

    protected static string Padding(int tab) => "".PadLeft(4 * tab, ' ');

    protected void AppendMember(string name, Type type, object? value, StringBuilder sb, string padding, int tab)
    {
        sb.Append(padding);
        if (IsSimpleType(type))
        {
            sb.AppendLine($"{name} = {value}");
        }
        else if (value is IEnumerable enumerable)
        {
            sb.AppendLine($"{name} = [{string.Join(", ", enumerable.Cast<object>())}]");
        }
        else if (value == null)
        {
            sb.AppendLine($"{name} = null");
        }
        else
        {
            sb.AppendLine($"{name} = {type.Name} {{");
            AppendObject(value, sb, tab + 1);
            sb.AppendLine($"{padding}}}");
        }
    }

    private static bool IsSimpleType(Type t)
        => t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime);
}

/// <summary>원본 방식 : 객체마다 GetFields / GetProperties 를 다시 부른다.</summary>
internal sealed class ReflectionDumper(BindingFlags flags) : Dumper
{
    protected override void AppendObject(object obj, StringBuilder sb, int tab)
    {
        string padding = Padding(tab);
        foreach (FieldInfo field in obj.GetType().GetFields(flags))
        {
            if (!field.Name.StartsWith('<'))
                AppendMember(field.Name, field.FieldType, field.GetValue(obj), sb, padding, tab);
        }

        foreach (PropertyInfo property in obj.GetType().GetProperties(flags))
            AppendMember(property.Name, property.PropertyType, property.GetValue(obj), sb, padding, tab);
    }
}

/// <summary>멤버 목록만 타입별로 캐싱한 Reflection. 캐싱의 이득과 Emit 의 이득을 나눠 보기 위한 것 (단일 스레드).</summary>
internal sealed class CachedReflectionDumper(BindingFlags flags) : Dumper
{
    private readonly Dictionary<Type, (FieldInfo[] Fields, PropertyInfo[] Properties)> _cache = new();

    protected override void AppendObject(object obj, StringBuilder sb, int tab)
    {
        Type type = obj.GetType();
        if (!_cache.TryGetValue(type, out var members))
        {
            members = (type.GetFields(flags).Where(f => !f.Name.StartsWith('<')).ToArray(), type.GetProperties(flags));
            _cache.Add(type, members);
        }

        string padding = Padding(tab);
        foreach (FieldInfo field in members.Fields)
            AppendMember(field.Name, field.FieldType, field.GetValue(obj), sb, padding, tab);
        foreach (PropertyInfo property in members.Properties)
            AppendMember(property.Name, property.PropertyType, property.GetValue(obj), sb, padding, tab);
    }
}

internal sealed class FastMemberDumper : Dumper
{
    protected override void AppendObject(object obj, StringBuilder sb, int tab)
    {
        string padding = Padding(tab);
        TypeAccessor accessor = TypeAccessor.Create(obj.GetType());
        MemberSet members = accessor.GetMembers();
        for (int i = 0; i < members.Count; i++)
        {
            Member member = members[i];
            AppendMember(member.Name, member.Type, accessor[obj, member.Name], sb, padding, tab);
        }
    }
}

internal sealed class ProbeDumper(ProbeScope scope) : Dumper
{
    protected override void AppendObject(object obj, StringBuilder sb, int tab)
    {
        string padding = Padding(tab);
        TypeProbe probe = TypeProbe.Of(obj.GetType(), scope);
        ProbeMemberSet members = probe.Members;
        for (int i = 0; i < members.Count; i++)
        {
            ProbeMember member = members[i];
            AppendMember(member.Name, member.Type, probe.GetValue(obj, i), sb, padding, tab);
        }
    }
}
