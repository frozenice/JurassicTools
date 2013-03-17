using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using Jurassic;
using Jurassic.Library;

namespace JurassicTools
{
  public static class JurassicExposer
  {
    private static readonly AssemblyBuilder MyAssembly;
    private static readonly ModuleBuilder MyModule;

    private static readonly Dictionary<Type, JurassicInfo[]> TypeInfos = new Dictionary<Type, JurassicInfo[]>();

    private static readonly Dictionary<Type, Type> StaticProxyCache = new Dictionary<Type, Type>();
    private static readonly Dictionary<Type, Type> InstanceProxyCache = new Dictionary<Type, Type>();

    private static long DelegateCounter;

    private static readonly Dictionary<Tuple<Type, WeakReference>, Delegate> DelegateProxyCache = new Dictionary<Tuple<Type, WeakReference>, Delegate>();
    private static readonly Dictionary<long, object> DelegateFunctions = new Dictionary<long, object>();

    static JurassicExposer()
    {
      AssemblyName name = new AssemblyName("JurassicProxy");
      MyAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(name, AssemblyBuilderAccess.RunAndSave);
      MyModule = MyAssembly.DefineDynamicModule("JurassicProxy.dll", "JurassicProxy.dll");
    }

    public static void SaveAssembly()
    {
      MyAssembly.Save("JurassicProxy.dll");
    }

    internal static object GetFunction(long index)
    {
      return DelegateFunctions[index];
    }

    private static long AddFunction(Type type, object function)
    {
      long l = Interlocked.Increment(ref DelegateCounter);
      DelegateFunctions[l] = function;
      return l;
    }

    public static void RegisterInfos<T>(params JurassicInfo[] infos)
    {
      RegisterInfos(typeof(T), infos);
    }

    public static void RegisterInfos(Type typeT, params JurassicInfo[] infos)
    {
      if (TypeInfos.ContainsKey(typeT)) return;
      TypeInfos[typeT] = infos;
    }

    private static JurassicInfo[] FindInfos(Type type)
    {
      List<JurassicInfo> infos = new List<JurassicInfo>();
      Type t = type;
      while (t != null && t != typeof(Object))
      {
        if (TypeInfos.ContainsKey(t))
        {
          infos.AddRange(TypeInfos[t].Where(ni => !infos.Any(i => String.Equals(i.MemberName, ni.MemberName))));
        }
        t = t.BaseType;
      }
      foreach (Type implementedInterface in type.GetInterfaces())
      {
        if (TypeInfos.ContainsKey(implementedInterface))
        {
          infos.AddRange(TypeInfos[implementedInterface].Where(ni => !infos.Any(i => String.Equals(i.MemberName, ni.MemberName))));
        }
      }
      return infos.ToArray();
    }

    public static void ExposeClass<T>(ScriptEngine engine, String name = null)
    {
      ExposeClass(typeof(T), engine, name);
    }

