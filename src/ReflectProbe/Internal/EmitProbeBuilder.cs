using System;
using System.Reflection;
using System.Reflection.Emit;

namespace ReflectProbe.Internal
{
    /// <summary>
    /// DynamicMethod(owner: 대상 타입, skipVisibility: true) 로 IL 을 생성한다.
    /// TypeBuilder 경로는 쓰지 않는다. (언로드 불가 + FastMember 동적 어셈블리 초기화 경쟁의 원인)
    /// </summary>
    internal sealed class EmitProbeBuilder : ProbeBuilder
    {
        private static readonly MethodInfo _getTypeFromHandle = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!;

        private static readonly ConstructorInfo _notReadableCtor
            = typeof(MemberNotReadableException).GetConstructor(new[] { typeof(Type), typeof(string) })!;

        private static readonly ConstructorInfo _notWritableCtor
            = typeof(MemberNotWritableException).GetConstructor(new[] { typeof(Type), typeof(string) })!;

        private static readonly ConstructorInfo _outOfRangeCtor
            = typeof(ArgumentOutOfRangeException).GetConstructor(new[] { typeof(string) })!;

        public override ProbeMode Mode => ProbeMode.Emit;

        public override Func<int, object?, object?> BuildGetter(Type type, ProbeMember[] members)
        {
            DynamicMethod method = CreateMethod("ProbeGet_" + type.Name, typeof(object), new[] { typeof(int), typeof(object) }, type);
            ILGenerator il = method.GetILGenerator();
            Label[] cases = EmitSwitch(il, members.Length);

            for (int i = 0; i < members.Length; i++)
            {
                ProbeMember member = members[i];
                il.MarkLabel(cases[i]);

                if (!member.CanRead)
                {
                    EmitThrow(il, _notReadableCtor, type, member.Name);
                    continue;
                }

                if (!member.IsStatic)
                {
                    il.Emit(OpCodes.Ldarg_1);
                    ILEmitHelpers.EmitLoadTarget(il, member.DeclaringType);
                }

                if (member.Info is FieldInfo field)
                    il.Emit(member.IsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld, field);
                else
                    EmitCall(il, member.GetMethod!);

                ILEmitHelpers.EmitConvertToObject(il, member.Type);
                il.Emit(OpCodes.Ret);
            }

            return (Func<int, object?, object?>)method.CreateDelegate(typeof(Func<int, object?, object?>));
        }

        public override Action<int, object?, object?> BuildSetter(Type type, ProbeMember[] members)
        {
            DynamicMethod method = CreateMethod("ProbeSet_" + type.Name, typeof(void), new[] { typeof(int), typeof(object), typeof(object) }, type);
            ILGenerator il = method.GetILGenerator();
            Label[] cases = EmitSwitch(il, members.Length);

            for (int i = 0; i < members.Length; i++)
            {
                ProbeMember member = members[i];
                il.MarkLabel(cases[i]);

                if (!member.CanWrite)
                {
                    EmitThrow(il, _notWritableCtor, type, member.Name);
                    continue;
                }

                if (!member.IsStatic)
                {
                    il.Emit(OpCodes.Ldarg_1);
                    ILEmitHelpers.EmitLoadTarget(il, member.DeclaringType);
                }

                il.Emit(OpCodes.Ldarg_2);
                ILEmitHelpers.EmitConvertFromObject(il, member.Type);

                if (member.Info is FieldInfo field)
                    il.Emit(member.IsStatic ? OpCodes.Stsfld : OpCodes.Stfld, field);
                else
                    EmitCall(il, member.SetMethod!);

                il.Emit(OpCodes.Ret);
            }

            return (Action<int, object?, object?>)method.CreateDelegate(typeof(Action<int, object?, object?>));
        }

