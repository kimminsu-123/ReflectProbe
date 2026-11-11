# ReflectProbe 설계

> FastMember 1.5.0 분석에서 가져갈 점(타입 단위 1회 생성 · 이름 → 인덱스 → IL switch · 판단은 생성 시점)은 유지하고,
> 고칠 점(캐시 경쟁 조건 · 모호한 접근 범위 · private 필드 미지원 · 박싱 · 래퍼 할당)은 바꾼 Reflection 대체 라이브러리.

## 1. 설계 결정 기록

| # | 항목 | 결정 | 이유 |
|---|---|---|---|
| 1 | 이름 | 패키지·네임스페이스 `ReflectProbe`, 클래스는 Probe 계열 | FastMember 의 Accessor 이름과 구분 |
| 2 | 타깃 | `netstandard2.1;net8.0` | Unity(Mono) 참조 가능 + 최신 런타임 |
| 3 | 디스패치 | 타입당 DynamicMethod 1개 + IL `switch` | FastMember 방식 계승. 해시 1번 + 점프 1번 |
| 4 | struct 대상 쓰기 | 강타입 API 에 `RefSetter<T,TValue>` 제공 | object 로 받으면 박스 사본에 써져 원본이 안 바뀜 |
| 5 | 예외 | `ProbeException` 계층 (`MemberNotFound` / `MemberNotReadable` / `MemberNotWritable`) | FastMember 는 전부 `ArgumentOutOfRangeException("name")` |
| 6 | Invoker 오버로드 | 이름 + 매개변수 타입. 이름만 주면 오버로드 1개일 때만 | 모호성 제거 |
| 7 | ObjectProbe | `readonly struct` | 래퍼 생성 할당 0 |
| 8 | 진입점 / 범위 | `TypeProbe.Of<T>(ProbeScope)` | |
| 9 | 기본 범위 | `ProbeScope.All` (Public · NonPublic · Instance · Static) | private 접근이 이 라이브러리의 차별점 |
| 10 | 백킹 필드 | `[CompilerGenerated]` 필드 제외 | 값은 프로퍼티 이름으로 접근 |
| 11 | 부모 private 필드 | BaseType 체인까지 수집, 이름 충돌 시 자식 우선 (가려진 멤버는 `IsHidden`, 인덱스로만 접근) | `GetFields(NonPublic)` 는 부모 private 을 돌려주지 않음 |
| 12 | readonly 필드 쓰기 | 인스턴스 readonly 허용, static readonly 불가 | static readonly 는 JIT 가 상수처럼 다룰 수 있음 (공식 문서 확인 필요) |
| 13 | AOT | Reflection 폴백 (아래 4장) | IL2CPP · NativeAOT 에서도 동작은 하게 |
| 14 | 캐시 | `ConcurrentDictionary<(Type, ProbeScope), Lazy<TypeProbe>>`, `ExecutionAndPublication` | 동시 최초 생성에도 IL 생성 1회 |
| 15 | 생성 경로 | DynamicMethod 만 사용, TypeBuilder 경로 없음 | 언로드 불가 + FastMember 동적 어셈블리 초기화 경쟁의 원인 |
| 16 | const 필드 | 수집 제외 (10주) | 저장 공간이 없어 `ldsfld` 불가. 필요하면 `FieldInfo.GetRawConstantValue` |
| 17 | 잘못된 입력 | 런타임 예외 그대로 전파 (10주) | null target → `NullReferenceException`, 타입 불일치 → `InvalidCastException`. 히트 경로 검사 비용 0, FastMember 와 같음 |
| 18 | TypeProbe 생성 | 생성자가 `ProbeBuilder` 를 받음 (10주) | 4장의 "테스트에서 ReflectionProbeBuilder 직접 주입" 경로. ProbeCache 는 `ProbeBuilder.Current` 를 넘긴다 |
| 19 | override 프로퍼티 | 가장 파생된 것 1개만 수집, 빠진 접근자는 부모 선언에서 보충 (10주) | getter 만 override 하면 파생 `PropertyInfo.SetMethod` 가 null 이라 쓰기가 막히는 문제 |
| 20 | 이름 맵 위치 | `ProbeMemberSet` 이 멤버 목록의 `IsHidden` 으로 직접 생성, TypeProbe 도 이것을 사용 (10주) | 맵 이중화 방지. MemberCollector 는 목록만 반환 |
| 21 | 이름 중복 시 깊이 기반 접근 | `probe[p, "_tag", 1]` — 세 번째 인자 `depthOffset` 으로 부모 계층 지정 (11주 구현) | 숫자 인덱스(`probe[p, 5]`)는 목록 순번이 바뀌면 깨지므로, 의미 있는 깊이 오프셋으로 접근하는 API 를 별도 제공한다. `0` = 자식(기본), `1` = 직속 부모, `2` = 조부모 |
| 22 | 생성자 · 메서드 매개변수 매칭 | 타입이 정확히 일치하는 것만. `Type.GetMethod` 의 기본 Binder 를 쓰지 않고 직접 비교 (12주) | 기본 Binder 는 대입 가능한 오버로드도 골라 `Describe(object)` 가 `Describe(string)` 요청에 잡힌다. 결정 6 의 "모호성 제거"와 충돌 |
| 23 | 메서드 수집 규칙 | 필드(결정 11 · 19)와 같음 : BaseType 체인 DeclaredOnly (부모 private 포함), override 는 가장 파생된 1개, 같은 시그니처 `new` 는 자식 우선. 특수 이름(접근자 · 연산자) · 제네릭 메서드 정의 · ref/out/포인터 시그니처 제외 (12주) | 필드 API 와 같은 규칙이어야 사용자가 예측 가능 |
| 24 | 기본 생성자 | `TypeProbe<T>.CreateInstance()` 는 `Func<T>` (struct 박싱 없음). struct 는 선언된 매개변수 없는 생성자 우선, 없으면 `initobj`. 추상 · 인터페이스 · static 클래스 · 생성자 없음은 `CanCreateInstance = false`, `CreateInstance()` 는 `MemberNotFoundException(".ctor()")` (12주) | Activator 는 `MissingMethodException` 이지만 라이브러리 예외 계층(결정 5)으로 통일 |
| 25 | struct 인스턴스 메서드 | 박스 대상 지원 : `unbox` 주소로 `call` → 값을 바꾸는 메서드는 박스 원본 수정 (12주, 5장 범위 밖에서 이동) | 이름 기반 setter 와 같은 규칙. `EmitLoadTarget` 재사용이라 추가 코드 없음 |
| 26 | 생성 시점 · 캐시 | 메서드 목록 · 기본 팩토리는 처음 쓸 때 `Lazy` 로 1회. 팩토리 · Invoker 는 `ConstructorInfo` · `MethodInfo` 키 캐시, `Invoke(name)` 은 이름 키 캐시 (12주) | 필드만 쓰는 타입은 메서드 수집 비용 0. 같은 메서드를 다른 경로로 찾아도 델리게이트 1개 |
| 27 | 강타입 Factory · Invoker | `GetFactory<TDelegate>()` · `GetInvoker<TDelegate>(name)` — 델리게이트 타입이 시그니처를 정한다. 매개변수 · 반환 타입 정확 일치 (오버로드는 매개변수로 결정), 인스턴스 메서드는 첫 매개변수가 대상 : class 는 선언 · 파생 타입 또는 부모 · object (castclass), struct 는 `ref T`(원본) 또는 `T`(사본). static 과 인스턴스가 모두 맞으면 `AmbiguousMemberException` (12주) | object[] 기반 `ProbeFactory` · `ProbeInvoker` 는 인자 배열 · 박싱 · 반환 박싱이 남는다 (메서드 80 B = MethodInfo.Invoke). 11주 Getter&lt;TValue&gt; 와 같은 이유로 강타입 경로를 둔다. 아리티별 오버로드(`GetInvoker<T1, TResult>` …)는 void · 인자 수마다 메서드가 늘어나 택하지 않음 |

