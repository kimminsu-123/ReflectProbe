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

        public override Func<object>? BuildDefaultFactory(Type type) => throw new NotImplementedException();

        public override ProbeFactory BuildFactory(ConstructorInfo constructor) => throw new NotImplementedException();

        public override ProbeInvoker BuildInvoker(MethodInfo method) => throw new NotImplementedException();

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
