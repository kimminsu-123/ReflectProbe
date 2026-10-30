using ReflectProbe.Internal;

namespace ReflectProbe.Tests;

public class MemberCollectorTests
{
    private static ProbeMember Single(ProbeMember[] members, string name, bool isHidden = false)
        => Assert.Single(members, m => m.Name == name && m.IsHidden == isHidden);

    [Fact]
    public void Collect_ExcludesConstBackingFieldAndIndexer()
    {
        ProbeMember[] members = MemberCollector.Collect(typeof(Player), ProbeScope.All);

        string[] expected =
        [
            "Version", "_createdCount", "_hp", "Name", "_id", "_secret",
            "Level", "Score", "SecretValue", "Hp", "Id", "Secret"
        ];
        Assert.Equal(expected.OrderBy(n => n), members.Select(m => m.Name).OrderBy(n => n));
    }

    [Fact]
    public void Collect_IndexMatchesArrayPosition()
    {
        ProbeMember[] members = MemberCollector.Collect(typeof(Player), ProbeScope.All);

        for (int i = 0; i < members.Length; i++)
            Assert.Equal(i, members[i].Index);
    }

    [Fact]
    public void Collect_ComputesReadWriteFlags()
    {
        ProbeMember[] members = MemberCollector.Collect(typeof(Player), ProbeScope.All);

        ProbeMember version = Single(members, "Version");
        Assert.True(version.CanRead);
        Assert.False(version.CanWrite);
        Assert.True(version.IsStatic);

        ProbeMember id = Single(members, "_id");
        Assert.True(id.CanWrite);
        Assert.False(id.IsPublic);

        ProbeMember score = Single(members, "Score");
        Assert.Equal(ProbeMemberKind.Property, score.Kind);
        Assert.True(score.CanRead);
        Assert.False(score.CanWrite);

        ProbeMember secretValue = Single(members, "SecretValue");
        Assert.False(secretValue.CanRead);
        Assert.True(secretValue.CanWrite);
    }

    [Fact]
    public void Collect_PublicScope_ExcludesNonPublic()
    {
        ProbeMember[] members = MemberCollector.Collect(typeof(Player), ProbeScope.Public | ProbeScope.Instance | ProbeScope.Static);

        Assert.All(members, m => Assert.True(m.IsPublic));
        Assert.DoesNotContain(members, m => m.Name == "_hp");
        Assert.Contains(members, m => m.Name == "Version");
    }

    [Fact]
    public void Collect_InstanceScope_ExcludesStatic()
    {
        ProbeMember[] members = MemberCollector.Collect(typeof(Player), ProbeScope.Public | ProbeScope.NonPublic | ProbeScope.Instance);

        Assert.All(members, m => Assert.False(m.IsStatic));
    }

    [Fact]
    public void Collect_IncludesBasePrivateField()
    {
        ProbeMember[] members = MemberCollector.Collect(typeof(Dog), ProbeScope.All);

        ProbeMember age = Single(members, "_age");
        Assert.Equal(typeof(Animal), age.DeclaringType);
    }

    [Fact]
    public void Collect_SameName_DerivedWinsAndBaseIsHidden()
    {
        ProbeMember[] members = MemberCollector.Collect(typeof(Dog), ProbeScope.All);

        Assert.Equal(typeof(Dog), Single(members, "_tag").DeclaringType);
        Assert.Equal(typeof(Animal), Single(members, "_tag", isHidden: true).DeclaringType);
    }

    [Fact]
    public void Collect_OverriddenProperty_CollectedOnce()
    {
        ProbeMember[] members = MemberCollector.Collect(typeof(Dog), ProbeScope.All);

        Assert.Equal(typeof(Dog), Assert.Single(members, m => m.Name == "Sound").DeclaringType);
        Assert.Equal(typeof(Dog), Assert.Single(members, m => m.Name == "Legs").DeclaringType);
    }

    [Fact]
    public void Collect_GetterOnlyOverride_KeepsInheritedSetter()
    {
        ProbeMember weight = Assert.Single(MemberCollector.Collect(typeof(Dog), ProbeScope.All), m => m.Name == "Weight");

        Assert.True(weight.CanRead);
        Assert.True(weight.CanWrite);
    }

    [Fact]
    public void Collect_TypeWithoutMembers_ReturnsEmpty()
    {
        Assert.Empty(MemberCollector.Collect(typeof(object), ProbeScope.All));
    }
}