### 계획서 초안에서 바뀐 점

- 초안의 `EmitTypeProbe` / `ReflectionTypeProbe` (TypeProbe 파생 클래스) → **`ProbeBuilder` 전략 2개** (`EmitProbeBuilder`, `ReflectionProbeBuilder`).
  - TypeProbe 는 빌더가 만든 델리게이트만 보관한다. 히트 경로가 `TryGetValue(name)` → 델리게이트 호출 1번으로 끝나고 가상 호출 단계가 없다.
  - 모든 TypeProbe 인스턴스가 `TypeProbe<T>` 여야 `Of(typeof(T))` 와 `Of<T>()` 가 같은 객체를 돌려줄 수 있는데, 구현을 파생 클래스로 나누면 이 조건과 충돌한다.
- `AmbiguousMemberException` 추가 (결정 6 의 "오버로드 2개 이상" 경우).

## 2. 클래스 구조

```mermaid
classDiagram
    class TypeProbe {
        <<abstract>>
        +Of(Type, ProbeScope)$ TypeProbe
        +Of~T~(ProbeScope)$ TypeProbe~T~
        +Mode$ ProbeMode
        +Type Type
        +ProbeScope Scope
        +ProbeMemberSet Members
        +this[target, name] object
        +TryGetValue() / TrySetValue()
        +GetIndex() / GetValue(target, index) / SetValue()
        +CreateInstance() / GetFactory(Type[])
        +GetInvoker(name, Type[]) / Invoke()
        +GetFactory~TDelegate~() / GetInvoker~TDelegate~(name)
    }
    class TypeProbe~T~ {
        +Getter~TValue~(name) Getter~T,TValue~
        +Setter~TValue~(name) Setter~T,TValue~
        +RefSetter~TValue~(name) RefSetter~T,TValue~
        +CreateInstance() T
    }
    class ObjectProbe {
        <<readonly struct>>
        +Target
        +Probe
        +this[name]
    }
    class ProbeMemberSet { +this[int] +this[string] +TryGet() }
    class ProbeMember { Name Type Kind IsStatic IsPublic CanRead CanWrite Index IsHidden Info }
    class ProbeCache { <<internal>> +GetOrCreate() }
    class MemberCollector { <<internal>> +Collect() }
    class ProbeBuilder { <<internal abstract>> +Current$ +BuildGetter() +BuildSetter() +BuildFactory() +BuildInvoker() }
    class EmitProbeBuilder { <<internal>> }
    class ReflectionProbeBuilder { <<internal>> }
    class DynamicCodeSupport { <<internal>> +IsSupported$ }
    class ILEmitHelpers { <<internal>> }

    TypeProbe <|-- TypeProbe~T~
    ObjectProbe --> TypeProbe
    TypeProbe --> ProbeMemberSet
    ProbeMemberSet o-- ProbeMember
    TypeProbe ..> ProbeCache : Of()
    ProbeCache ..> MemberCollector
    ProbeCache ..> ProbeBuilder
    ProbeBuilder <|-- EmitProbeBuilder
    ProbeBuilder <|-- ReflectionProbeBuilder
    ProbeBuilder ..> DynamicCodeSupport : Current
    EmitProbeBuilder ..> ILEmitHelpers
```

