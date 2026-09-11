using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;

namespace GHShield.Core
{
    /// <summary>
    /// Generates a protection adapter at runtime for whatever attributes class
    /// an object happens to have.
    ///
    /// WHY THIS EXISTS
    ///
    /// Hand-writing one adapter per component type does not scale and cannot
    /// ever be complete. Grasshopper ships dozens of interactive object types;
    /// third-party plugins add hundreds more, and their attributes classes
    /// cannot be referenced at compile time at all. The Gene Pool is the
    /// obvious example: it lives in Galapagos, a separate assembly.
    ///
    /// So instead of naming types, this asks the object what class its
    /// attributes are, then emits a subclass of exactly that class which
    /// overrides the four mouse handlers. Because the generated type really
    /// does derive from the original, Grasshopper's own rendering and any
    /// internal casts keep working untouched.
    ///
    /// The proxy falls straight through to the base implementation unless the
    /// object is frozen, so an unfrozen object behaves identically to one
    /// GHShield has never seen.
    ///
    /// Proxies are installed only on objects that are actually frozen, which
    /// keeps the blast radius as small as possible.
    /// </summary>
    public static class AttributeProxyFactory
    {
        private static readonly Dictionary<Type, Type> Cache =
            new Dictionary<Type, Type>();

        private static readonly HashSet<Type> Failed =
            new HashSet<Type>();

        private static ModuleBuilder _module;
        private static int _counter;

        private static readonly string[] MouseMethods =
        {
            "RespondToMouseDown",
            "RespondToMouseMove",
            "RespondToMouseUp",
            "RespondToMouseDoubleClick"
        };

        // =====================================================
        // PUBLIC ENTRY POINT
        // =====================================================

        /// <summary>
        /// Ensures the object's attributes refuse interaction while frozen.
        /// Safe to call repeatedly; does nothing if protection is already in
        /// place or if this attributes class cannot be subclassed.
        /// </summary>
        public static void Protect(IGH_DocumentObject obj)
        {
            if (obj == null)
                return;

            IGH_Attributes original = obj.Attributes;

            if (original == null)
                return;

            // Already protected, by a hand-written adapter or a proxy.
            if (original is IGHShieldAttributes)
                return;

            Type baseType = original.GetType();

            if (Failed.Contains(baseType))
                return;

            Type proxyType = GetOrCreateProxy(baseType);

            if (proxyType == null)
                return;

            try
            {
                IGH_Attributes replacement =
                    Activator.CreateInstance(proxyType, new object[] { obj })
                        as IGH_Attributes;

                if (replacement == null)
                {
                    Failed.Add(baseType);
                    return;
                }

                // A fresh attributes instance starts at the canvas origin.
                PointF pivot = original.Pivot;
                bool selected = original.Selected;

                obj.Attributes = replacement;
                obj.Attributes.Pivot = pivot;
                obj.Attributes.Selected = selected;
                obj.Attributes.ExpireLayout();

                Log.Debug($"Proxy protection attached to '{obj.NickName}' ({baseType.Name}).");
            }
            catch (Exception ex)
            {
                Failed.Add(baseType);

                Log.Debug(
                    $"Could not protect '{obj.NickName}' ({baseType.Name}): {ex.Message}");
            }
        }

        // =====================================================
        // TYPE GENERATION
        // =====================================================

        private static Type GetOrCreateProxy(Type baseType)
        {
            Type existing;

            if (Cache.TryGetValue(baseType, out existing))
                return existing;

            Type built = null;

            try
            {
                built = Build(baseType);
            }
            catch (Exception ex)
            {
                Log.Debug($"Proxy generation failed for {baseType.Name}: {ex.Message}");
            }

            if (built == null)
            {
                Failed.Add(baseType);
                return null;
            }

            Cache[baseType] = built;

            return built;
        }

        private static Type Build(Type baseType)
        {
            // A sealed, generic or inaccessible class cannot be subclassed.
            if (baseType.IsSealed || baseType.IsGenericType || !baseType.IsVisible)
            {
                Log.Debug($"{baseType.Name} cannot be subclassed - skipping.");
                return null;
            }

            ConstructorInfo baseCtor = baseType
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(c => c.GetParameters().Length == 1);

            if (baseCtor == null)
            {
                Log.Debug($"{baseType.Name} has no single-argument constructor - skipping.");
                return null;
            }

            TypeBuilder type = Module().DefineType(
                "GHShield.Generated.Proxy_" + baseType.Name + "_" + (++_counter),
                TypeAttributes.Public | TypeAttributes.Class,
                baseType,
                new[] { typeof(IGHShieldAttributes) });

            EmitConstructor(type, baseCtor);

            int overridden = 0;

            foreach (string name in MouseMethods)
            {
                if (EmitOverride(type, baseType, name))
                    overridden++;
            }

            if (overridden == 0)
            {
                Log.Debug($"{baseType.Name} exposes no overridable mouse handlers - skipping.");
                return null;
            }

            return type.CreateType();
        }

        private static void EmitConstructor(
            TypeBuilder type,
            ConstructorInfo baseCtor)
        {
            Type[] parameters = baseCtor
                .GetParameters()
                .Select(p => p.ParameterType)
                .ToArray();

            ConstructorBuilder ctor = type.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                parameters);

            ILGenerator il = ctor.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, baseCtor);
            il.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// Emits:
        ///
        ///     if (ProxyInterceptor.ShouldBlock(this))
        ///         return ProxyInterceptor.Block(this, sender, e);
        ///
        ///     return base.Method(sender, e);
        /// </summary>
        private static bool EmitOverride(
            TypeBuilder type,
            Type baseType,
            string methodName)
        {
            Type[] signature = { typeof(GH_Canvas), typeof(GH_CanvasMouseEvent) };

            MethodInfo baseMethod = baseType.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Instance,
                null,
                signature,
                null);

            // IsFinal catches a method that was sealed further down the chain.
            if (baseMethod == null || !baseMethod.IsVirtual || baseMethod.IsFinal)
                return false;

            if (baseMethod.ReturnType != typeof(GH_ObjectResponse))
                return false;

            MethodInfo shouldBlock = typeof(ProxyInterceptor).GetMethod(
                "ShouldBlock",
                BindingFlags.Public | BindingFlags.Static);

            MethodInfo block = typeof(ProxyInterceptor).GetMethod(
                "Block",
                BindingFlags.Public | BindingFlags.Static);

            if (shouldBlock == null || block == null)
                return false;

            MethodBuilder method = type.DefineMethod(
                methodName,
                MethodAttributes.Public |
                MethodAttributes.Virtual |
                MethodAttributes.HideBySig,
                typeof(GH_ObjectResponse),
                signature);

            ILGenerator il = method.GetILGenerator();
            Label fallThrough = il.DefineLabel();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, shouldBlock);
            il.Emit(OpCodes.Brfalse, fallThrough);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Call, block);
            il.Emit(OpCodes.Ret);

            il.MarkLabel(fallThrough);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            // Call, not Callvirt: this must reach the base implementation
            // rather than recursing into the override we are defining.
            il.Emit(OpCodes.Call, baseMethod);
            il.Emit(OpCodes.Ret);

            type.DefineMethodOverride(method, baseMethod);

            return true;
        }

        private static ModuleBuilder Module()
        {
            if (_module != null)
                return _module;

            AssemblyName name = new AssemblyName("GHShield.GeneratedProxies");

            AssemblyBuilder assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(
                name,
                AssemblyBuilderAccess.Run);

            _module = assembly.DefineDynamicModule("Main");

            return _module;
        }
    }
}
