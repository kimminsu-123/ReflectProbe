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

/// <summary>3단계 상속. _tag 가 Puppy(0) · Dog(1) · Animal(2) 에 모두 있다. depthOffset 테스트 전용.</summary>
internal class Puppy : Dog
{
    private new string _tag = "puppy";
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

// ---------- 12주 : 생성자 · 메서드 ----------

/// <summary>5주 ReflectionBenchmark 의 Enemy 와 같은 모양 + 오버로드 · private · static · void.</summary>
internal class Enemy
{
    private int _hp;
    private int _damage;
    private string _log = "";

    public Enemy() { }

    public Enemy(int hp, int damage)
    {
        _hp = hp;
        _damage = damage;
    }

    private Enemy(string log) => _log = log;

    public int Hp => _hp;
    public string Log => _log;

    public int Attack() => _damage;                                          // 오버로드 3개
    public int Attack(int multiplier) => _damage * multiplier;
    public int Attack(int multiplier, int bonus) => _damage * multiplier + bonus;

    public void TakeDamage(int amount) => _hp -= amount;                     // void
    private string Secret() => "secret:" + _hp;                              // private
    public static int Add(int a, int b) => a + b;                            // static
    public string Describe(object value) => "object:" + value;              // 정확 일치 검사용
    public string Rename(string? name) => _log = name ?? "null";             // null 인자
    public T Echo<T>(T value) => value;                                      // 제외 (제네릭 메서드 정의)
    public void TryHit(out int damage) => damage = _damage;                  // 제외 (out 매개변수)
}

internal class Creature
{
    private string Secret() => "creature-secret";                           // 부모 private
    public virtual string Speak() => "creature";
    public string Name() => "creature";
}

internal class Goblin : Creature
{
    public override string Speak() => "goblin";                              // override → 1개만
    public new string Name() => "goblin";                                    // new → 자식 우선
}

internal abstract class Shape
{
    public Shape() { }
}

internal interface IShape
{
    int Sides();
}

internal class Triangle : IShape
{
    public int Sides() => 3;
}

internal class Singleton
{
    private Singleton() { }
}

internal struct Counter
{
    public int Count;

    public Counter(int count) => Count = count;

    public void Increment() => Count++;
    public override string ToString() => "Counter(" + Count + ")";
}

/// <summary>같은 델리게이트 시그니처에 static 과 인스턴스 메서드가 모두 맞는 경우. Func&lt;Calc, int, int&gt; 는 모호하다.</summary>
internal class Calc
{
    public int Twice(int x) => x * 2;
    public static int Twice(Calc calc, int x) => x * 2;
}

/// <summary>struct 원본을 바꾸는 강타입 Invoker 용 델리게이트.</summary>
internal delegate void CounterAction(ref Counter counter);

/// <summary>매개변수 없는 생성자를 직접 선언한 struct (C# 10). initobj 가 아니라 이 생성자가 불려야 한다.</summary>
internal struct DefaultedStruct
{
    public int Value;

    public DefaultedStruct() => Value = 42;
}

#pragma warning restore CS0169, CS0414, CS0649