| 파일 | 공개 | 역할 |
|---|---|---|
| `TypeProbe.cs` | public | 타입 단위 진입점, 이름·인덱스 접근, 생성자, 메서드 |
| `TypeProbe.Generic.cs` | public | 강타입 Getter / Setter / RefSetter |
| `ObjectProbe.cs` | public | 객체 1개 래퍼 (struct) |
| `ProbeMember.cs`, `ProbeMemberSet.cs` | public | 메타데이터 |
| `ProbeScope.cs`, `ProbeMode.cs` | public | 범위 플래그, 생성 방식 |
| `ProbeDelegates.cs` | public | Getter, Setter, RefSetter, ProbeFactory, ProbeInvoker |
| `ProbeExceptions.cs` | public | 예외 계층 |
| `Internal/ProbeCache.cs` | internal | 동시성 캐시 |
| `Internal/MemberCollector.cs` | internal | 멤버 수집 + 이름 맵 (단일 목록에서 생성) |
| `Internal/ProbeBuilder.cs` 외 2개 | internal | Emit / Reflection 델리게이트 생성 |
| `Internal/DynamicCodeSupport.cs` | internal | 폴백 감지 |
| `Internal/ILEmitHelpers.cs` | internal | castclass / unbox / box 변환 |

## 3. 핵심 흐름

```text
TypeProbe.Of<Player>()
  └ ProbeCache.GetOrCreate(typeof(Player), All)
      ├ 히트 : Lazy.Value 반환 (락 없음)
      └ 미스 : Lazy 1개만 실행
          ├ MemberCollector.Collect → ProbeMember[] (→ ProbeMemberSet 이 name→index 생성)
          ├ ProbeBuilder.Current.BuildGetter / BuildSetter (IL switch DynamicMethod)
          └ new TypeProbe<Player>(...)

probe[p, "hp"]
  └ name→index TryGetValue → _getter(index, p) → switch(index) → ldfld hp → box
probe.GetValue(p, index)        ← 해시 생략
probe.Getter<int>("hp")(p)      ← 멤버 전용 DynamicMethod, 박싱 없음
```

## 4. AOT 폴백

- 감지 : 프로세스당 1회. net8.0 은 `RuntimeFeature.IsDynamicCodeSupported`, netstandard2.1 은 작은 DynamicMethod 생성·호출 시도 후 `PlatformNotSupportedException` / `NotSupportedException` 이면 폴백.
- 동작 : 공개 API 동일. 내부는 캐싱한 MemberInfo 로 GetValue / SetValue / Invoke. 강타입 API 도 박싱이 남는다.
- 확인 : `TypeProbe.Mode`. 테스트는 internal 생성 경로로 `ReflectionProbeBuilder` 를 직접 넣어 같은 테스트를 두 모드로 실행 (전역 스위치를 바꾸지 않아 병렬 테스트에 안전).
- Unity IL2CPP : 코드 스트리핑으로 메타데이터가 빠질 수 있어 `link.xml` 또는 `[Preserve]` 필요.

## 5. 범위 밖

