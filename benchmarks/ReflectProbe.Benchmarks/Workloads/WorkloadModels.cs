// 워크로드 모델. ReflectionBenchmark/Exam2(덤퍼) · Exam3(필드 감시) 에서 옮겼다.
// Public*  : FastMember 도 접근할 수 있도록 같은 멤버를 public 으로 바꾼 것 (워크로드 A, 3자 비교)
// Private* : 원본 그대로 private 필드를 가진 것 (워크로드 B, FastMember 불가 영역)
#pragma warning disable CS0169, CS0414, CS0649, IDE0044, IDE0052 // 리플렉션으로만 읽는 필드

namespace ReflectProbe.Benchmarks.Workloads;

public class Inventory
{
    public int Gold { get; set; }
    public List<string> Items { get; set; } = new();
}

// ---------- A : public ----------

public class PublicPlayer
{
    public int hp;
    public int mana;

    public string? Name { get; set; }
    public int Level { get; set; }
    public Inventory Inventory { get; set; } = new();
}

public class PublicGuild
{
    public string? guildName;
    public int level;
    public PublicPlayer? master;
    public Inventory? storage;
}

public class PublicEnemy
{
    public int hp = 50;
    public string internalId = "e_001";
    public float speed = 3.5f;
    public static int enemyCount = 3;
}

// ---------- B : private 원본 ----------

public class PrivatePlayer
{
    private int hp;
    private int mana;

    public string? Name { get; set; }
    public int Level { get; set; }
    public Inventory Inventory { get; set; } = new();

    public PrivatePlayer(int hp, int mana)
    {
        this.hp = hp;
        this.mana = mana;
    }
}

public class PrivateGuild
{
    private string guildName;
    private int level;
    private PrivatePlayer master;
    private Inventory storage;

    public PrivateGuild(string guildName, int level, PrivatePlayer master, Inventory storage)
    {
        this.guildName = guildName;
        this.level = level;
        this.master = master;
        this.storage = storage;
    }
}

public class PrivateEnemy
{
    public int hp = 50;
    public string internalId = "e_001";
    private float speed = 3.5f;
    public static int enemyCount = 3;
}

// ---------- 샘플 ----------

internal static class WorkloadSamples
{
    // Small : Player → Inventory, Items 3개 / Large : Guild → Player → Inventory, Items 100개씩
    public static object PublicSmall() => FillSmall(new PublicPlayer { hp = 100, mana = 50, Name = "Hero", Level = 5 }, p => p.Inventory);

    public static object PublicLarge() => new PublicGuild
    {
        guildName = "Dragons",
        level = 10,
        master = FillLarge(new PublicPlayer { hp = 999, mana = 500, Name = "GuildMaster", Level = 60 }, p => p.Inventory),
        storage = Storage(),
    };

    public static object PrivateSmall() => FillSmall(new PrivatePlayer(100, 50) { Name = "Hero", Level = 5 }, p => p.Inventory);

    public static object PrivateLarge() => new PrivateGuild(
        "Dragons", 10, FillLarge(new PrivatePlayer(999, 500) { Name = "GuildMaster", Level = 60 }, p => p.Inventory), Storage());

    private static T FillSmall<T>(T player, Func<T, Inventory> inventory)
    {
        inventory(player).Gold = 5;
        inventory(player).Items.AddRange(["Sword", "Shield", "Potion"]);
        return player;
    }

    private static T FillLarge<T>(T player, Func<T, Inventory> inventory)
    {
        inventory(player).Gold = 123456;
        inventory(player).Items.AddRange(Enumerable.Range(0, 100).Select(i => "Item" + i));
        return player;
    }

    private static Inventory Storage()
        => new() { Gold = 9999999, Items = Enumerable.Range(0, 100).Select(i => "Stored" + i).ToList() };
}