    public static void ExposeClass(Type typeT, ScriptEngine engine, String name = null)
    {
      if (name == null) name = typeT.Name;
      JurassicInfo[] infos = FindInfos(typeT);

      Type proxiedType;
      if (!StaticProxyCache.TryGetValue(typeT, out proxiedType))
      {
        // public class JurassicStaticProxy.T : ClrFunction
        TypeBuilder typeBuilder = MyModule.DefineType("JurassicStaticProxy." + typeT.FullName, TypeAttributes.Class | TypeAttributes.Public,
                                                       typeof(ClrFunction));

        // public .ctor(ScriptEngine engine, plainString name)
        // : base(engine.Function.InstancePrototype, name, engine.Object)
        // base.PopulateFunctions(null, BindingFlags.Public | BindingFlags.Static /*| BindingFlags.DeclaredOnly*/);
        // base.PopulateFields(null);
        ConstructorBuilder ctorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public | MethodAttributes.HideBySig, CallingConventions.HasThis,
                                                                       new[] { typeof(ScriptEngine), typeof(string) });
        ILGenerator ctorGen = ctorBuilder.GetILGenerator();

        ctorGen.Emit(OpCodes.Ldarg_0); // # this
        ctorGen.Emit(OpCodes.Ldarg_1); // > engine
        ctorGen.Emit(OpCodes.Callvirt, ReflectionCache.ScriptEngine__get_Function); // > <.Function
        ctorGen.Emit(OpCodes.Callvirt, ReflectionCache.FunctionInstance__get_InstancePrototype); // > <.InstancePrototype
        ctorGen.Emit(OpCodes.Ldarg_2); // > name
        ctorGen.Emit(OpCodes.Ldarg_1); // > engine
        ctorGen.Emit(OpCodes.Callvirt, ReflectionCache.ScriptEngine__get_Object); // > <.Object
        ctorGen.Emit(OpCodes.Call, ReflectionCache.ClrFunction__ctor__ObjectInstance_String_ObjectInstance); // #:base(<, <, <)
        ctorGen.Emit(OpCodes.Ldarg_0); // # this
        ctorGen.Emit(OpCodes.Ldtoken, typeBuilder); // __this__
        ctorGen.Emit(OpCodes.Call, ReflectionCache.Type__GetTypeFromHandle__RuntimeTypeHandle); // > typeof(__this__)
        ctorGen.Emit(OpCodes.Ldc_I4, (int)(BindingFlags.Public /**/| BindingFlags.Static/**/ | BindingFlags.Instance /*| BindingFlags.DeclaredOnly*/)); // > flags
        ctorGen.Emit(OpCodes.Call, ReflectionCache.ObjectInstance__PopulateFunctions__Type_BindingFlags); // #.PopulateFunctions(<, <)
        ctorGen.Emit(OpCodes.Ldarg_0); // # this
        ctorGen.Emit(OpCodes.Ldnull); // > null
        ctorGen.Emit(OpCodes.Call, ReflectionCache.ObjectInstance__PopulateFields__Type); // #.PopulateFields(<)
        ctorGen.Emit(OpCodes.Ret);

        if (typeT.IsEnum)
        {
          if (Attribute.IsDefined(typeT, typeof(FlagsAttribute)))
          {
            Type enumType = Enum.GetUnderlyingType(typeT);
            foreach (string v in Enum.GetNames(typeT))
            {
              FieldBuilder field = typeBuilder.DefineField(v, enumType, FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal);
              field.SetConstant(Convert.ChangeType(Enum.Parse(typeT, v), enumType));
              field.SetCustomAttribute(new CustomAttributeBuilder(ReflectionCache.JSField__ctor, new object[] { }));
            }
          }
          else
          {
            foreach (string v in Enum.GetNames(typeT))
            {
              FieldBuilder field = typeBuilder.DefineField(v, typeof(string), FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal);
              field.SetConstant(v);
              field.SetCustomAttribute(new CustomAttributeBuilder(ReflectionCache.JSField__ctor, new object[] { }));
            }
          }
        }
        else
        {
          // [JsConstructorFunction]
          // public ObjectInstance Construct(params object[] args)
          // return JurassicExposer.WrapObject(Activator.CreateInstance(typeof(T), args), Engine);
          MethodBuilder jsctorBuilder = typeBuilder.DefineMethod("Construct", MethodAttributes.Public | MethodAttributes.HideBySig, CallingConventions.HasThis,
                                                                 typeof(ObjectInstance), new[] { typeof(Object[]) });
          jsctorBuilder.SetCustomAttribute(new CustomAttributeBuilder(ReflectionCache.JSConstructorFunctionAttribute__ctor, new object[] { }));
          ParameterBuilder jsctorParams = jsctorBuilder.DefineParameter(1, ParameterAttributes.None, "args");
          jsctorParams.SetCustomAttribute(new CustomAttributeBuilder(ReflectionCache.ParamArrayAttribute__ctor, new object[] { }));
          ILGenerator jsctorGen = jsctorBuilder.GetILGenerator();

          jsctorGen.Emit(OpCodes.Ldtoken, typeT); // T
          jsctorGen.Emit(OpCodes.Call, ReflectionCache.Type__GetTypeFromHandle__RuntimeTypeHandle); // > typeof(T)
          jsctorGen.Emit(OpCodes.Ldarg_1); // > args
          jsctorGen.Emit(OpCodes.Call, ReflectionCache.Activator__CreateInstance__Type_aObject); // > Activator.CreateInstance(<, <)
          jsctorGen.Emit(OpCodes.Ldarg_0); // # this
          jsctorGen.Emit(OpCodes.Call, ReflectionCache.ObjectInstance__get_Engine); // > #.Engine
          jsctorGen.Emit(OpCodes.Call, ReflectionCache.JurassicExposer__WrapObject__Object_ScriptEngine); // > JurassicExposer.WrapObject(<, <)
          jsctorGen.Emit(OpCodes.Ret);

          // public ... Method(...)
          // !converting static method to instance method!
          MethodInfo[] miStatics = typeT.GetMethods(BindingFlags.Public | BindingFlags.Static /*| BindingFlags.DeclaredOnly*/);
          List<String> methodNames = new List<string>();
          foreach (MethodInfo miStatic in miStatics)
          {
            if (methodNames.Contains(miStatic.Name)) continue;
            else methodNames.Add(miStatic.Name);
            Attribute[] infoAttributes = GetAttributes(infos, miStatic.Name, typeof(JSFunctionAttribute));
            if (!Attribute.IsDefined(miStatic, typeof(JSFunctionAttribute)) && infoAttributes.Length == 0) continue;
            MethodBuilder proxyStatic = typeBuilder.DefineMethod(miStatic.Name, miStatic.Attributes & ~MethodAttributes.Static);
            proxyStatic.SetReturnType(GetConvertOrWrapType(miStatic.ReturnType));
            proxyStatic.CopyParametersFrom(miStatic);
            proxyStatic.CopyCustomAttributesFrom(miStatic, infoAttributes);
            ILGenerator methodGen = proxyStatic.GetILGenerator();

            ParameterInfo[] parameterInfos = miStatic.GetParameters();
            JSFunctionAttribute attr = Attribute.GetCustomAttribute(miStatic, typeof(JSFunctionAttribute)) as JSFunctionAttribute;
            if (attr != null && attr.Flags.HasFlag(JSFunctionFlags.HasEngineParameter))
            {
              parameterInfos = parameterInfos.Skip(1).ToArray();
              methodGen.Emit(OpCodes.Ldarg, 0); // > [this]
              methodGen.Emit(OpCodes.Call, ReflectionCache.ObjectInstance__get_Engine); // > <[this].Engine
            }
            for (int i = 0; i < parameterInfos.Length; i++)
            {
              methodGen.Emit(OpCodes.Ldarg, i + 1); // > arg*
              if (!Attribute.IsDefined(parameterInfos[i], typeof(ParamArrayAttribute))) EmitConvertOrUnwrap(methodGen, parameterInfos[i].ParameterType);
            }
            methodGen.Emit(miStatic.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, miStatic); // >? Method(<*)
            EmitConvertOrWrap(methodGen, miStatic.ReturnType);
            methodGen.Emit(OpCodes.Ret);
          }

          // public ... Property
          // !converting static accessors to instance accessors!
          PropertyInfo[] piStatics = typeT.GetProperties(BindingFlags.Public | BindingFlags.Static /*| BindingFlags.DeclaredOnly*/);
          foreach (PropertyInfo piStatic in piStatics)
          {
            Attribute[] infoAttributes = GetAttributes(infos, piStatic.Name, typeof(JSPropertyAttribute));
            if (!Attribute.IsDefined(piStatic, typeof(JSPropertyAttribute)) && infoAttributes.Length == 0) continue;
            MethodInfo piStaticGet = piStatic.GetGetMethod();
            MethodInfo piStaticSet = piStatic.GetSetMethod();
            if (piStaticGet == null && piStaticSet == null) continue;
            PropertyBuilder proxyStatic = typeBuilder.DefineProperty(piStatic.Name, piStatic.Attributes, GetConvertOrWrapType(piStatic.PropertyType), null);
            proxyStatic.CopyCustomAttributesFrom(piStatic, infoAttributes);
            if (piStaticGet != null && !methodNames.Contains(piStaticGet.Name))
            {
              methodNames.Add(piStaticGet.Name);
              MethodBuilder proxyStaticGet = typeBuilder.DefineMethod(piStaticGet.Name, piStaticGet.Attributes & ~MethodAttributes.Static);
              proxyStaticGet.SetReturnType(GetConvertOrWrapType(piStaticGet.ReturnType));
              proxyStaticGet.CopyParametersFrom(piStaticGet);
              proxyStaticGet.CopyCustomAttributesFrom(piStaticGet);
              ILGenerator getGen = proxyStaticGet.GetILGenerator();

              ParameterInfo[] parameterInfos = piStaticGet.GetParameters();
              JSFunctionAttribute attr = Attribute.GetCustomAttribute(piStaticGet, typeof(JSFunctionAttribute)) as JSFunctionAttribute;
              if (attr != null && attr.Flags.HasFlag(JSFunctionFlags.HasEngineParameter))
              {
                parameterInfos = parameterInfos.Skip(1).ToArray();
                getGen.Emit(OpCodes.Ldarg, 0); // > [this]
                getGen.Emit(OpCodes.Call, ReflectionCache.ObjectInstance__get_Engine); // > <[this].Engine
              }
              for (int i = 0; i < parameterInfos.Length; i++)
              {
                getGen.Emit(OpCodes.Ldarg, i + 1); // > arg*
                if (!Attribute.IsDefined(parameterInfos[i], typeof(ParamArrayAttribute))) EmitConvertOrUnwrap(getGen, parameterInfos[i].ParameterType);
              }
              getGen.Emit(piStaticGet.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, piStaticGet); // > Property <*
              EmitConvertOrWrap(getGen, piStaticGet.ReturnType);
              getGen.Emit(OpCodes.Ret);
              proxyStatic.SetGetMethod(proxyStaticGet);
            }
            if (piStaticSet != null && !methodNames.Contains(piStaticSet.Name))
            {
              methodNames.Add(piStaticSet.Name);
              MethodBuilder proxyStaticSet = typeBuilder.DefineMethod(piStaticSet.Name, piStaticSet.Attributes & ~MethodAttributes.Static);
              proxyStaticSet.SetReturnType(piStaticSet.ReturnType);
              proxyStaticSet.CopyParametersFrom(piStaticSet);
              proxyStaticSet.CopyCustomAttributesFrom(piStaticSet);
              ILGenerator setGen = proxyStaticSet.GetILGenerator();

              ParameterInfo[] parameterInfos = piStaticSet.GetParameters();
              JSFunctionAttribute attr = Attribute.GetCustomAttribute(piStaticSet, typeof(JSFunctionAttribute)) as JSFunctionAttribute;
              if (attr != null && attr.Flags.HasFlag(JSFunctionFlags.HasEngineParameter))
              {
                parameterInfos = parameterInfos.Skip(1).ToArray();
                setGen.Emit(OpCodes.Ldarg, 0); // > [this]
                setGen.Emit(OpCodes.Call, ReflectionCache.ObjectInstance__get_Engine); // > <[this].Engine
              }
              for (int i = 0; i < parameterInfos.Length; i++)
              {
                setGen.Emit(OpCodes.Ldarg, i + 1); // > arg*
                if (!Attribute.IsDefined(parameterInfos[i], typeof(ParamArrayAttribute))) EmitConvertOrUnwrap(setGen, parameterInfos[i].ParameterType);
              }
              setGen.Emit(piStaticSet.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, piStaticSet); // Property = <*
              setGen.Emit(OpCodes.Ret);
              proxyStatic.SetSetMethod(proxyStaticSet);
            }
          }

          // public event ...
          // !converting static methods to instance methods!
          EventInfo[] eiStatics = typeT.GetEvents(BindingFlags.Public | BindingFlags.Static /*| BindingFlags.DeclaredOnly*/);
          foreach (EventInfo eventInfo in eiStatics)
          {
            Attribute[] infoAttributes = GetAttributes(infos, eventInfo.Name, typeof(JSEventAttribute));
            if (!Attribute.IsDefined(eventInfo, typeof(JSEventAttribute)) && infoAttributes.Length == 0) continue;
            JSEventAttribute eventAttribute = (JSEventAttribute)Attribute.GetCustomAttribute(eventInfo, typeof(JSEventAttribute));
            if (eventAttribute == null) eventAttribute = (JSEventAttribute)infoAttributes.FirstOrDefault(a => a is JSEventAttribute);
            MethodInfo eiAdd = eventInfo.GetAddMethod();
            if (eventAttribute == null) eventAttribute = new JSEventAttribute();
            string addName = eventAttribute.AddPrefix + (eventAttribute.Name ?? eventInfo.Name);
            MethodBuilder proxyAdd = typeBuilder.DefineMethod(addName, eiAdd.Attributes & ~MethodAttributes.Static);
            proxyAdd.SetReturnType(eiAdd.ReturnType);
            proxyAdd.CopyParametersFrom(eiAdd);
            proxyAdd.CopyCustomAttributesFrom(eiAdd, new JSFunctionAttribute());
            ILGenerator ilAdd = proxyAdd.GetILGenerator();
            ParameterInfo[] parameterInfos = eiAdd.GetParameters();
            JSFunctionAttribute attr = Attribute.GetCustomAttribute(eiAdd, typeof(JSFunctionAttribute)) as JSFunctionAttribute;
            if (attr != null && attr.Flags.HasFlag(JSFunctionFlags.HasEngineParameter))
            {
              parameterInfos = parameterInfos.Skip(1).ToArray();
              ilAdd.Emit(OpCodes.Ldarg, 0); // > [this]
              ilAdd.Emit(OpCodes.Call, ReflectionCache.ObjectInstance__get_Engine); // > <[this].Engine
            }
            for (int i = 0; i < parameterInfos.Length; i++)
            {
              ilAdd.Emit(OpCodes.Ldarg, i + 1); // > arg*
              if (!Attribute.IsDefined(parameterInfos[i], typeof(ParamArrayAttribute))) EmitConvertOrUnwrap(ilAdd, parameterInfos[i].ParameterType);
            }
            ilAdd.Emit(eiAdd.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, eiAdd);
            ilAdd.Emit(OpCodes.Ret);

            MethodInfo eiRemove = eventInfo.GetRemoveMethod();
            string removeName = eventAttribute.RemovePrefix + (eventAttribute.Name ?? eventInfo.Name);
            MethodBuilder proxyRemove = typeBuilder.DefineMethod(removeName, eiRemove.Attributes & ~MethodAttributes.Static);
            proxyRemove.SetReturnType(eiRemove.ReturnType);
            proxyRemove.CopyParametersFrom(eiRemove);
            proxyRemove.CopyCustomAttributesFrom(eiRemove, new JSFunctionAttribute());
            ILGenerator ilRemove = proxyRemove.GetILGenerator();
            parameterInfos = eiRemove.GetParameters();
            attr = Attribute.GetCustomAttribute(eiRemove, typeof(JSFunctionAttribute)) as JSFunctionAttribute;
            if (attr != null && attr.Flags.HasFlag(JSFunctionFlags.HasEngineParameter))
            {
              parameterInfos = parameterInfos.Skip(1).ToArray();
              ilRemove.Emit(OpCodes.Ldarg, 0); // > [this]
              ilRemove.Emit(OpCodes.Call, ReflectionCache.ObjectInstance__get_Engine); // > <[this].Engine
            }
            for (int i = 0; i < parameterInfos.Length; i++)
            {
              ilRemove.Emit(OpCodes.Ldarg, i + 1); // > arg*
              if (!Attribute.IsDefined(parameterInfos[i], typeof(ParamArrayAttribute))) EmitConvertOrUnwrap(ilRemove, parameterInfos[i].ParameterType);
            }
            ilRemove.Emit(eiRemove.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, eiRemove);
            ilRemove.Emit(OpCodes.Ret);
          }
        }

        proxiedType = typeBuilder.CreateType();
        StaticProxyCache[typeT] = proxiedType;
      }
      ClrFunction proxiedInstance = (ClrFunction)Activator.CreateInstance(proxiedType, engine, name);
      engine.SetGlobalValue(name, proxiedInstance);
    }

