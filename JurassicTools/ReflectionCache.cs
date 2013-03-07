using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Jurassic;
using Jurassic.Library;

namespace JurassicTools
{
  // ReSharper disable InconsistentNaming
  internal static class ReflectionCache
  {
    /// <summary>
    ///   protected ClrFunction(ObjectInstance prototype, string name, ObjectInstance instancePrototype)
    /// </summary>
    public static readonly ConstructorInfo ClrFunction__ctor__ObjectInstance_String_ObjectInstance =
      typeof(ClrFunction).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
                                         new[] { typeof(ObjectInstance), typeof(String), typeof(ObjectInstance) }, null);

    /// <summary>
    ///   protected ObjectInstance(ScriptEngine engine)
    /// </summary>
    public static readonly ConstructorInfo ObjectInstance__ctor__ScriptEngine =
      typeof(ObjectInstance).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(ScriptEngine) }, null);

    /// <summary>
    ///   [JSConstructorFunction]
    /// </summary>
    public static readonly ConstructorInfo JSConstructorFunctionAttribute__ctor =
      typeof(JSConstructorFunctionAttribute).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, new Type[] { }, null);

    /// <summary>
    ///   [JSField]
    /// </summary>
    public static readonly ConstructorInfo JSField__ctor = typeof(JSFieldAttribute).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null,
                                                                                                   new Type[] { }, null);

    /// <summary>
    ///   params
    /// </summary>
    public static readonly ConstructorInfo ParamArrayAttribute__ctor = typeof(ParamArrayAttribute).GetConstructor(BindingFlags.Instance | BindingFlags.Public,
                                                                                                                  null, new Type[] { }, null);

    /// <summary>
    ///   public FunctionConstructor Function get
    /// </summary>
    public static readonly MethodInfo ScriptEngine__get_Function = typeof(ScriptEngine).GetMethod("get_Function", BindingFlags.Instance | BindingFlags.Public,
                                                                                                  null, new Type[] { }, null);

    /// <summary>
    ///   public ObjectInstance InstancePrototype get
    /// </summary>
    public static readonly MethodInfo FunctionInstance__get_InstancePrototype = typeof(FunctionInstance).GetMethod("get_InstancePrototype",
                                                                                                                   BindingFlags.Instance | BindingFlags.Public,
                                                                                                                   null, new Type[] { }, null);

    /// <summary>
    ///   public object CallLateBound(object thisObject, params object[] argumentValues)
    /// </summary>
    public static readonly MethodInfo FunctionInstance__CallLateBound__Object_aObject = typeof(FunctionInstance).GetMethod("CallLateBound",
                                                                                                                           BindingFlags.Instance |
                                                                                                                           BindingFlags.Public, null,
                                                                                                                           new[]
                                                                                                                           { typeof(object), typeof(object[]) },
                                                                                                                           null);

    /// <summary>
    ///   public ObjectConstructor Object get
    /// </summary>
    public static readonly MethodInfo ScriptEngine__get_Object = typeof(ScriptEngine).GetMethod("get_Object", BindingFlags.Instance | BindingFlags.Public, null,
                                                                                                new Type[] { }, null);

    /// <summary>
    ///   internal protected void PopulateFunctions(Type type, BindingFlags bindingFlags)
    /// </summary>
    public static readonly MethodInfo ObjectInstance__PopulateFunctions__Type_BindingFlags = typeof(ObjectInstance).GetMethod("PopulateFunctions",
                                                                                                                              BindingFlags.Instance |
                                                                                                                              BindingFlags.NonPublic, null,
                                                                                                                              new[]
                                                                                                                              {
                                                                                                                                typeof(Type),
                                                                                                                                typeof(BindingFlags)
                                                                                                                              }, null);

    /// <summary>
    ///   internal protected void PopulateFields(Type type)
    /// </summary>
    public static readonly MethodInfo ObjectInstance__PopulateFields__Type = typeof(ObjectInstance).GetMethod("PopulateFields",
                                                                                                              BindingFlags.Instance | BindingFlags.NonPublic,
                                                                                                              null, new[] { typeof(Type) }, null);

    /// <summary>
    ///   typeof(...) aka public static RuntimeTypeHandle GetTypeHandle(Object o)
    /// </summary>
    public static readonly MethodInfo Type__GetTypeFromHandle__RuntimeTypeHandle = typeof(Type).GetMethod("GetTypeFromHandle",
                                                                                                          BindingFlags.Static | BindingFlags.Public, null,
                                                                                                          new[] { typeof(RuntimeTypeHandle) }, null);

    /// <summary>
    ///   public ScriptEngine Engine get
    /// </summary>
    public static readonly MethodInfo ObjectInstance__get_Engine = typeof(ObjectInstance).GetMethod("get_Engine", BindingFlags.Instance | BindingFlags.Public,
                                                                                                    null, new Type[] { }, null);

    /// <summary>
    ///   static public Object CreateInstance(Type type, params Object[] args)
    /// </summary>
    public static readonly MethodInfo Activator__CreateInstance__Type_aObject = typeof(Activator).GetMethod("CreateInstance",
                                                                                                            BindingFlags.Static | BindingFlags.Public, null,
                                                                                                            new[] { typeof(Type), typeof(Object[]) }, null);

    /// <summary>
    ///   public static ObjectInstance WrapObject(object instance, ScriptEngine engine, params JurassicInfo[] infos)
    /// </summary>
    public static readonly MethodInfo JurassicExposer__WrapObject__Object_ScriptEngine = typeof(JurassicExposer).GetMethod("WrapObject",
                                                                                                                           BindingFlags.Static |
                                                                                                                           BindingFlags.Public, null,
                                                                                                                           new[]
                                                                                                                           {
                                                                                                                             typeof(Object),
                                                                                                                             typeof(ScriptEngine)
                                                                                                                           }, null);

    /// <summary>
    ///   public static object ConvertOrWrapObject(object instance, ScriptEngine engine)
    /// </summary>
    public static readonly MethodInfo JurassicExposer__ConvertOrWrapObject__Object_ScriptEngine = typeof(JurassicExposer).GetMethod("ConvertOrWrapObject",
                                                                                                                                    BindingFlags.Static |
                                                                                                                                    BindingFlags.Public, null,
                                                                                                                                    new[]
                                                                                                                                    {
                                                                                                                                      typeof(object),
                                                                                                                                      typeof(ScriptEngine)
                                                                                                                                    },
                                                                                                                                    null);

    /// <summary>
    ///   public static object ConvertOrUnwrapObject(object instance, Type origType)
    /// </summary>
    public static readonly MethodInfo JurassicExposer__ConvertOrUnwrapObject__Object_Type = typeof(JurassicExposer).GetMethod("ConvertOrUnwrapObject",
                                                                                                                                      BindingFlags.Static |
                                                                                                                                      BindingFlags.Public, null,
                                                                                                                                      new[]
                                                                                                                                      {
                                                                                                                                        typeof(object),
                                                                                                                                        typeof(Type)
                                                                                                                                      },
                                                                                                                                      null);

    /// <summary>
    ///   internal static FunctionInstance GetFunction(long index)
    /// </summary>
    public static readonly MethodInfo JurassicExposer__GetFunction__long = typeof(JurassicExposer).GetMethod("GetFunction",
                                                                                                             BindingFlags.Static | BindingFlags.NonPublic, null,
                                                                                                             new[] { typeof(long) }, null);
  }

  // ReSharper restore InconsistentNaming
}