        public override Getter<T, TValue> BuildGetter<T, TValue>(ProbeMember member)
        {
            if (!member.CanRead)
                throw new MemberNotReadableException(member.DeclaringType, member.Name);

            Type tType = typeof(T);
            Type tvType = typeof(TValue);
            DynamicMethod method = CreateMethod(
                $"ProbeGet_{tType.Name}_{member.Name}",
                tvType,
                new[] { tType },
                member.DeclaringType);
            ILGenerator il = method.GetILGenerator();

            if (!member.IsStatic)
            {
                if (tType.IsValueType)
                    il.Emit(OpCodes.Ldarga_S, (byte)0);
                else
                {
                    il.Emit(OpCodes.Ldarg_0);
                    if (tType != member.DeclaringType)
                        il.Emit(OpCodes.Castclass, member.DeclaringType);
                }
            }

            if (member.Info is FieldInfo getterField)
                il.Emit(member.IsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld, getterField);
            else
                EmitCall(il, member.GetMethod!);

            if (tvType != member.Type)
            {
                if (member.Type.IsValueType && !tvType.IsValueType)
                    il.Emit(OpCodes.Box, member.Type);
                else if (!member.Type.IsValueType && tvType.IsValueType)
                    il.Emit(OpCodes.Unbox_Any, tvType);
                else if (!tvType.IsValueType)
                    il.Emit(OpCodes.Castclass, tvType);
            }

            il.Emit(OpCodes.Ret);
            return (Getter<T, TValue>)method.CreateDelegate(typeof(Getter<T, TValue>));
        }

        public override Setter<T, TValue> BuildSetter<T, TValue>(ProbeMember member)
        {
            if (!member.CanWrite)
                throw new MemberNotWritableException(member.DeclaringType, member.Name);

            Type tType = typeof(T);
            Type tvType = typeof(TValue);
            DynamicMethod method = CreateMethod(
                $"ProbeSet_{tType.Name}_{member.Name}",
                typeof(void),
                new[] { tType, tvType },
                member.DeclaringType);
            ILGenerator il = method.GetILGenerator();

            if (!member.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);
                if (!tType.IsValueType && tType != member.DeclaringType)
                    il.Emit(OpCodes.Castclass, member.DeclaringType);
            }

            il.Emit(OpCodes.Ldarg_1);
            if (tvType != member.Type)
            {
                if (tvType.IsValueType && !member.Type.IsValueType)
                    il.Emit(OpCodes.Box, tvType);
                else if (!tvType.IsValueType && member.Type.IsValueType)
                    il.Emit(OpCodes.Unbox_Any, member.Type);
                else if (!member.Type.IsValueType)
                    il.Emit(OpCodes.Castclass, member.Type);
            }

            if (member.Info is FieldInfo setterField)
                il.Emit(member.IsStatic ? OpCodes.Stsfld : OpCodes.Stfld, setterField);
            else
                EmitCall(il, member.SetMethod!);

            il.Emit(OpCodes.Ret);
            return (Setter<T, TValue>)method.CreateDelegate(typeof(Setter<T, TValue>));
        }

        public override RefSetter<T, TValue> BuildRefSetter<T, TValue>(ProbeMember member)
        {
            if (!member.CanWrite)
                throw new MemberNotWritableException(member.DeclaringType, member.Name);

            Type tType = typeof(T);
            Type tvType = typeof(TValue);
            DynamicMethod method = CreateMethod(
                $"ProbeRefSet_{tType.Name}_{member.Name}",
                typeof(void),
                new[] { tType.MakeByRefType(), tvType },
                member.DeclaringType);
            ILGenerator il = method.GetILGenerator();

            if (!member.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);
                if (!tType.IsValueType)
                {
                    il.Emit(OpCodes.Ldind_Ref);
                    if (tType != member.DeclaringType)
                        il.Emit(OpCodes.Castclass, member.DeclaringType);
                }
            }

            il.Emit(OpCodes.Ldarg_1);
            if (tvType != member.Type)
            {
                if (tvType.IsValueType && !member.Type.IsValueType)
                    il.Emit(OpCodes.Box, tvType);
                else if (!tvType.IsValueType && member.Type.IsValueType)
                    il.Emit(OpCodes.Unbox_Any, member.Type);
                else if (!member.Type.IsValueType)
                    il.Emit(OpCodes.Castclass, member.Type);
            }