    public static void ExposeInstance(ScriptEngine engine, object instance, String name)
    {
      object inst = ConvertOrWrapObject(instance, engine);
      engine.SetGlobalValue(name, inst);
    }

    public static void ExposeFunction(ScriptEngine engine, Delegate dele, String name)
    {
      engine.SetGlobalValue(name, WrapDelegate(dele, engine));
    }

    public static Type GetConvertOrWrapType(Type type)
    {
      if (type == typeof(void)) return typeof(void);
      if (type == typeof(ScriptEngine)) return typeof(ScriptEngine); // JSFunction with HasEngineParameter
      if (type.IsEnum)
      {
        return Attribute.IsDefined(type, typeof(FlagsAttribute)) ? Enum.GetUnderlyingType(type) : typeof(string);
      }
      if (type.IsArray)
      {
        return typeof(ArrayInstance);
      }
      switch (Type.GetTypeCode(type))
      {
        case TypeCode.Boolean:
          return typeof(bool);
        case TypeCode.Byte:
          return typeof(int);
        case TypeCode.Char:
          return typeof(string);
        case TypeCode.DateTime:
          return typeof(DateInstance);
        case TypeCode.Decimal:
          return typeof(double);
        case TypeCode.Double:
          return typeof(double);
        case TypeCode.Int16:
          return typeof(int);
        case TypeCode.Int32:
          return typeof(int);
        case TypeCode.Int64:
          return typeof(double);
        case TypeCode.Object:
          return typeof(ObjectInstance);
        case TypeCode.SByte:
          return typeof(int);
        case TypeCode.Single:
          return typeof(double);
        case TypeCode.String:
          return typeof(string);
        case TypeCode.UInt16:
          return typeof(int);
        case TypeCode.UInt32:
          return typeof(uint);
        case TypeCode.UInt64:
          return typeof(double);
        default:
          throw new ArgumentException(string.Format("Cannot convert value of type {0}.", type), "type");
      }
    }

