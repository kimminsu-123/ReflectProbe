using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using FastMember;
using ReflectProbe.Internal;

namespace ReflectProbe.Benchmarks;

// 14주 시나리오 1 : 히트 경로 get / set. 접근자는 모두 미리 만들어 두고 호출 1회의 비용만 잰다.
// FastMember 가 접근할 수 있도록 public 멤버만 쓴다. 이름 기반 API 는 값 타입을 박싱하므로 int / string 을 나눠 잰다.
// Fallback_* : IL2CPP · NativeAOT 폴백(ReflectionProbeBuilder)을 CoreCLR 에서 강제한 것. AOT 런타임의 수치가 아니다.
// 실행 : dotnet run -c Release -- --filter *HitGet* *HitSet*

public class HitModel
{
    public int IntField = 100;
    public string StringField = "hero";

    public int IntProperty { get; set; } = 50;
    public string StringProperty { get; set; } = "mage";
}

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[BaselineColumn]
[Config(typeof(ZeroAsBytesConfig))]
public abstract class HitPathBase
{
    protected readonly HitModel _model = new();
    protected readonly TypeAccessor _accessor = TypeAccessor.Create(typeof(HitModel));
    protected readonly TypeProbe<HitModel> _probe = TypeProbe.Of<HitModel>();

    // internal 생성자로 폴백 빌더를 직접 넣는다 (프로세스 모드는 Emit 그대로)
    private protected readonly TypeProbe<HitModel> _fallback = new(ProbeScope.All, new ReflectionProbeBuilder());

    protected readonly FieldInfo _intFieldInfo = typeof(HitModel).GetField(nameof(HitModel.IntField))!;
    protected readonly FieldInfo _stringFieldInfo = typeof(HitModel).GetField(nameof(HitModel.StringField))!;
    protected readonly PropertyInfo _intPropertyInfo = typeof(HitModel).GetProperty(nameof(HitModel.IntProperty))!;
    protected readonly PropertyInfo _stringPropertyInfo = typeof(HitModel).GetProperty(nameof(HitModel.StringProperty))!;

    protected int _intFieldIndex;
    protected int _stringFieldIndex;
    protected int _intPropertyIndex;
    protected int _stringPropertyIndex;

    protected void SetupIndexes()
    {
        _intFieldIndex = _probe.GetIndex(nameof(HitModel.IntField));
        _stringFieldIndex = _probe.GetIndex(nameof(HitModel.StringField));
        _intPropertyIndex = _probe.GetIndex(nameof(HitModel.IntProperty));
        _stringPropertyIndex = _probe.GetIndex(nameof(HitModel.StringProperty));
    }
}

public class HitGetBenchmarks : HitPathBase
{
    private Getter<HitModel, int> _intFieldGetter = null!;
    private Getter<HitModel, int> _intFieldFallback = null!;
    private Getter<HitModel, string> _stringFieldGetter = null!;
    private Getter<HitModel, string> _stringFieldFallback = null!;
    private Getter<HitModel, int> _intPropertyGetter = null!;
    private Getter<HitModel, int> _intPropertyFallback = null!;
    private Getter<HitModel, string> _stringPropertyGetter = null!;
    private Getter<HitModel, string> _stringPropertyFallback = null!;

    [GlobalSetup]
    public void Setup()
    {
        SetupIndexes();
        _intFieldGetter = _probe.Getter<int>(nameof(HitModel.IntField));
        _intFieldFallback = _fallback.Getter<int>(nameof(HitModel.IntField));
        _stringFieldGetter = _probe.Getter<string>(nameof(HitModel.StringField));
        _stringFieldFallback = _fallback.Getter<string>(nameof(HitModel.StringField));
        _intPropertyGetter = _probe.Getter<int>(nameof(HitModel.IntProperty));
        _intPropertyFallback = _fallback.Getter<int>(nameof(HitModel.IntProperty));
        _stringPropertyGetter = _probe.Getter<string>(nameof(HitModel.StringProperty));
        _stringPropertyFallback = _fallback.Getter<string>(nameof(HitModel.StringProperty));
    }