ObjectReader(IDataReader), DLR `dynamic`, TypeBuilder 경로, ref/out 매개변수, 제네릭 메서드 정의, 이름으로 연산자 · 프로퍼티 접근자 호출.

## 6. 구현 전 확인할 세부 항목

| 항목 | 현재 안 |
|---|---|
| `Getter<TValue>` 의 TValue 가 멤버 타입과 다를 때 | 정확히 일치할 때만 허용, 아니면 예외 |
| struct 대상에 `Setter<TValue>` 호출 | `NotSupportedException` (RefSetter 안내) |
| static 멤버의 강타입 Getter | target 인자를 무시 |
| 인스턴스 readonly 필드에 DynamicMethod `stfld` | 10주 확인 : 동작함 (`Indexer_InstanceReadonlyField_Writes`) |
| 박싱된 struct 를 `unbox` 주소로 쓰기 | 10주 확인 : 박스 원본이 바뀜 (`BoxedStruct_*`) |

## 7. 11주 구현 계획

### 7-1. 깊이 기반 이름 접근 (depthOffset 오버로드)

**배경**: 이름이 같은 필드가 부모-자식 계층에 모두 존재할 때, 현재는 자식이 이름 맵에 들어가고 부모는 `IsHidden=true` 로 인덱스로만 접근할 수 있다. 인덱스는 목록 순번에 의존하므로 계층이 바뀌면 깨진다.

**API (예정)**

```csharp
// 현재 (자식 필드 — depthOffset=0 과 동일)
probe[p, "_tag"]

// 11주 추가
probe[p, "_tag", 1]   // 직속 부모(Animal)의 _tag
probe[p, "_tag", 2]   // 조부모의 _tag
```

**구현 포인트**

1. `MemberCollector.Collect` 가 부모 깊이 정보를 `ProbeMember.DepthOffset` 프로퍼티로 기록한다.
   - `BaseType` 체인을 내려가면서 `DeclaredOnly`로 수집. `DepthOffset = 0`(자식), `1`(직속 부모), `2`(조부모) …
2. `ProbeMemberSet` 에 `TryGet(string name, int depthOffset, out ProbeMember)` 오버로드 추가.
   - `depthOffset == 0` 이면 기존 `_nameToIndex` 맵 사용.
   - `depthOffset >= 1` 이면 `IsHidden == true` 인 멤버 중 `Name == name && DepthOffset == depthOffset` 을 찾는다. 히트 경로가 아니므로 선형 탐색 허용.
   - **구현 시 변경 (11주)**: `IsHidden` 조건은 빼고 `Name == name && DepthOffset == depthOffset` 만 본다. 자식에 없는 부모 멤버(`Dog` 의 `_age`)는 가려지지 않아서, `IsHidden` 만 찾으면 `probe[dog, "_age", 1]` 이 실패하기 때문. 음수는 `ArgumentOutOfRangeException`, 없으면 `MemberNotFoundException`.
3. `TypeProbe` 에 인덱서 오버로드 추가.
   ```csharp
   public object? this[object? target, string name, int depthOffset]
   {
       get => _getter(GetIndex(name, depthOffset), target);
       set => _setter(GetIndex(name, depthOffset), target, value);
   }
   ```
4. `GetIndex(string name, int depthOffset)` 헬퍼를 `TypeProbe` 에 추가. `depthOffset == 0` 이면 기존 경로, 아니면 `Members.TryGet(name, depthOffset, ...)`.

**비고**: IL switch 의 인덱스 번호는 그대로 사용하므로 getter/setter DynamicMethod 변경 없음. 맵 탐색 레이어만 추가된다.

### 7-2. RefSetter (박싱 없는 struct 쓰기)

**배경**: 현재 setter 경로는 `object` 로 받아 박스된 인스턴스에 `unbox` + `stfld` 한다. 박싱되지 않은 로컬 struct 는 사본만 바뀐다.

**API (예정)**

```csharp
// 강타입 API — TypeProbe<T>
RefSetter<Dog, string> refSetTag = probe.RefSetter<string>("_tag");
refSetTag(ref dog, "bark");  // 박싱 없이 원본에 직접 씀
```

**구현 포인트**

- `RefSetter<T, TValue>` 델리게이트: `delegate void RefSetter<T, TValue>(ref T target, TValue value)` (이미 `ProbeDelegates.cs` 에 선언 예정)
- `EmitProbeBuilder.BuildRefSetter` : `unbox T` 로 주소를 꺼내고 `stfld` 로 씀. 대상이 참조 타입이면 `castclass` 후 `stfld`.
- `TypeProbe<T>.RefSetter<TValue>(string name)` 공개 API.

**비고**: getter 쪽 `unbox.any` 복사 비용은 `Getter<TValue>` 에서는 그대로 남는다. object API 를 쓰지 않는 경우에만 박싱 비용이 제거된다.
