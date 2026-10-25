# ReflectProbe

IL Emit 기반 Reflection 가속 라이브러리. 런타임에 알게 된 이름으로 필드·프로퍼티를 읽고 쓰고, 생성자와 메서드를 호출한다.

> 🚧 개발 중 (2026 게임프로젝트연구). 설계는 [docs/design.md](docs/design.md) 참고.

```csharp
var probe = TypeProbe.Of<Player>();
probe[p, "hp"] = 30;                    // 이름 기반 (private 필드 포함)
var getHp = probe.Getter<int>("hp");    // 강타입, 박싱 없음
```

## 구성

| 경로 | 내용 |
|---|---|
| `src/ReflectProbe` | 라이브러리 (`netstandard2.1;net8.0`) |
| `tests/ReflectProbe.Tests` | xUnit 테스트 |
| `benchmarks/ReflectProbe.Benchmarks` | Reflection · FastMember · ReflectProbe 비교 (BenchmarkDotNet) |

## 라이선스

MIT
