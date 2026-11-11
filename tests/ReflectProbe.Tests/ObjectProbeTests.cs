using System;

namespace ReflectProbe.Tests;

public class ObjectProbeTests
{
    [Fact]
    public void Constructor_SetsTargetAndProbe()
    {
        var player = new Player();
        var op = new ObjectProbe(player);

        Assert.Same(player, op.Target);
        Assert.Same(TypeProbe.Of(typeof(Player)), op.Probe);
    }

    [Fact]
    public void Constructor_NullTarget_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ObjectProbe(null!));
    }

    [Fact]
    public void Indexer_Get_ReadsPrivateField()
    {
        var player = new Player();
        var op = new ObjectProbe(player);

        Assert.Equal(50, op["_hp"]);
    }

    [Fact]
    public void Indexer_Set_WritesPrivateField()
    {
        var player = new Player();
        var op = new ObjectProbe(player);

        op["_hp"] = 30;
        Assert.Equal(30, player.Hp);
    }

    [Fact]
    public void Indexer_UnknownMember_ThrowsMemberNotFound()
    {
        var op = new ObjectProbe(new Player());
        Assert.Throws<MemberNotFoundException>(() => op["nope"]);
    }

    [Fact]
    public void TryGetValue_ExistingReadableMember_ReturnsTrueWithValue()
    {
        var player = new Player();
        var op = new ObjectProbe(player);

        bool result = op.TryGetValue("_hp", out object? value);

        Assert.True(result);
        Assert.Equal(50, value);
    }

    [Fact]
    public void TryGetValue_NonExistentMember_ReturnsFalse()
    {
        var op = new ObjectProbe(new Player());

        Assert.False(op.TryGetValue("nope", out _));
    }

    [Fact]
    public void TryGetValue_SetOnlyMember_ReturnsFalse()
    {
        var op = new ObjectProbe(new Player());

        Assert.False(op.TryGetValue("SecretValue", out _));
    }

    [Fact]
    public void TrySetValue_ExistingWritableMember_ReturnsTrueAndWrites()
    {
        var player = new Player();
        var op = new ObjectProbe(player);

        bool result = op.TrySetValue("_hp", 99);

        Assert.True(result);
        Assert.Equal(99, player.Hp);
    }

    [Fact]
    public void TrySetValue_NonExistentMember_ReturnsFalse()
    {
        var op = new ObjectProbe(new Player());

        Assert.False(op.TrySetValue("nope", 0));
    }

    [Fact]
    public void TrySetValue_GetOnlyMember_ReturnsFalse()
    {
        var op = new ObjectProbe(new Player());

        Assert.False(op.TrySetValue("Score", 0));
    }

    [Fact]
    public void ProbeProperty_MatchesTypeProbeOfTargetType()
    {
        var player = new Player();
        var op = new ObjectProbe(player);

        Assert.Same(TypeProbe.Of(typeof(Player)), op.Probe);
    }

    [Fact]
    public void Constructor_Struct_BoxedTargetReadable()
    {
        var point = new MutablePoint { X = 3 };
        var op = new ObjectProbe(point);

        Assert.Equal(3, op["X"]);
    }

    [Fact]
    public void TargetProperty_ReturnsSameReference()
    {
        var player = new Player();
        var op = new ObjectProbe(player);

        Assert.Same(player, op.Target);
    }
}