    // ---------------- IntField ----------------

    [Benchmark(Baseline = true), BenchmarkCategory("IntField")]
    public int IntField_Direct() => _model.IntField;

    [Benchmark, BenchmarkCategory("IntField")]
    public int IntField_Reflection() => (int)_intFieldInfo.GetValue(_model)!;

    [Benchmark, BenchmarkCategory("IntField")]
    public int IntField_FastMember() => (int)_accessor[_model, nameof(HitModel.IntField)];

    [Benchmark, BenchmarkCategory("IntField")]
    public int IntField_Probe_Name() => (int)_probe[_model, nameof(HitModel.IntField)]!;

    [Benchmark, BenchmarkCategory("IntField")]
    public int IntField_Probe_Index() => (int)_probe.GetValue(_model, _intFieldIndex)!;

    [Benchmark, BenchmarkCategory("IntField")]
    public int IntField_Probe_Typed() => _intFieldGetter(_model);

    [Benchmark, BenchmarkCategory("IntField")]
    public int IntField_Fallback_Name() => (int)_fallback[_model, nameof(HitModel.IntField)]!;

    [Benchmark, BenchmarkCategory("IntField")]
    public int IntField_Fallback_Typed() => _intFieldFallback(_model);

    // ---------------- StringField ----------------

    [Benchmark(Baseline = true), BenchmarkCategory("StringField")]
    public string StringField_Direct() => _model.StringField;

    [Benchmark, BenchmarkCategory("StringField")]
    public string StringField_Reflection() => (string)_stringFieldInfo.GetValue(_model)!;

    [Benchmark, BenchmarkCategory("StringField")]
    public string StringField_FastMember() => (string)_accessor[_model, nameof(HitModel.StringField)];

    [Benchmark, BenchmarkCategory("StringField")]
    public string StringField_Probe_Name() => (string)_probe[_model, nameof(HitModel.StringField)]!;

    [Benchmark, BenchmarkCategory("StringField")]
    public string StringField_Probe_Index() => (string)_probe.GetValue(_model, _stringFieldIndex)!;

    [Benchmark, BenchmarkCategory("StringField")]
    public string StringField_Probe_Typed() => _stringFieldGetter(_model);

    [Benchmark, BenchmarkCategory("StringField")]
    public string StringField_Fallback_Name() => (string)_fallback[_model, nameof(HitModel.StringField)]!;

    [Benchmark, BenchmarkCategory("StringField")]
    public string StringField_Fallback_Typed() => _stringFieldFallback(_model);

    // ---------------- IntProperty ----------------

    [Benchmark(Baseline = true), BenchmarkCategory("IntProperty")]
    public int IntProperty_Direct() => _model.IntProperty;

    [Benchmark, BenchmarkCategory("IntProperty")]
    public int IntProperty_Reflection() => (int)_intPropertyInfo.GetValue(_model)!;

    [Benchmark, BenchmarkCategory("IntProperty")]
    public int IntProperty_FastMember() => (int)_accessor[_model, nameof(HitModel.IntProperty)];

    [Benchmark, BenchmarkCategory("IntProperty")]
    public int IntProperty_Probe_Name() => (int)_probe[_model, nameof(HitModel.IntProperty)]!;

    [Benchmark, BenchmarkCategory("IntProperty")]
    public int IntProperty_Probe_Index() => (int)_probe.GetValue(_model, _intPropertyIndex)!;

    [Benchmark, BenchmarkCategory("IntProperty")]
    public int IntProperty_Probe_Typed() => _intPropertyGetter(_model);

    [Benchmark, BenchmarkCategory("IntProperty")]
    public int IntProperty_Fallback_Name() => (int)_fallback[_model, nameof(HitModel.IntProperty)]!;

    [Benchmark, BenchmarkCategory("IntProperty")]
    public int IntProperty_Fallback_Typed() => _intPropertyFallback(_model);

    // ---------------- StringProperty ----------------

