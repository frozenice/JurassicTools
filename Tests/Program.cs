using System;
using System.Reflection;
using Jurassic;
using Jurassic.Library;
using JurassicTools;

namespace Tests
{
  class Program
  {
    static void Main(string[] args)
    {
      typeof(ScriptEngine).GetField("lowPrivilegeEnvironment", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
      typeof(ScriptEngine).GetField("lowPrivilegeEnvironmentTested", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
      ScriptEngine engine = new ScriptEngine();
      engine.EnableDebugging = true;
      engine.ForceStrictMode = true;

      JurassicExposer.RegisterInfos(typeof(Environment), new JurassicInfo("UserName", new JSPropertyAttribute()));
      JurassicExposer.ExposeClass(typeof(Environment), engine);
      
      JurassicExposer.ExposeFunction(engine, new Action<string>(Console.WriteLine), "log");

      engine.ExecuteFile("test.js");
    }
  }
}