    public static Type GetConvertOrUnwrapType(Type type)
    {
      if (type == typeof(void)) return typeof(void);
      if (type == typeof(ConcatenatedString)) return typeof(string);
      if (type == typeof(ScriptEngine)) return typeof(ScriptEngine); // JSFunction with HasEngineParameter
      if (type == typeof(ArrayInstance)) return typeof(object[]);
      if (type == typeof(BooleanInstance) || type == typeof(bool)) return typeof(bool);
      if (type == typeof(NumberInstance) || type == typeof(Byte) || type == typeof(Decimal) || type == typeof(Double) || type == typeof(Int16) ||
          type == typeof(Int32) || type == typeof(Int64) || type == typeof(SByte) || type == typeof(Single) || type == typeof(UInt16) || type == typeof(UInt32) ||
          type == typeof(UInt64)) return typeof(double);
      if (type == typeof(StringInstance) || type == typeof(string)) return typeof(string);
      if (type == typeof(DateInstance)) return typeof(DateTime);
      if (type == typeof(FunctionInstance)) return typeof(Delegate); // TODO?
      if (type == typeof(ObjectInstance)) return typeof(IDictionary<string, object>);
      throw new ArgumentException("unkown type for unwrap: " + type.FullName);
    }

    public static object ConvertOrWrapObject(object instance, ScriptEngine engine)
    {
      if (instance == null) return Undefined.Value;
      //if (instance == null) return engine.Object.InstancePrototype;
      Type type = instance.GetType();
      if (type == typeof(void)) return null;
      if (type == typeof(ScriptEngine)) return instance;
      if (type.IsEnum)
      {
        return Attribute.IsDefined(type, typeof(FlagsAttribute)) ? Convert.ChangeType(instance, Enum.GetUnderlyingType(type)) : Enum.GetName(type, instance);
      }
      if (type.IsArray)
      {
        Array arr = (Array)instance;
        ArrayInstance arr2 = engine.Array.New();
        for (int i = 0; i < arr.Length; i++)
        {
          arr2[i] = ConvertOrWrapObject(arr.GetValue(i), engine);
        }
        return arr2;
      }
      if (type == typeof(JSONString))
      {
        return JSONObject.Parse(engine, ((JSONString)instance).plainString);
      }
      switch (Type.GetTypeCode(type))
      {
        case TypeCode.Boolean:
          return (bool)instance;
        case TypeCode.Byte:
          return (int)(byte)instance;
        case TypeCode.Char:
          return new string((char)instance, 1);
        case TypeCode.DateTime:
          DateTime dt = (DateTime)instance;
          if (dt == DateTime.MinValue) return null;
          return engine.Date.Construct((((DateTime)instance).ToUniversalTime().Ticks - new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).Ticks) / 10000.0);
        case TypeCode.Decimal:
          return decimal.ToDouble((decimal)instance);
        case TypeCode.Double:
          return (double)instance;
        case TypeCode.Int16:
          return (int)(short)instance;
        case TypeCode.Int32:
          return (int)instance;
        case TypeCode.Int64:
          return (double)(long)instance;
        case TypeCode.Object:
          if (instance is FunctionInstance)
          {
            /*var oldDelegates = DelegateProxyCache.Where(kvp => !kvp.Key.IsAlive);
            foreach (KeyValuePair<WeakReference, Delegate> keyValuePair in oldDelegates)
            {
              DelegateProxyCache.Remove(keyValuePair.Key);
            }
            Delegate dele;
            if (DelegateProxyCache.Any(pair => pair.Key.Target == instance))
            {
              dele = DelegateProxyCache.First(pair => pair.Key.Target == instance).Value;
            }
            else
            {
              //dele = objects => ((FunctionInstance)instance).Call(null, objects);
              dele = null;
              DelegateProxyCache[new WeakReference(instance)] = dele;
            }
            return dele;*/
          }
          if (instance is ObjectInstance) return instance;
          IDictionary dictionary = instance as IDictionary;
          if (dictionary != null)
          {
            ObjectInstance obj = WrapObject(instance, engine);
            foreach (DictionaryEntry dictionaryEntry in dictionary)
            {
              obj.SetPropertyValue(dictionaryEntry.Key.ToString(), ConvertOrWrapObject(dictionaryEntry.Value, engine), true);
            }
          return obj;
          }
          if (typeof(NameValueCollection).IsAssignableFrom(type))
          {
            ObjectInstance obj = WrapObject(instance, engine);
            NameValueCollection nvc = (NameValueCollection)instance;
            foreach (var item in nvc.AllKeys)
            {
              string[] vals = nvc.GetValues(item);
              if (vals.Length == 1) obj.SetPropertyValue(item, vals[0], true);
              else
              {
                ArrayInstance arr = engine.Array.Construct(vals);
                obj.SetPropertyValue(item, arr, true);
              }
            }
            return obj;
          }
          if (type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
          {
            ArrayInstance arr = engine.Array.Construct();
            IEnumerable ienum = (IEnumerable<Object>)instance;
            int i = 0;
            foreach (object item in ienum)
            {
              arr.Push(ConvertOrWrapObject(item, engine));
            }
            return arr;
          }
          if (type.GetInterfaces().Any(i => i == typeof(IEnumerable)))
          {
            ArrayInstance arr = engine.Array.Construct();
            IEnumerable ienum = (IEnumerable)instance;
            int i = 0;
            foreach (object item in ienum)
            {
              arr.Push(ConvertOrWrapObject(item, engine));
            }
            return arr;
          }
          return WrapObject(instance, engine);
        case TypeCode.SByte:
          return (int)(sbyte)instance;
        case TypeCode.Single:
          return (double)(float)instance;
        case TypeCode.String:
          return instance;
        case TypeCode.UInt16:
          return (int)(ushort)instance;
        case TypeCode.UInt32:
          return (uint)instance;
        case TypeCode.UInt64:
          return (double)(ulong)instance;
        default:
          throw new ArgumentException(string.Format("Cannot store value of type {0}.", type), "instance");
      }
    }

