namespace ReflectProbe.Tests;

public class TypeProbeNameIndexTests
{
    private readonly TypeProbe _playerProbe = TypeProbe.Of<Player>();

    // ---------- 이름 기반 ----------

    [Fact]
    public void Indexer_PrivateField_ReadsAndWrites()
    {
        var player = new Player();

        Assert.Equal(50, _playerProbe[player, "_hp"]);
        _playerProbe[player, "_hp"] = 30;
        Assert.Equal(30, player.Hp);
    }

    [Fact]
    public void Indexer_ReferenceTypeField_ReadsAndWrites()
    {
        var player = new Player();

        _playerProbe[player, "Name"] = "hero";
        Assert.Equal("hero", _playerProbe[player, "Name"]);
    }

    [Fact]
    public void Indexer_Property_ReadsAndWrites()
    {
        var player = new Player();

        _playerProbe[player, "Level"] = 5;
        Assert.Equal(5, player.Level);
        Assert.Equal(500, _playerProbe[player, "Score"]);
    }

    [Fact]
    public void Indexer_InstanceReadonlyField_Writes()
    {
        var player = new Player();

        _playerProbe[player, "_id"] = 99;
        Assert.Equal(99, player.Id);
    }

    [Fact]
    public void Indexer_SetOnlyProperty_Writes()
    {
        var player = new Player();

        _playerProbe[player, "SecretValue"] = 3;
        Assert.Equal(3, player.Secret);
    }

    [Fact]
    public void Indexer_StaticMembers_IgnoreTarget()
    {
        TypeProbe probe = TypeProbe.Of(typeof(GameConfig));

        probe[null, "MaxPlayers"] = 8;
        Assert.Equal(8, probe[null, "MaxPlayers"]);
        Assert.Equal(8, GameConfig.MaxPlayers);

        probe[null, "Mode"] = "hard";
        Assert.Equal("hard", probe[null, "Mode"]);
    }

    [Fact]
    public void Indexer_StaticReadonly_ReadsButThrowsOnWrite()
    {
        Assert.Equal(1, _playerProbe[null, "Version"]);

        var e = Assert.Throws<MemberNotWritableException>(() => _playerProbe[null, "Version"] = 2);
        Assert.Equal(typeof(Player), e.Type);
        Assert.Equal("Version", e.MemberName);
    }

    [Fact]
    public void Indexer_GetOnlyProperty_ThrowsOnWrite()
    {
        Assert.Throws<MemberNotWritableException>(() => _playerProbe[new Player(), "Score"] = 1);
    }

    [Fact]
    public void Indexer_SetOnlyProperty_ThrowsOnRead()
    {
        var e = Assert.Throws<MemberNotReadableException>(() => _playerProbe[new Player(), "SecretValue"]);
        Assert.Equal("SecretValue", e.MemberName);
    }

    [Fact]
    public void Indexer_UnknownName_ThrowsMemberNotFound()
    {
        var e = Assert.Throws<MemberNotFoundException>(() => _playerProbe[new Player(), "nope"]);
        Assert.Equal(typeof(Player), e.Type);
        Assert.Throws<MemberNotFoundException>(() => _playerProbe[new Player(), "nope"] = 1);
    }

    [Fact]
    public void Indexer_ExcludedMembers_ThrowMemberNotFound()
    {
        Assert.Throws<MemberNotFoundException>(() => _playerProbe[null, "MaxHp"]);
        Assert.Throws<MemberNotFoundException>(() => _playerProbe[new Player(), "<Level>k__BackingField"]);
    }

    [Fact]
    public void Indexer_IsCaseSensitive()
    {
        Assert.Throws<MemberNotFoundException>(() => _playerProbe[new Player(), "name"]);
    }

    // ---------- Try ----------

    [Fact]
    public void TryGetValue_ReturnsFalseForUnknownOrUnreadable()
    {
        var player = new Player();

        Assert.True(_playerProbe.TryGetValue(player, "_hp", out object? hp));
        Assert.Equal(50, hp);
        Assert.False(_playerProbe.TryGetValue(player, "nope", out object? missing));
        Assert.Null(missing);
        Assert.False(_playerProbe.TryGetValue(player, "SecretValue", out _));
    }

    [Fact]
    public void TrySetValue_ReturnsFalseForUnknownOrUnwritable()
    {
        var player = new Player();

        Assert.True(_playerProbe.TrySetValue(player, "_hp", 1));
        Assert.Equal(1, player.Hp);
        Assert.False(_playerProbe.TrySetValue(player, "nope", 1));
        Assert.False(_playerProbe.TrySetValue(player, "Score", 1));
        Assert.False(_playerProbe.TrySetValue(null, "Version", 2));
        Assert.Equal(1, Player.Version);
    }

