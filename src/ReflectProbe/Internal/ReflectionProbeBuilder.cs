using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace ReflectProbe.Internal
{
    /// <summary>
    /// 동적 코드 생성이 불가능한 환경(IL2CPP, NativeAOT)용 폴백.
    /// 멤버마다 FieldInfo / MethodInfo 를 붙잡은 델리게이트를 인덱스 배열로 캐싱해 GetValue / SetValue / Invoke 로 접근한다.
    /// 강타입 델리게이트도 내부적으로 박싱이 남는다.
    ///
    /// 공개 API 의 동작은 Emit 구현과 같게 맞춘다 (같은 테스트 세트를 두 모드로 실행).
    /// - IL 의 castclass / unbox.any 가 내는 예외를 그대로 낸다 : null 대상 · 값 타입에 null → NullReferenceException,
    ///   타입 불일치 → InvalidCastException. (Reflection API 는 TargetException / ArgumentException 을 내거나 null 을 default 로 바꾼다)
    /// - 호출된 코드의 예외는 TargetInvocationException 을 벗겨 원래 예외로 다시 던진다.
    /// - 값 타입 대상은 박스 안의 값을 수정한다. ref T 대상은 박스 사본에 쓴 뒤 원본에 되돌려 쓴다.
    /// </summary>
    internal sealed class ReflectionProbeBuilder : ProbeBuilder
    {
        private static readonly MethodInfo _factoryInvoke = typeof(ProbeFactory).GetMethod(nameof(ProbeFactory.Invoke))!;
        private static readonly MethodInfo _invokerInvoke = typeof(ProbeInvoker).GetMethod(nameof(ProbeInvoker.Invoke))!;

        public override ProbeMode Mode => ProbeMode.Reflection;

        // ---------- 필드 / 프로퍼티 ----------

        public override Func<int, object?, object?> BuildGetter(Type type, ProbeMember[] members)
        {
            var getters = new Func<object?, object?>[members.Length];
            for (int i = 0; i < members.Length; i++)
                getters[i] = CreateGetter(type, members[i]);

            return (index, target) =>
            {
                if ((uint)index >= (uint)getters.Length)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return getters[index](target);
            };
        }

        public override Action<int, object?, object?> BuildSetter(Type type, ProbeMember[] members)
        {
            var setters = new Action<object?, object?>[members.Length];
            for (int i = 0; i < members.Length; i++)
                setters[i] = CreateSetter(type, members[i]);

            return (index, target, value) =>
            {
                if ((uint)index >= (uint)setters.Length)
                    throw new ArgumentOutOfRangeException(nameof(index));
                setters[index](target, value);
            };
        }

        public override Getter<T, TValue> BuildGetter<T, TValue>(ProbeMember member)
        {
            EnsureGettable<TValue>(member);

            Func<object?, object?> get = CreateGetter(member.DeclaringType, member);
            return target => (TValue)get(target)!;     // unbox.any / castclass 와 같은 변환
        }

        public override Setter<T, TValue> BuildSetter<T, TValue>(ProbeMember member)
        {
            EnsureSettableByValue<T, TValue>(member);

            Action<object?, object?> set = CreateSetter(member.DeclaringType, member);
            return (target, value) => set(target, value);
        }

        public override RefSetter<T, TValue> BuildRefSetter<T, TValue>(ProbeMember member)
        {
            EnsureSettable<TValue>(member);

            Action<object?, object?> set = CreateSetter(member.DeclaringType, member);
            if (!typeof(T).IsValueType || member.IsStatic)
                return (ref T target, TValue value) => set(target, value);

            // struct 원본 : 박스 사본에 쓰고 되돌려 쓴다.
            return (ref T target, TValue value) =>
            {
                object boxed = target!;
                set(boxed, value);
                target = (T)boxed;
            };
        }

        // ---------- 생성자 ----------

        public override Func<T> BuildDefaultFactory<T>(ConstructorInfo? constructor)
        {
            if (constructor == null)
                return () => default!;                  // 생성자 없는 struct : initobj 와 같음

            return () => (T)InvokeUnwrapped(constructor, null, Array.Empty<object>())!;
        }

        public override ProbeFactory BuildFactory(ConstructorInfo constructor)
        {
            Type[] parameterTypes = ParameterTypes(constructor);
            return args => InvokeUnwrapped(constructor, null, ConvertArguments(args, parameterTypes))!;
        }

        // ---------- 메서드 ----------

        public override ProbeInvoker BuildInvoker(MethodInfo method)
        {
            Type owner = method.DeclaringType!;
            Type[] parameterTypes = ParameterTypes(method);

            if (method.IsStatic)
                return (target, args) => InvokeUnwrapped(method, null, ConvertArguments(args, parameterTypes));

            // 값 타입 대상은 박스 그대로 넘긴다 → 값을 바꾸는 메서드는 박스 원본을 수정한다 (Emit 의 unbox 주소 호출과 같음).
            return (target, args) => InvokeUnwrapped(method, ConvertTarget(target, owner), ConvertArguments(args, parameterTypes));
        }

        // ---------- 강타입 생성자 · 메서드 ----------
        // 델리게이트 타입이 사용자 지정이라 C# 람다로 만들 수 없다. 식 트리를 인터프리터 모드로 컴파일해
        // "인자 → object[] → object 기반 팩토리 · Invoker → 반환 변환" 을 감싼다. 인터프리터는 IL 을 생성하지 않는다.
        // ref 매개변수가 있는 델리게이트는 만들지 않는다 (결정 34).

        public override Delegate BuildTypedFactory(ConstructorInfo constructor, Type delegateType)
        {
            ThrowIfByRefDelegate(delegateType);
            ProbeFactory factory = BuildFactory(constructor);
            ParameterExpression[] parameters = CreateParameters(delegateType);

            Expression body = Expression.Convert(
                Expression.Call(Expression.Constant(factory), _factoryInvoke, PackArguments(parameters, 0)),
                constructor.DeclaringType!);
            return Expression.Lambda(delegateType, body, parameters).Compile(preferInterpretation: true);
        }

        public override Delegate BuildTypedInvoker(MethodInfo method, Type delegateType)
        {
            ThrowIfByRefDelegate(delegateType);
            ProbeInvoker invoker = BuildInvoker(method);
            ParameterExpression[] parameters = CreateParameters(delegateType);

            // static : invoker(null, args) / 인스턴스 : invoker((object)arg0, args[1..]) — class 대상, 또는 struct 사본(T)
            Expression target = method.IsStatic ? Expression.Constant(null, typeof(object)) : Expression.Convert(parameters[0], typeof(object));
            Expression call = Expression.Call(Expression.Constant(invoker), _invokerInvoke, target, PackArguments(parameters, method.IsStatic ? 0 : 1));
            return Expression.Lambda(delegateType, ConvertResult(call, method.ReturnType), parameters).Compile(preferInterpretation: true);
        }

        /// <summary>
        /// 결정 34 : Unity IL2CPP 의 식 트리 인터프리터는 ref 매개변수 델리게이트를 만들지 못한다 (NotImplementedException "byref delegate",
        /// 13주 WebGL 실측). CoreCLR 인터프리터는 되지만 런타임마다 동작이 갈리지 않도록 폴백에서는 항상 막는다.
        /// </summary>
        private static void ThrowIfByRefDelegate(Type delegateType)
        {
            foreach (Type parameterType in DelegateSignature.ParameterTypes(delegateType))
            {
                if (parameterType.IsByRef)
                    throw new NotSupportedException(
                        $"Reflection 폴백 모드에서는 ref 매개변수가 있는 델리게이트 '{delegateType}' 를 만들 수 없습니다. " +
                        "대상을 T(사본) 로 받는 델리게이트, 또는 박스 원본을 수정하는 object 기반 GetInvoker(name, types) 를 쓰세요.");
            }
        }

        // ---------- 공용 ----------

        private static Func<object?, object?> CreateGetter(Type type, ProbeMember member)
        {
            string name = member.Name;
            if (!member.CanRead)
                return _ => throw new MemberNotReadableException(type, name);

            Type owner = member.DeclaringType;
            if (member.Info is FieldInfo field)
            {
                if (member.IsStatic)
                    return _ => field.GetValue(null);
                return target => field.GetValue(ConvertTarget(target, owner));
            }

            MethodInfo getMethod = member.GetMethod!;
            if (member.IsStatic)
                return _ => InvokeUnwrapped(getMethod, null, null);
            return target => InvokeUnwrapped(getMethod, ConvertTarget(target, owner), null);
        }

        private static Action<object?, object?> CreateSetter(Type type, ProbeMember member)
        {
            string name = member.Name;
            if (!member.CanWrite)
                return (_, _) => throw new MemberNotWritableException(type, name);

            Type owner = member.DeclaringType;
            Type valueType = member.Type;
            if (member.Info is FieldInfo field)
            {
                if (member.IsStatic)
                    return (_, value) => field.SetValue(null, ConvertValue(value, valueType));
                return (target, value) => field.SetValue(ConvertTarget(target, owner), ConvertValue(value, valueType));
            }

            MethodInfo setMethod = member.SetMethod!;
            if (member.IsStatic)
                return (_, value) => InvokeUnwrapped(setMethod, null, new[] { ConvertValue(value, valueType) });
            return (target, value) => InvokeUnwrapped(setMethod, ConvertTarget(target, owner), new[] { ConvertValue(value, valueType) });
        }

        /// <summary>IL 의 castclass(참조 타입) · unbox(값 타입) 대상 변환과 같은 예외를 낸다. 값 타입은 박스를 그대로 돌려준다.</summary>
        private static object ConvertTarget(object? target, Type declaringType)
        {
            if (target is null)
                throw new NullReferenceException();
            if (!declaringType.IsInstanceOfType(target))
                throw new InvalidCastException($"'{target.GetType()}' 을(를) '{declaringType}' 로 변환할 수 없습니다.");
            return target;
        }

        /// <summary>IL 의 unbox.any(값 타입) · castclass(참조 타입) 와 같은 규칙. 열거형 ↔ 기본 정수 타입은 unbox 처럼 허용한다.</summary>
        private static object? ConvertValue(object? value, Type type)
        {
            if (value is null)
            {
                if (type.IsValueType && Nullable.GetUnderlyingType(type) == null)
                    throw new NullReferenceException();
                return null;
            }

            if (type.IsInstanceOfType(value))
                return value;

            Type target = Nullable.GetUnderlyingType(type) ?? type;
            if (target.IsValueType && UnderlyingType(target) == UnderlyingType(value.GetType()))
                return target.IsEnum ? Enum.ToObject(target, value) : Convert.ChangeType(value, target);

            throw new InvalidCastException($"'{value.GetType()}' 을(를) '{type}' 로 변환할 수 없습니다.");
        }

        private static Type UnderlyingType(Type type) => type.IsEnum ? Enum.GetUnderlyingType(type) : type;

        /// <summary>args[i] 를 매개변수 타입으로 바꾼 새 배열. 개수가 모자라면 IndexOutOfRangeException (Emit 의 ldelem 과 같음), 남는 인자는 무시.</summary>
        private static object?[] ConvertArguments(object?[] args, Type[] parameterTypes)
        {
            if (parameterTypes.Length == 0)
                return Array.Empty<object>();

            var converted = new object?[parameterTypes.Length];
            for (int i = 0; i < parameterTypes.Length; i++)
                converted[i] = ConvertValue(args[i], parameterTypes[i]);
            return converted;
        }

        private static object? InvokeUnwrapped(MethodBase method, object? target, object?[]? args)
        {
            try
            {
                return method is ConstructorInfo constructor ? constructor.Invoke(args) : method.Invoke(target, args);
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                throw;
            }
        }

        private static Type[] ParameterTypes(MethodBase method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            var types = new Type[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
                types[i] = parameters[i].ParameterType;
            return types;
        }

        /// <summary>델리게이트 매개변수. ref T 는 ByRef 매개변수(Type = T, IsByRef = true)가 된다.</summary>
        private static ParameterExpression[] CreateParameters(Type delegateType)
        {
            Type[] types = DelegateSignature.ParameterTypes(delegateType);
            var parameters = new ParameterExpression[types.Length];
            for (int i = 0; i < types.Length; i++)
                parameters[i] = Expression.Parameter(types[i], "arg" + i);
            return parameters;
        }

        /// <summary>parameters[start..] → new object[] { (object)p, ... }</summary>
        private static Expression PackArguments(ParameterExpression[] parameters, int start)
        {
            var items = new Expression[parameters.Length - start];
            for (int i = start; i < parameters.Length; i++)
                items[i - start] = Expression.Convert(parameters[i], typeof(object));
            return Expression.NewArrayInit(typeof(object), items);
        }

        /// <summary>object 반환값 → 델리게이트 반환 타입. void 는 결과를 버린다.</summary>
        private static Expression ConvertResult(Expression result, Type returnType)
        {
            if (returnType == typeof(void))
                return Expression.Block(typeof(void), result);
            return Expression.Convert(result, returnType);
        }
    }
}