    public static object ConvertOrUnwrapObject(object instance, Type type)
    {
      if (instance == null) return Null.Value;
      //Type type = instance.GetType();
      if (type == typeof(void)) return null;
      if (instance is ConcatenatedString)
      {
        return (instance as ConcatenatedString).ToString();
      }
      if (type.IsEnum)
      {
        if (!Attribute.IsDefined(type, typeof(FlagsAttribute)))
        {
          return Enum.Parse(type, (string)instance);
        }
      }
      if (type.IsArray)
      {
        ArrayInstance arr = (ArrayInstance)instance;
        object[] arr2 = new object[arr.Length];
        for (int i = 0; i < arr.Length; i++)
        {
          arr2[i] = ConvertOrUnwrapObject(arr[i], GetConvertOrUnwrapType(arr[i] == null ? null : arr[i].GetType()));
        }
        return arr2;
      }
      if (type == typeof(JSONString))
      {
        throw new NotImplementedException("TODO: Unwrap JSONString");
      }
      switch (Type.GetTypeCode(type))
      {
        case TypeCode.Boolean:
          return (bool)Convert.ChangeType(instance, type);
        case TypeCode.Byte:
          return (byte)Convert.ChangeType(instance, type);
        case TypeCode.Char:
          string str = instance as string;
          return string.IsNullOrEmpty(str) ? char.MinValue : str[0];
        case TypeCode.DateTime:
          return new DateTime((long)((((DateInstance)instance).GetTime() * 10000) + new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).Ticks)).ToLocalTime(/* TODO: check*/);
        case TypeCode.Decimal:
          return new decimal((double)Convert.ChangeType(instance, type));
        case TypeCode.Double:
          return (double)Convert.ChangeType(instance, type);
        case TypeCode.Int16:
          return (short)Convert.ChangeType(instance, type);
        case TypeCode.Int32:
          return (int)Convert.ChangeType(instance, type);
        case TypeCode.Int64:
          return (long)Convert.ChangeType(instance, type);
        case TypeCode.Object:
          if (instance is FunctionInstance && typeof(Delegate).IsAssignableFrom(type))
          {
            var oldDelegates = DelegateProxyCache.Where(kvp => !kvp.Key.Item2.IsAlive);
            foreach (KeyValuePair<Tuple<Type, WeakReference>, Delegate> oldDelegate in oldDelegates)
            {
              DelegateProxyCache.Remove(oldDelegate.Key);
            }

            FunctionInstance function = (FunctionInstance)instance;
            if (DelegateProxyCache.Any(kvp => kvp.Key.Item1 == type && kvp.Key.Item2.Target == function))
            {
              return DelegateProxyCache.First(kvp => kvp.Key.Item1 == type && kvp.Key.Item2.Target == function).Value;
            }

            WeakReference weakReference = new WeakReference(function);
            Tuple<Type, WeakReference> tuple = new Tuple<Type, WeakReference>(type, weakReference);
            var dele = UnwrapFunction(type, function);
            DelegateProxyCache.Add(tuple, dele);
            DelegateProxyCache[tuple] = dele;
            return dele;
          }
          if (typeof(IDictionary<string, object>).IsAssignableFrom(type))
          {
            ObjectInstance obj = instance as ObjectInstance;
            if (obj == null) return null;
            return obj.Properties.ToDictionary(nameAndValue => nameAndValue.Name,
                                               nameAndValue => ConvertOrUnwrapObject(nameAndValue.Value, GetConvertOrUnwrapType(nameAndValue.Value.GetType())));
          }
          if (typeof(NameValueCollection).IsAssignableFrom(type))
          {
            /* NameValueCollection nvc = (NameValueCollection)instance;
             foreach (var item in nvc.AllKeys)
             {
               var val = nvc[item];
               obj.SetPropertyValue(item, ConvertOrWrapObject(val, engine), true);
             }*/
            ObjectInstance obj = instance as ObjectInstance;
            if (obj == null) return null;
            NameValueCollection nvc = new NameValueCollection();
            foreach (PropertyNameAndValue propertyNameAndValue in obj.Properties)
            {
              string key = propertyNameAndValue.Name;
              object val = propertyNameAndValue.Value;
              if (val is ArrayInstance)
              {
                ArrayInstance arr = (ArrayInstance)val;
                for (int i = 0; i < arr.Length; i++)
                {
                  if (arr[i] is ObjectInstance) continue;
                  if (arr[i] is Null) nvc.Add(key, null);
                  else nvc.Add(key, arr[i].ToString());
                }
              }
              else if (val is ObjectInstance) continue;
              else if (val is Null) nvc.Add(key, null);
              else nvc.Add(key, val.ToString());
            }
            return nvc;
          }
          //if (instance is ObjectInstance) return instance;
          if (type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
          {
            /*IEnumerable ienum = (IEnumerable<Object>)instance;
            int i = 0;
            foreach (var item in ienum)
            {
              obj[i++] = ConvertOrWrapObject(item, engine);
            }
            obj["length"] = i;*/
            throw new NotImplementedException("TODO: Unwrap IEnumerable<>");
          }
          if (type.GetInterfaces().Any(i => i == typeof(IEnumerable)))
          {
            /*IEnumerable ienum = (IEnumerable)instance;
            int i = 0;
            foreach (var item in ienum)
            {
              obj[i++] = ConvertOrWrapObject(item, engine);
            }
            obj["length"] = i;*/
            throw new NotImplementedException("TODO: Unwrap IEnumerable");
          }
          Type instanceType = instance.GetType();
          return instanceType.FullName.StartsWith("JurassicInstanceProxy.") ? instanceType.GetField("realInstance").GetValue(instance) : instance;
        case TypeCode.SByte:
          return (sbyte)instance;
        case TypeCode.Single:
          return (float)instance;
        case TypeCode.String:
          return instance;
        case TypeCode.UInt16:
          return (ushort)instance;
        case TypeCode.UInt32:
          return (uint)instance;
        case TypeCode.UInt64:
          return (ulong)instance;
        default:
          throw new ArgumentException(string.Format("Cannot store value of type {0}.", type), "instance");
      }
    }

