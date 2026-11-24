# ReflectProbe

IL Emit 기반 Reflection 가속 라이브러리. 런타임에 알게 된 이름으로 필드·프로퍼티를 읽고 쓰고, 생성자와 메서드를 호출한다.

> 🚧 개발 중 (2026 게임프로젝트연구). 설계는 [docs/design.md](docs/design.md) 참고.

## 사용법

```csharp
var probe = TypeProbe.Of<Player>();           // (타입, 범위)마다 1개만 만들어 재사용

// 이름 기반 (private 필드 포함). 값 타입은 박싱된다
probe[player, "_hp"] = 30;
object? hp = probe[player, "_hp"];
object? parentTag = probe[player, "_tag", 1]; // 이름이 같은 부모 멤버 (1 = 직속 부모)

// 인덱스 기반 : 이름 검색을 한 번만 한다
int index = probe.GetIndex("_hp");
probe.SetValue(player, index, 50);

// 강타입 : 박싱 없음. 반환된 델리게이트를 보관해서 쓴다
Getter<Player, int> getHp = probe.Getter<int>("_hp");
Setter<Player, int> setHp = probe.Setter<int>("_hp");

// 생성자 · 메서드
var create = probe.GetFactory<Func<int, int, Player>>();
var attack = probe.GetInvoker<Func<Player, int, int>>("Attack");
object? result = probe.Invoke(player, "Attack", 2);

// struct 는 RefSetter 로 원본을 바꾼다 (Setter 는 NotSupportedException)
RefSetter<Point, int> setX = TypeProbe.Of<Point>().RefSetter<int>("X");
setX(ref point, 5);

// 대상 1개에 묶어서 쓰기
var view = new ObjectProbe(player);
view["_hp"] = 10;
```

- `TValue` 는 멤버 타입과 같거나 참조 변환 · 박싱이 되는 타입이어야 한다. `int` 멤버에 `Getter<float>` · `Getter<string>` 을 요청하면 만들 때 `InvalidCastException`.
- static 멤버는 target 을 무시한다 (`null` 가능).

## 접근 범위

`TypeProbe.Of<T>(ProbeScope)` 로 정한다. 기본값은 public · non-public · instance · static 전부와 부모의 private 멤버까지다.
인덱서, `const`, 컴파일러가 만든 백킹 필드, ref · 포인터 · 함수 포인터 · ref struct 타입 멤버는 제외한다.
인터페이스의 static abstract · static virtual 멤버도 제외한다 (타입 인수 없이는 호출할 수 없다).

인터페이스를 대상으로 하면 상속한 인터페이스의 멤버까지 수집한다. 상속 관계가 없는 두 부모가 같은 이름의 멤버를 가지면
이름으로는 `AmbiguousMemberException`, 인덱스로는 둘 다 접근할 수 있다.

`Nullable<T>` 자체(`TypeProbe.Of<int?>()`)는 대상이 될 수 없다. `int?` 타입의 멤버는 지원한다.

## AOT 환경

동적 코드 생성이 안 되는 환경(IL2CPP, NativeAOT)에서는 Reflection 폴백으로 자동 전환한다. `TypeProbe.Mode` 로 확인한다.

| | Emit (CoreCLR · Mono) | Reflection 폴백 (IL2CPP 등) |
|---|---|---|
| 이름 · 인덱스 기반 | IL switch | `FieldInfo` · `MethodInfo` 호출 |
| 강타입 Getter / Setter | 박싱 없음 | 내부에서 박싱 |
| 강타입 Factory / Invoker | 전용 IL | 식 트리 인터프리터 |
| ref · 포인터 · ref struct 매개변수 델리게이트 | 지원 | `NotSupportedException` (대상을 `T` 로 받거나 object 기반 `GetInvoker` 사용) |
| 예외 종류 | 동일 (위 제한 제외) | 동일 (위 제한 제외) |

Unity WebGL(IL2CPP) 과 Unity Mono 에디터에서 확인했다. NativeAOT 는 확인하지 않았다.

### Unity Managed Stripping

Stripping Level 을 High 로 올리면 이름으로만 쓰는 멤버가 제거될 수 있다. `[Preserve]` 를 붙이거나 `Assets/link.xml` 에 적는다.

```xml
<linker>
  <assembly fullname="Assembly-CSharp">
    <type fullname="Player" preserve="all" />
  </assembly>
</linker>
```

## 구성

| 경로 | 내용 |
|---|---|
| `src/ReflectProbe` | 라이브러리 (`netstandard2.1;net8.0`) |
| `tests/ReflectProbe.Tests` | xUnit 테스트 (Emit) |
| `tests/ReflectProbe.Tests.Reflection` | 같은 테스트를 Reflection 폴백으로 실행 |
| `tests/run-matrix.ps1` | net8.0 · netstandard2.1 × Emit · Reflection 4회 실행 |
| `benchmarks/ReflectProbe.Benchmarks` | Reflection · FastMember · ReflectProbe 비교 (BenchmarkDotNet) |

## 벤치마크

```bash
cd benchmarks/ReflectProbe.Benchmarks
dotnet run -c Release -- --filter "*Hit*"        # 히트 경로
dotnet run -c Release -- --filter "*Creation*"   # 최초 생성 비용
dotnet run -c Release -- --filter "*Dump*" "*Watch*"  # 실제 워크로드
dotnet run -c Release -- concurrency             # 동시 최초 생성의 중복 · 예외 확인
```

## 라이선스

MIT