    [Benchmark(Baseline = true), BenchmarkCategory("StringProperty")]
    public string StringProperty_Direct() => _model.StringProperty;

    [Benchmark, BenchmarkCategory("StringProperty")]
    public string StringProperty_Reflection() => (string)_stringPropertyInfo.GetValue(_model)!;

    [Benchmark, BenchmarkCategory("StringProperty")]
    public string StringProperty_FastMember() => (string)_accessor[_model, nameof(HitModel.StringProperty)];

    [Benchmark, BenchmarkCategory("StringProperty")]
    public string StringProperty_Probe_Name() => (string)_probe[_model, nameof(HitModel.StringProperty)]!;

    [Benchmark, BenchmarkCategory("StringProperty")]
    public string StringProperty_Probe_Index() => (string)_probe.GetValue(_model, _stringPropertyIndex)!;

    [Benchmark, BenchmarkCategory("StringProperty")]
    public string StringProperty_Probe_Typed() => _stringPropertyGetter(_model);

    [Benchmark, BenchmarkCategory("StringProperty")]
    public string StringProperty_Fallback_Name() => (string)_fallback[_model, nameof(HitModel.StringProperty)]!;

    [Benchmark, BenchmarkCategory("StringProperty")]
    public string StringProperty_Fallback_Typed() => _stringPropertyFallback(_model);
}

public class HitSetBenchmarks : HitPathBase
{
    private Setter<HitModel, int> _intFieldSetter = null!;
    private Setter<HitModel, int> _intFieldFallback = null!;
    private Setter<HitModel, string> _stringFieldSetter = null!;
    private Setter<HitModel, string> _stringFieldFallback = null!;
    private Setter<HitModel, int> _intPropertySetter = null!;
    private Setter<HitModel, int> _intPropertyFallback = null!;
    private Setter<HitModel, string> _stringPropertySetter = null!;
    private Setter<HitModel, string> _stringPropertyFallback = null!;

    [GlobalSetup]
    public void Setup()
    {
        SetupIndexes();
        _intFieldSetter = _probe.Setter<int>(nameof(HitModel.IntField));
        _intFieldFallback = _fallback.Setter<int>(nameof(HitModel.IntField));
        _stringFieldSetter = _probe.Setter<string>(nameof(HitModel.StringField));
        _stringFieldFallback = _fallback.Setter<string>(nameof(HitModel.StringField));
        _intPropertySetter = _probe.Setter<int>(nameof(HitModel.IntProperty));
        _intPropertyFallback = _fallback.Setter<int>(nameof(HitModel.IntProperty));
        _stringPropertySetter = _probe.Setter<string>(nameof(HitModel.StringProperty));
        _stringPropertyFallback = _fallback.Setter<string>(nameof(HitModel.StringProperty));
    }

    // ---------------- IntField ----------------

    [Benchmark(Baseline = true), BenchmarkCategory("IntField")]
    public void IntField_Direct() => _model.IntField = 30;

    [Benchmark, BenchmarkCategory("IntField")]
    public void IntField_Reflection() => _intFieldInfo.SetValue(_model, 30);

    [Benchmark, BenchmarkCategory("IntField")]
    public void IntField_FastMember() => _accessor[_model, nameof(HitModel.IntField)] = 30;

    [Benchmark, BenchmarkCategory("IntField")]
    public void IntField_Probe_Name() => _probe[_model, nameof(HitModel.IntField)] = 30;

    [Benchmark, BenchmarkCategory("IntField")]
    public void IntField_Probe_Index() => _probe.SetValue(_model, _intFieldIndex, 30);

    [Benchmark, BenchmarkCategory("IntField")]
    public void IntField_Probe_Typed() => _intFieldSetter(_model, 30);

    [Benchmark, BenchmarkCategory("IntField")]
    public void IntField_Fallback_Name() => _fallback[_model, nameof(HitModel.IntField)] = 30;

    [Benchmark, BenchmarkCategory("IntField")]
    public void IntField_Fallback_Typed() => _intFieldFallback(_model, 30);

    // ---------------- StringField ----------------