    private static Delegate UnwrapFunction(Type delegateType, FunctionInstance function)
    {
      if (!typeof(Delegate).IsAssignableFrom(delegateType)) return null;
      MethodInfo mi = delegateType.GetMethod("Invoke");
      ParameterInfo[] parameterInfos = mi.GetParameters();
      DynamicMethod dm = new DynamicMethod("DynamicMethod_for_" + delegateType.Name, mi.ReturnType, parameterInfos.Select(pi => pi.ParameterType).ToArray(),
                                           typeof(JurassicExposer));
      ILGenerator il = dm.GetILGenerator();
      var localFunction = il.DeclareLocal(typeof(FunctionInstance));
      var par = il.DeclareLocal(typeof(object[]));
      long l = AddFunction(delegateType, function);
      il.Emit(OpCodes.Ldc_I8, l); // >[index]
      il.Emit(OpCodes.Call, ReflectionCache.JurassicExposer__GetFunction__long); // >[function] JurassicExposer.GetFunction(<[index])
      il.Emit(OpCodes.Stloc, localFunction); // localFunction = <[function]
      il.Emit(OpCodes.Ldloc, localFunction); // >localFunction
      il.Emit(OpCodes.Ldc_I4, parameterInfos.Length); // >[length]
      il.Emit(OpCodes.Newarr, typeof(object)); // > new object[<[length]]
      il.Emit(OpCodes.Stloc, par); // par = <
      for (int i = 0; i < parameterInfos.Length; i++)
      {
        il.Emit(OpCodes.Ldloc, par); // >par
        il.Emit(OpCodes.Ldc_I4, i); // >i
        il.Emit(OpCodes.Ldarg, i); // > arg*
        if (!Attribute.IsDefined(parameterInfos[i], typeof(ParamArrayAttribute))) EmitConvertOrWrap(il, parameterInfos[i].ParameterType, localFunction);
        if (parameterInfos[i].ParameterType.IsValueType) il.Emit(OpCodes.Box, parameterInfos[i].ParameterType);
        il.Emit(OpCodes.Stelem_Ref); // <par[<i] = <
      }
      il.Emit(OpCodes.Ldnull); // >[thisObject]
      il.Emit(OpCodes.Ldloc, par); // >par
      il.Emit(OpCodes.Callvirt, ReflectionCache.FunctionInstance__CallLateBound__Object_aObject); // >? <localFunction.CallLateBound(<[thisObject], <par)
      if (mi.ReturnType == typeof(void)) il.Emit(OpCodes.Pop);
      else EmitConvertOrWrap(il, mi.ReturnType);
      il.Emit(OpCodes.Ret);
      Delegate dele = dm.CreateDelegate(delegateType);
      return dele;
    }

    private static FunctionInstance WrapDelegate(Delegate dele, ScriptEngine engine)
    {
      return new WrapperFunction(engine, dele);
    }

    private static void EmitConvertOrWrap(ILGenerator gen, Type type, LocalBuilder localFunction = null)
    {
      if (type == typeof(void)) return;
      // > value (from caller)
      if (type.IsValueType)
      {
        gen.Emit(OpCodes.Box, type);
      }
      if (localFunction == null) gen.Emit(OpCodes.Ldarg_0); // >[this]
      else gen.Emit(OpCodes.Ldloc, localFunction); // >localFunction
      gen.Emit(OpCodes.Call, ReflectionCache.ObjectInstance__get_Engine); // > <[this].Engine
      gen.Emit(OpCodes.Call, ReflectionCache.JurassicExposer__ConvertOrWrapObject__Object_ScriptEngine); // JurassicExposer.ConvertOrWrapObject(<, <, <)
      Type convertOrWrapType = GetConvertOrWrapType(type);
      if (convertOrWrapType.IsValueType) gen.Emit(OpCodes.Unbox_Any, convertOrWrapType);
    }

