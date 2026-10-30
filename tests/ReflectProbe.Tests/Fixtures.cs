namespace ReflectProbe.Tests;

// 테스트 대상 타입. 테스트 클래스끼리 병렬 실행되므로 static 상태를 바꾸는 테스트는 전용 타입을 쓴다.

#pragma warning disable CS0169, CS0414, CS0649 // 리플렉션으로만 접근하는 필드

internal class Player
{
    public const int MaxHp = 100;                     // 제외 (const)
    public static readonly int Version = 1;           // CanWrite = false
    private static int _createdCount = 0;

    private int _hp = 50;
    public string Name = "player";
    private readonly int _id = 7;                     // 인스턴스 readonly → 쓰기 가능
    private int _secret;

    public int Level { get; set; } = 1;               // 백킹 필드는 제외
    public int Score => _hp * 10;                     // get-only
    public int SecretValue { set => _secret = value; } // set-only
    public int this[int i] => i;                      // 제외 (인덱서)

    public int Hp => _hp;
    public int Id => _id;
    public int Secret => _secret;
}

internal class Animal
{
    private int _age = 3;                             // 부모 private
    protected string _tag = "animal";                 // Dog._tag 에 가려짐

    public virtual string Sound => "...";
    public virtual int Legs { get; set; } = 4;
    public virtual int Weight { get; set; } = 10;
}

internal class Dog : Animal
{
    private new string _tag = "dog";

    public override string Sound => "woof";
    public override int Legs { get; set; } = 4;
    public override int Weight => base.Weight + 1;    // getter 만 override, setter 는 Animal 선언
}

internal struct MutablePoint
{
    public int X;
    private int _y;

    public int Y
    {
        readonly get => _y;
        set => _y = value;
    }
}

internal static class GameConfig
{
    public static int MaxPlayers = 4;
    private static string Mode { get; set; } = "normal";
}

/// <summary>동시 최초 생성 테스트 전용. 다른 테스트에서 쓰지 않는다.</summary>
internal class ConcurrencyTarget
{
    private int _value;
}

#pragma warning restore CS0169, CS0414, CS0649