            if (member.Info is FieldInfo refSetterField)
                il.Emit(member.IsStatic ? OpCodes.Stsfld : OpCodes.Stfld, refSetterField);
            else
                EmitCall(il, member.SetMethod!);

            il.Emit(OpCodes.Ret);
            return (RefSetter<T, TValue>)method.CreateDelegate(typeof(RefSetter<T, TValue>));
        }

        public override Func<T> BuildDefaultFactory<T>(ConstructorInfo? constructor)
        {
            Type tType = typeof(T);
            DynamicMethod method = CreateMethod("ProbeNew_" + tType.Name, tType, Type.EmptyTypes, tType);
            ILGenerator il = method.GetILGenerator();

            if (constructor != null)
            {
                il.Emit(OpCodes.Newobj, constructor);           // new T()
            }
            else
            {
                LocalBuilder value = il.DeclareLocal(tType);    // 생성자 없는 struct : default(T)
                il.Emit(OpCodes.Ldloca_S, value);
                il.Emit(OpCodes.Initobj, tType);
                il.Emit(OpCodes.Ldloc, value);
            }

            il.Emit(OpCodes.Ret);
            return (Func<T>)method.CreateDelegate(typeof(Func<T>));
        }

        public override ProbeFactory BuildFactory(ConstructorInfo constructor)
        {
            Type owner = constructor.DeclaringType!;
            DynamicMethod method = CreateMethod("ProbeNew_" + owner.Name, typeof(object), new[] { typeof(object?[]) }, owner);
            ILGenerator il = method.GetILGenerator();

            EmitLoadArguments(il, OpCodes.Ldarg_0, constructor.GetParameters());
            il.Emit(OpCodes.Newobj, constructor);
            ILEmitHelpers.EmitConvertToObject(il, owner);
            il.Emit(OpCodes.Ret);
            return (ProbeFactory)method.CreateDelegate(typeof(ProbeFactory));
        }

        public override ProbeInvoker BuildInvoker(MethodInfo method)
        {
            Type owner = method.DeclaringType!;
            DynamicMethod dynamicMethod = CreateMethod(
                $"ProbeInvoke_{owner.Name}_{method.Name}",
                typeof(object),
                new[] { typeof(object), typeof(object?[]) },
                owner);
            ILGenerator il = dynamicMethod.GetILGenerator();

            if (!method.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);
                ILEmitHelpers.EmitLoadTarget(il, owner);       // class → castclass, struct → unbox (박스 안 주소)
            }

            EmitLoadArguments(il, OpCodes.Ldarg_1, method.GetParameters());
            EmitCall(il, method);

            if (method.ReturnType == typeof(void))
                il.Emit(OpCodes.Ldnull);                        // void → null
            else
                ILEmitHelpers.EmitConvertToObject(il, method.ReturnType);

            il.Emit(OpCodes.Ret);
            return (ProbeInvoker)dynamicMethod.CreateDelegate(typeof(ProbeInvoker));
        }

        public override Delegate BuildTypedFactory(ConstructorInfo constructor, Type delegateType)
        {
            Type owner = constructor.DeclaringType!;
            Type[] parameterTypes = DelegateSignature.ParameterTypes(delegateType);
            DynamicMethod method = CreateMethod("ProbeNewTyped_" + owner.Name, owner, parameterTypes, owner);
            ILGenerator il = method.GetILGenerator();

            for (int i = 0; i < parameterTypes.Length; i++)
                EmitLdarg(il, i);                               // 인자를 그대로 — 배열 · unbox 없음
            il.Emit(OpCodes.Newobj, constructor);
            il.Emit(OpCodes.Ret);
            return method.CreateDelegate(delegateType);
        }

        public override Delegate BuildTypedInvoker(MethodInfo method, Type delegateType)
        {
            Type owner = method.DeclaringType!;
            Type[] parameterTypes = DelegateSignature.ParameterTypes(delegateType);
            DynamicMethod dynamicMethod = CreateMethod(
                $"ProbeInvokeTyped_{owner.Name}_{method.Name}",
                method.ReturnType,
                parameterTypes,
                owner);
            ILGenerator il = dynamicMethod.GetILGenerator();

            int first = 0;
            if (!method.IsStatic)
            {
                Type targetType = parameterTypes[0];
                if (owner.IsValueType)
                {
                    // ref T 는 이미 주소, T 는 매개변수 주소 (사본에 호출)
                    if (targetType.IsByRef)
                        il.Emit(OpCodes.Ldarg_0);
                    else
                        il.Emit(OpCodes.Ldarga_S, (byte)0);
                }
                else
                {
                    il.Emit(OpCodes.Ldarg_0);
                    if (!owner.IsAssignableFrom(targetType))
                        il.Emit(OpCodes.Castclass, owner);     // object · 부모 타입으로 받은 대상
                }
                first = 1;
            }

            for (int i = first; i < parameterTypes.Length; i++)
                EmitLdarg(il, i);
            EmitCall(il, method);
            il.Emit(OpCodes.Ret);
            return dynamicMethod.CreateDelegate(delegateType);
        }

        private static void EmitLdarg(ILGenerator il, int index)
        {
            switch (index)
            {
                case 0: il.Emit(OpCodes.Ldarg_0); break;
                case 1: il.Emit(OpCodes.Ldarg_1); break;
                case 2: il.Emit(OpCodes.Ldarg_2); break;
                case 3: il.Emit(OpCodes.Ldarg_3); break;
                default:
                    if (index <= byte.MaxValue)
                        il.Emit(OpCodes.Ldarg_S, (byte)index);
                    else
                        il.Emit(OpCodes.Ldarg, (short)index);
                    break;
            }
        }

        /// <summary>args[i] 를 꺼내 매개변수 타입으로 바꿔 스택에 올린다. 개수 · 타입 검사는 하지 않는다 (설계 결정 17).</summary>
        private static void EmitLoadArguments(ILGenerator il, OpCode loadArray, ParameterInfo[] parameters)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                il.Emit(loadArray);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldelem_Ref);
                ILEmitHelpers.EmitConvertFromObject(il, parameters[i].ParameterType);
            }
        }

        /// <summary>인터페이스·배열은 DynamicMethod owner 가 될 수 없어 모듈 소속으로 만든다.</summary>
        private static DynamicMethod CreateMethod(string name, Type returnType, Type[] parameterTypes, Type owner)
        {
            if (owner.IsInterface || owner.IsArray)
                return new DynamicMethod(name, returnType, parameterTypes, typeof(EmitProbeBuilder).Module, skipVisibility: true);

            return new DynamicMethod(name, returnType, parameterTypes, owner, skipVisibility: true);
        }

        /// <summary>switch(index) 를 내보내고 case 레이블을 돌려준다. 범위 밖 index 는 ArgumentOutOfRangeException.</summary>
        private static Label[] EmitSwitch(ILGenerator il, int count)
        {
            var cases = new Label[count];
            for (int i = 0; i < count; i++)
                cases[i] = il.DefineLabel();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Switch, cases);
            il.Emit(OpCodes.Ldstr, "index");
            il.Emit(OpCodes.Newobj, _outOfRangeCtor);
            il.Emit(OpCodes.Throw);
            return cases;
        }

        /// <summary>throw new XxxException(typeof(type), name)</summary>
        private static void EmitThrow(ILGenerator il, ConstructorInfo exceptionCtor, Type type, string memberName)
        {
            il.Emit(OpCodes.Ldtoken, type);
            il.Emit(OpCodes.Call, _getTypeFromHandle);
            il.Emit(OpCodes.Ldstr, memberName);
            il.Emit(OpCodes.Newobj, exceptionCtor);
            il.Emit(OpCodes.Throw);
        }

        /// <summary>static · 값 타입 메서드는 call, 참조 타입 인스턴스 메서드는 callvirt.</summary>
        private static void EmitCall(ILGenerator il, MethodInfo method)
        {
            bool isVirtualCall = !method.IsStatic && !method.DeclaringType!.IsValueType;
            il.Emit(isVirtualCall ? OpCodes.Callvirt : OpCodes.Call, method);
        }
    }
}