    private static void EmitConvertOrUnwrap(ILGenerator gen, Type type)
    {
      if (type == typeof(void)) return;
      // > value (from caller)
      //Type convertOrWrapType = GetConvertOrWrapType(type);
      //if (convertOrWrapType.IsValueType) gen.Emit(OpCodes.Box, convertOrWrapType);
      if (type.IsValueType)
      {
        if (type.IsEnum)
        {
          //return Attribute.IsDefined(type, typeof(FlagsAttribute)) ? Convert.ChangeType(instance, Enum.GetUnderlyingType(type)) : Enum.GetName(type, instance);
          if (Attribute.IsDefined(type, typeof(FlagsAttribute))) gen.Emit(OpCodes.Box, Enum.GetUnderlyingType(type));
          else gen.Emit(OpCodes.Box, typeof(string));
        }
        else
        {
          // TODO: needed?
          gen.Emit(OpCodes.Box, type); // why? bugs!
        }
      }
      Type realType = type;
      if (type.IsByRef || type.IsPointer) realType = type.GetElementType();
      gen.Emit(OpCodes.Ldtoken, realType); // > type
      gen.Emit(OpCodes.Call, ReflectionCache.Type__GetTypeFromHandle__RuntimeTypeHandle); // > typeof(<)
      gen.Emit(OpCodes.Call, ReflectionCache.JurassicExposer__ConvertOrUnwrapObject__Object_Type); // JurassicExposer.ConvertOrUnwrapObject(<, <)
      if (type.IsValueType) gen.Emit(OpCodes.Unbox_Any, type);
    }

