namespace ReflectProbe.Tests;

// 같은 이름의 부모 멤버를 선언 깊이로 지정하는 probe[target, name, depthOffset]
public class DepthOffsetTests
{
    [Fact]
    public void Collect_RecordsDeclarationDepth()
    {
        ProbeMemberSet members = TypeProbe.Of<Puppy>().Members;

        Assert.Equal(0, members.Single(m => m.Name == "_tag" && m.DeclaringType == typeof(Puppy)).DepthOffset);
        Assert.Equal(1, members.Single(m => m.Name == "_tag" && m.DeclaringType == typeof(Dog)).DepthOffset);
        Assert.Equal(2, members.Single(m => m.Name == "_tag" && m.DeclaringType == typeof(Animal)).DepthOffset);
        Assert.Equal(2, members.Single(m => m.Name == "_age").DepthOffset);
    }

    [Fact]
    public void Indexer_DepthOffset_ReadsEachLevel()
    {
        TypeProbe probe = TypeProbe.Of<Puppy>();
        var puppy = new Puppy();

        Assert.Equal("puppy", probe[puppy, "_tag", 0]);
        Assert.Equal("dog", probe[puppy, "_tag", 1]);
        Assert.Equal("animal", probe[puppy, "_tag", 2]);
    }

    [Fact]
    public void Indexer_DepthZero_SameAsNameOnly()
    {
        TypeProbe probe = TypeProbe.Of<Dog>();
        var dog = new Dog();

        Assert.Equal(probe[dog, "_tag"], probe[dog, "_tag", 0]);
        Assert.Equal(probe.GetIndex("_tag"), probe.GetIndex("_tag", 0));
    }

    [Fact]
    public void Indexer_DepthOffset_WritesOnlyThatLevel()
    {
        TypeProbe probe = TypeProbe.Of<Dog>();
        var dog = new Dog();

        probe[dog, "_tag", 1] = "changed";

        Assert.Equal("dog", probe[dog, "_tag"]);
        Assert.Equal("changed", probe[dog, "_tag", 1]);
    }

    [Fact]
    public void Indexer_DepthOffset_FindsNotHiddenParentMember()
    {
        // _age 는 Animal 에만 있어 가려지지 않았지만 깊이 1 로도 찾을 수 있다.
        TypeProbe probe = TypeProbe.Of<Dog>();

        Assert.Equal(3, probe[new Dog(), "_age", 1]);
    }

    [Fact]
    public void GetIndex_DepthOffset_MatchesGetValue()
    {
        TypeProbe probe = TypeProbe.Of<Dog>();
        int index = probe.GetIndex("_tag", 1);

        Assert.True(probe.Members[index].IsHidden);
        Assert.Equal("animal", probe.GetValue(new Dog(), index));
    }

    [Fact]
    public void Indexer_NoMemberAtDepth_ThrowsMemberNotFound()
    {
        TypeProbe probe = TypeProbe.Of<Dog>();

        Assert.Throws<MemberNotFoundException>(() => probe[new Dog(), "_tag", 2]);
        Assert.Throws<MemberNotFoundException>(() => probe[new Dog(), "_age", 2]);
    }

    [Fact]
    public void Indexer_NegativeDepth_ThrowsArgumentOutOfRange()
    {
        TypeProbe probe = TypeProbe.Of<Dog>();

        Assert.Throws<ArgumentOutOfRangeException>(() => probe[new Dog(), "_tag", -1]);
    }
}