    [Benchmark(Baseline = true), BenchmarkCategory("StringField")]
    public void StringField_Direct() => _model.StringField = "rogue";

    [Benchmark, BenchmarkCategory("StringField")]
    public void StringField_Reflection() => _stringFieldInfo.SetValue(_model, "rogue");

    [Benchmark, BenchmarkCategory("StringField")]
    public void StringField_FastMember() => _accessor[_model, nameof(HitModel.StringField)] = "rogue";

    [Benchmark, BenchmarkCategory("StringField")]
    public void StringField_Probe_Name() => _probe[_model, nameof(HitModel.StringField)] = "rogue";

    [Benchmark, BenchmarkCategory("StringField")]
    public void StringField_Probe_Index() => _probe.SetValue(_model, _stringFieldIndex, "rogue");

    [Benchmark, BenchmarkCategory("StringField")]
    public void StringField_Probe_Typed() => _stringFieldSetter(_model, "rogue");

    [Benchmark, BenchmarkCategory("StringField")]
    public void StringField_Fallback_Name() => _fallback[_model, nameof(HitModel.StringField)] = "rogue";

    [Benchmark, BenchmarkCategory("StringField")]
    public void StringField_Fallback_Typed() => _stringFieldFallback(_model, "rogue");

    // ---------------- IntProperty ----------------

    [Benchmark(Baseline = true), BenchmarkCategory("IntProperty")]
    public void IntProperty_Direct() => _model.IntProperty = 30;

    [Benchmark, BenchmarkCategory("IntProperty")]
    public void IntProperty_Reflection() => _intPropertyInfo.SetValue(_model, 30);

    [Benchmark, BenchmarkCategory("IntProperty")]
    public void IntProperty_FastMember() => _accessor[_model, nameof(HitModel.IntProperty)] = 30;

    [Benchmark, BenchmarkCategory("IntProperty")]
    public void IntProperty_Probe_Name() => _probe[_model, nameof(HitModel.IntProperty)] = 30;

    [Benchmark, BenchmarkCategory("IntProperty")]
    public void IntProperty_Probe_Index() => _probe.SetValue(_model, _intPropertyIndex, 30);

    [Benchmark, BenchmarkCategory("IntProperty")]
    public void IntProperty_Probe_Typed() => _intPropertySetter(_model, 30);

    [Benchmark, BenchmarkCategory("IntProperty")]
    public void IntProperty_Fallback_Name() => _fallback[_model, nameof(HitModel.IntProperty)] = 30;

    [Benchmark, BenchmarkCategory("IntProperty")]
    public void IntProperty_Fallback_Typed() => _intPropertyFallback(_model, 30);

    // ---------------- StringProperty ----------------

    [Benchmark(Baseline = true), BenchmarkCategory("StringProperty")]
    public void StringProperty_Direct() => _model.StringProperty = "rogue";

    [Benchmark, BenchmarkCategory("StringProperty")]
    public void StringProperty_Reflection() => _stringPropertyInfo.SetValue(_model, "rogue");

    [Benchmark, BenchmarkCategory("StringProperty")]
    public void StringProperty_FastMember() => _accessor[_model, nameof(HitModel.StringProperty)] = "rogue";

    [Benchmark, BenchmarkCategory("StringProperty")]
    public void StringProperty_Probe_Name() => _probe[_model, nameof(HitModel.StringProperty)] = "rogue";

    [Benchmark, BenchmarkCategory("StringProperty")]
    public void StringProperty_Probe_Index() => _probe.SetValue(_model, _stringPropertyIndex, "rogue");

    [Benchmark, BenchmarkCategory("StringProperty")]
    public void StringProperty_Probe_Typed() => _stringPropertySetter(_model, "rogue");

    [Benchmark, BenchmarkCategory("StringProperty")]
    public void StringProperty_Fallback_Name() => _fallback[_model, nameof(HitModel.StringProperty)] = "rogue";

    [Benchmark, BenchmarkCategory("StringProperty")]
    public void StringProperty_Fallback_Typed() => _stringPropertyFallback(_model, "rogue");
}
