using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Jurassic;
using Jurassic.Library;

namespace JurassicTools
{
  internal class WrapperFunction : FunctionInstance
  {
    private readonly Delegate _delegate;

    public WrapperFunction(ScriptEngine engine, Delegate dele)
      : base(engine)
    {
      _delegate = dele;
    }

    public override object CallLateBound(object thisObject, params object[] argumentValues)
    {
      object[] args = new object[argumentValues.Length];
      ParameterInfo[] parameterInfos = _delegate.GetType().GetMethod("Invoke").GetParameters();
      Type[] types = parameterInfos.Select(pi => pi.ParameterType).ToArray();
      for (int i = 0; i < argumentValues.Length; i++)
      {
        args[i] = JurassicExposer.ConvertOrUnwrapObject(argumentValues[i], types[i]);
      }
      object ret = _delegate.DynamicInvoke(args);
      return JurassicExposer.ConvertOrWrapObject(ret, Engine);
    }
  }
}