    public static ObjectInstance WrapObject(object instance, ScriptEngine engine)
    {
      Type type = instance.GetType();
      JurassicInfo[] infos = FindInfos(type);

      Type proxiedType;
      if (!InstanceProxyCache.TryGetValue(type, out proxiedType))
      {
        // public class JurassicInstanceProxy.T : ObjectInstance
        TypeBuilder typeBuilder = MyModule.DefineType("JurassicInstanceProxy." + type.FullName, TypeAttributes.Class | TypeAttributes.Public,
                                                       typeof(ObjectInstance));

        // public object realInstance
        FieldBuilder fldInstance = typeBuilder.DefineField("realInstance", typeof(object), FieldAttributes.Public);

        // .ctor(ScriptEngine engine, object instance)
        // : base(engine)
        // base.PopulateFunctions(typeof(__this__), BindingFlags.Public | BindingFlags.Instance /*| BindingFlags.DeclaredOnly*/);
        // realInstance = instance;
        ConstructorBuilder ctorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public | MethodAttributes.HideBySig, CallingConventions.HasThis,
                                                                       new[] { typeof(ScriptEngine), typeof(object) });
        ILGenerator ctorGen = ctorBuilder.GetILGenerator();

        ctorGen.Emit(OpCodes.Ldarg_0); // # this
        ctorGen.Emit(OpCodes.Ldarg_1); // > engine
        ctorGen.Emit(OpCodes.Call, ReflectionCache.ObjectInstance__ctor__ScriptEngine); // #:base(<)
        ctorGen.Emit(OpCodes.Ldarg_0); // # this
        ctorGen.Emit(OpCodes.Ldtoken, typeBuilder); // >[__this__]
        ctorGen.Emit(OpCodes.Call, ReflectionCache.Type__GetTypeFromHandle__RuntimeTypeHandle); // > typeof(<[__this__])
        ctorGen.Emit(OpCodes.Ldc_I4, (int)(BindingFlags.Public | BindingFlags.Instance /*| BindingFlags.DeclaredOnly*/)); // > flags
        ctorGen.Emit(OpCodes.Call, ReflectionCache.ObjectInstance__PopulateFunctions__Type_BindingFlags); // #.PopulateFunctions(<, <);
        ctorGen.Emit(OpCodes.Ldarg_0); // # this
        ctorGen.Emit(OpCodes.Ldarg_2); // > instance
        ctorGen.Emit(OpCodes.Stfld, fldInstance); // #.realInstance = <
        ctorGen.Emit(OpCodes.Ret);

        // public ... Method(...)
        MethodInfo[] miInsts = type.GetMethods(BindingFlags.Public | BindingFlags.Instance /*| BindingFlags.DeclaredOnly*/);
        List<string> methodNames = new List<string>();
        foreach (MethodInfo miInst in miInsts)
        {
          if (methodNames.Contains(miInst.Name)) continue;
          else methodNames.Add(miInst.Name);
          Attribute[] infoAttributes = GetAttributes(infos, miInst.Name, typeof(JSFunctionAttribute));
          if (!Attribute.IsDefined(miInst, typeof(JSFunctionAttribute)) && infoAttributes.Length == 0) continue;
          MethodBuilder proxyInst = typeBuilder.DefineMethod(miInst.Name, miInst.Attributes);
          proxyInst.SetReturnType(GetConvertOrWrapType(miInst.ReturnType));
          proxyInst.CopyParametersFrom(miInst);
          proxyInst.CopyCustomAttributesFrom(miInst, infoAttributes);
          ILGenerator methodGen = proxyInst.GetILGenerator();

          methodGen.Emit(OpCodes.Ldarg_0); // # this
          methodGen.Emit(OpCodes.Ldfld, fldInstance); // > #.realInstance
          ParameterInfo[] parameterInfos = miInst.GetParameters();
          for (int i = 0; i < parameterInfos.Length; i++)
          {
            methodGen.Emit(OpCodes.Ldarg, i + 1); // > arg*
            if (!Attribute.IsDefined(parameterInfos[i], typeof(ParamArrayAttribute))) EmitConvertOrUnwrap(methodGen, parameterInfos[i].ParameterType);
          }
          methodGen.Emit(miInst.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, miInst); // >? <.Method(<*)
          EmitConvertOrWrap(methodGen, miInst.ReturnType);
          methodGen.Emit(OpCodes.Ret);
        }

        // public ... Property
        PropertyInfo[] piInsts = type.GetProperties(BindingFlags.Public | BindingFlags.Instance /*| BindingFlags.DeclaredOnly*/);
        foreach (PropertyInfo piInst in piInsts)
        {
          Attribute[] infoAttributes = GetAttributes(infos, piInst.Name, typeof(JSPropertyAttribute));
          if (!Attribute.IsDefined(piInst, typeof(JSPropertyAttribute)) && infoAttributes.Length == 0) continue;
          MethodInfo piInstGet = piInst.GetGetMethod();
          MethodInfo piInstSet = piInst.GetSetMethod();
          if (piInstGet == null && piInstSet == null) continue;
          PropertyBuilder proxyInstance = typeBuilder.DefineProperty(piInst.Name, piInst.Attributes, GetConvertOrWrapType(piInst.PropertyType), null);
          proxyInstance.CopyCustomAttributesFrom(piInst, infoAttributes);
          if (piInstGet != null && !methodNames.Contains(piInstGet.Name))
          {
            methodNames.Add(piInstGet.Name);
            MethodBuilder proxyInstanceGet = typeBuilder.DefineMethod(piInstGet.Name, piInstGet.Attributes);
            proxyInstanceGet.SetReturnType(GetConvertOrWrapType(piInstGet.ReturnType));
            proxyInstanceGet.CopyParametersFrom(piInstGet);
            proxyInstanceGet.CopyCustomAttributesFrom(piInstGet);
            ILGenerator getGen = proxyInstanceGet.GetILGenerator();

            getGen.Emit(OpCodes.Ldarg_0); // # this
            getGen.Emit(OpCodes.Ldfld, fldInstance); // > #.realInstance
            ParameterInfo[] parameterInfos = piInstGet.GetParameters();
            for (int i = 0; i < parameterInfos.Length; i++)
            {
              getGen.Emit(OpCodes.Ldarg, i + 1); // > arg*
              if (!Attribute.IsDefined(parameterInfos[i], typeof(ParamArrayAttribute))) EmitConvertOrUnwrap(getGen, parameterInfos[i].ParameterType);
            }
            getGen.Emit(piInstGet.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, piInstGet); // <.Property <*
            EmitConvertOrWrap(getGen, piInstGet.ReturnType);
            getGen.Emit(OpCodes.Ret);
            proxyInstance.SetGetMethod(proxyInstanceGet);
          }
          if (piInstSet != null && !methodNames.Contains(piInstSet.Name))
          {
            methodNames.Add(piInstSet.Name);
            MethodBuilder proxyInstanceSet = typeBuilder.DefineMethod(piInstSet.Name, piInstSet.Attributes);
            proxyInstanceSet.SetReturnType(piInstSet.ReturnType);
            proxyInstanceSet.CopyParametersFrom(piInstSet);
            proxyInstanceSet.CopyCustomAttributesFrom(piInstSet);
            ILGenerator setGen = proxyInstanceSet.GetILGenerator();

            setGen.Emit(OpCodes.Ldarg_0); // # this
            setGen.Emit(OpCodes.Ldfld, fldInstance); // > #.realInstance
            ParameterInfo[] parameterInfos = piInstSet.GetParameters();
            for (int i = 0; i < parameterInfos.Length; i++)
            {
              setGen.Emit(OpCodes.Ldarg, i + 1); // > arg*
              if (!Attribute.IsDefined(parameterInfos[i], typeof(ParamArrayAttribute))) EmitConvertOrUnwrap(setGen, parameterInfos[i].ParameterType);
            }
            setGen.Emit(piInstSet.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, piInstSet); // <.Property = <*
            setGen.Emit(OpCodes.Ret);
            proxyInstance.SetSetMethod(proxyInstanceSet);
          }
        }

        // public event ...
        EventInfo[] eiInsts = type.GetEvents(BindingFlags.Public | BindingFlags.Instance /*| BindingFlags.DeclaredOnly*/);
        foreach (EventInfo eventInfo in eiInsts)
        {
          Attribute[] infoAttributes = GetAttributes(infos, eventInfo.Name, typeof(JSEventAttribute));
          if (!Attribute.IsDefined(eventInfo, typeof(JSEventAttribute)) && infoAttributes.Length == 0) continue;
          JSEventAttribute eventAttribute = (JSEventAttribute)Attribute.GetCustomAttribute(eventInfo, typeof(JSEventAttribute));
          if (eventAttribute == null) eventAttribute = (JSEventAttribute)infoAttributes.FirstOrDefault(a => a is JSEventAttribute);
          MethodInfo eiAdd = eventInfo.GetAddMethod();
          if (eventAttribute == null) eventAttribute = new JSEventAttribute();
          string addName = eventAttribute.AddPrefix + (eventAttribute.Name ?? eventInfo.Name);
          MethodBuilder proxyAdd = typeBuilder.DefineMethod(addName, eiAdd.Attributes);
          proxyAdd.SetReturnType(eiAdd.ReturnType);
          proxyAdd.CopyParametersFrom(eiAdd);
          proxyAdd.CopyCustomAttributesFrom(eiAdd, new JSFunctionAttribute());
          ILGenerator ilAdd = proxyAdd.GetILGenerator();
          ilAdd.Emit(OpCodes.Ldarg_0); // # this
          ilAdd.Emit(OpCodes.Ldfld, fldInstance); // > #.realInstance
          ParameterInfo[] parameterInfos = eiAdd.GetParameters();
          for (int i = 0; i < parameterInfos.Length; i++)
          {
            ilAdd.Emit(OpCodes.Ldarg, i + 1); // > arg*
            if (!Attribute.IsDefined(parameterInfos[i], typeof(ParamArrayAttribute))) EmitConvertOrUnwrap(ilAdd, parameterInfos[i].ParameterType);
          }
          ilAdd.Emit(eiAdd.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, eiAdd);
          ilAdd.Emit(OpCodes.Ret);

          MethodInfo eiRemove = eventInfo.GetRemoveMethod();
          string removeName = eventAttribute.RemovePrefix + (eventAttribute.Name ?? eventInfo.Name);
          MethodBuilder proxyRemove = typeBuilder.DefineMethod(removeName, eiRemove.Attributes);
          proxyRemove.SetReturnType(eiRemove.ReturnType);
          proxyRemove.CopyParametersFrom(eiRemove);
          proxyRemove.CopyCustomAttributesFrom(eiRemove, new JSFunctionAttribute());
          ILGenerator ilRemove = proxyRemove.GetILGenerator();
          ilRemove.Emit(OpCodes.Ldarg_0); // # this
          ilRemove.Emit(OpCodes.Ldfld, fldInstance); // > #.realInstance
          parameterInfos = eiRemove.GetParameters();
          for (int i = 0; i < parameterInfos.Length; i++)
          {
            ilRemove.Emit(OpCodes.Ldarg, i + 1); // > arg*
            if (!Attribute.IsDefined(parameterInfos[i], typeof(ParamArrayAttribute))) EmitConvertOrUnwrap(ilRemove, parameterInfos[i].ParameterType);
          }
          ilRemove.Emit(eiRemove.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, eiRemove);
          ilRemove.Emit(OpCodes.Ret);
        }

        proxiedType = typeBuilder.CreateType();
        InstanceProxyCache[type] = proxiedType;
      }

      ObjectInstance proxiedInstance = (ObjectInstance)Activator.CreateInstance(proxiedType, engine, instance);
      return proxiedInstance;
    }

    private static Attribute[] GetAttributes(JurassicInfo[] infos, string name, Type attributeType = null)
    {
      return (infos == null || infos.Length == 0)
               ? new Attribute[0]
               : infos.Where(i => String.Equals(i.MemberName, name))
                      .SelectMany(i => i.Attributes.Where(a => attributeType == null || a.GetType() == attributeType))
                      .ToArray();
    }
  }
}