    // ---------- 인덱스 기반 ----------

    [Fact]
    public void GetIndex_MatchesMember()
    {
        int index = _playerProbe.GetIndex("_hp");

        Assert.Equal("_hp", _playerProbe.Members[index].Name);
        Assert.True(_playerProbe.TryGetIndex("_hp", out int same));
        Assert.Equal(index, same);
        Assert.False(_playerProbe.TryGetIndex("nope", out _));
        Assert.Throws<MemberNotFoundException>(() => _playerProbe.GetIndex("nope"));
    }

    [Fact]
    public void GetValueSetValue_ByIndex()
    {
        var player = new Player();
        int index = _playerProbe.GetIndex("_hp");

        _playerProbe.SetValue(player, index, 77);
        Assert.Equal(77, _playerProbe.GetValue(player, index));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1000)]
    public void GetValueSetValue_OutOfRange_Throws(int index)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _playerProbe.GetValue(new Player(), index));
        Assert.Throws<ArgumentOutOfRangeException>(() => _playerProbe.SetValue(new Player(), index, 1));
    }

    // ---------- 상속 ----------

    [Fact]
    public void Indexer_BasePrivateField_ThroughDerivedProbe()
    {
        TypeProbe probe = TypeProbe.Of<Dog>();
        var dog = new Dog();

        probe[dog, "_age"] = 10;
        Assert.Equal(10, probe[dog, "_age"]);
    }

    [Fact]
    public void HiddenMember_NameResolvesToDerived_IndexReachesBase()
    {
        TypeProbe probe = TypeProbe.Of<Dog>();
        var dog = new Dog();

        Assert.Equal("dog", probe[dog, "_tag"]);

        ProbeMember hidden = probe.Members.Single(m => m.Name == "_tag" && m.IsHidden);
        Assert.Equal("animal", probe.GetValue(dog, hidden.Index));
        probe.SetValue(dog, hidden.Index, "base");
        Assert.Equal("base", probe.GetValue(dog, hidden.Index));
        Assert.Equal("dog", probe[dog, "_tag"]);
    }

    [Fact]
    public void Indexer_OverriddenProperty_CallsOverride()
    {
        Assert.Equal("woof", TypeProbe.Of<Animal>()[new Dog(), "Sound"]);
        Assert.Equal("woof", TypeProbe.Of<Dog>()[new Dog(), "Sound"]);
    }

    [Fact]
    public void Indexer_GetterOnlyOverride_WritesThroughBaseSetter()
    {
        TypeProbe probe = TypeProbe.Of<Dog>();
        var dog = new Dog();

        probe[dog, "Weight"] = 20;
        Assert.Equal(21, probe[dog, "Weight"]);
    }

    // ---------- struct ----------

    [Fact]
    public void BoxedStruct_FieldWrite_ModifiesBox()
    {
        TypeProbe probe = TypeProbe.Of<MutablePoint>();
        object boxed = new MutablePoint();

        probe[boxed, "X"] = 5;
        Assert.Equal(5, ((MutablePoint)boxed).X);
        Assert.Equal(5, probe[boxed, "X"]);
    }

    [Fact]
    public void BoxedStruct_PropertyWrite_ModifiesBox()
    {
        TypeProbe probe = TypeProbe.Of<MutablePoint>();
        object boxed = new MutablePoint();

        probe[boxed, "Y"] = 9;
        Assert.Equal(9, ((MutablePoint)boxed).Y);
        Assert.Equal(9, probe[boxed, "_y"]);
    }

    // ---------- 잘못된 입력 : 런타임 예외 그대로 전파 ----------

    [Fact]
    public void WrongValueType_ThrowsInvalidCast()
    {
        Assert.Throws<InvalidCastException>(() => _playerProbe[new Player(), "_hp"] = "text");
        Assert.Throws<InvalidCastException>(() => _playerProbe[new Player(), "Name"] = 1);
    }

    [Fact]
    public void WrongTargetType_ThrowsInvalidCast()
    {
        Assert.Throws<InvalidCastException>(() => _playerProbe[new Dog(), "_hp"]);
    }

    [Fact]
    public void NullTargetOrNullValueForValueType_ThrowsNullReference()
    {
        Assert.Throws<NullReferenceException>(() => _playerProbe[null, "_hp"]);
        Assert.Throws<NullReferenceException>(() => _playerProbe[new Player(), "_hp"] = null);
    }
}
