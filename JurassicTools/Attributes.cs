using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jurassic.Library;

namespace JurassicTools
{
  [AttributeUsage(AttributeTargets.Event, AllowMultiple = false)]
  public class JSEventAttribute : Attribute
  {
    public string Name { get; set; }

    public string AddPrefix { get; set; }
    public string RemovePrefix { get; set; }

    public JSEventAttribute()
    {
      AddPrefix = "add_";
      RemovePrefix = "remove_";
    }
  }

  // because Jurassic's one doesn't have a parameterless constructor for easy copying in reflection
  [AttributeUsage(AttributeTargets.Parameter)]
  public class JurassicToolsDefaultValue : DefaultParameterValueAttribute
  {
    public JurassicToolsDefaultValue()
      : base(null) {}

    private object _value;

    public new object Value
    {
      get { return _value; }
      set {
        _value = value;
        typeof(DefaultParameterValueAttribute).GetProperty("Value").GetSetMethod(true).Invoke(this, new[] { value });
      }
    }
  }
}
