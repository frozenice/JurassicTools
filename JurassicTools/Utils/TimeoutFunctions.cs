using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jurassic;
using Jurassic.Library;

namespace JurassicTools.Utils
{
  public class TimeoutFunctions : ObjectInstance
  {
    private static ConcurrentDictionary<ScriptEngine, int> timerIds = new ConcurrentDictionary<ScriptEngine, int>();

    private static ConcurrentDictionary<Tuple<ScriptEngine, int>, CancellationTokenSource> cancelTokens =
      new ConcurrentDictionary<Tuple<ScriptEngine, int>, CancellationTokenSource>();

    private TimeoutFunctions(ScriptEngine engine)
      : base(engine) {}

    public static void Expose(ScriptEngine engine)
    {
      TimeoutFunctions tf = new TimeoutFunctions(engine);
      tf.PopulateFunctions();
      engine.SetGlobalValue("sleep", tf["sleep"]);
      engine.SetGlobalValue("setTimeout", tf["setTimeout"]);
      engine.SetGlobalValue("clearTimeout", tf["clearTimeout"]);
      engine.SetGlobalValue("setInterval", tf["setInterval"]);
      engine.SetGlobalValue("clearInterval", tf["clearInterval"]);
    }

    [JSFunction(Name = "sleep")]
    public static void Sleep(int timeout)
    {
      Thread.Sleep(timeout);
    }

    [JSFunction(Name = "setTimeout", Flags = JSFunctionFlags.HasEngineParameter)]
    public static int SetTimeout(ScriptEngine engine, FunctionInstance function, int timeout, params object[] args)
    {
      int newId = timerIds.AddOrUpdate(engine, 1, (scriptEngine, i) => i + 1);
      CancellationTokenSource cts = new CancellationTokenSource();
      cancelTokens.TryAdd(new Tuple<ScriptEngine, int>(engine, newId), cts);
      Task task = new Task(() =>
      {
        cts.Token.WaitHandle.WaitOne(timeout);
        cts.Token.ThrowIfCancellationRequested();
        new Thread(() => function.Call(function.Engine.Global, args)).Start();
      }, cts.Token);
      task.Start();
      return newId;
    }

    [JSFunction(Name = "clearTimeout", Flags = JSFunctionFlags.HasEngineParameter)]
    public static void ClearTimeout(ScriptEngine engine, int id)
    {
      CancellationTokenSource cts;
      if (cancelTokens.TryGetValue(new Tuple<ScriptEngine, int>(engine, id), out cts)) cts.Cancel();
    }

    [JSFunction(Name = "setInterval", Flags = JSFunctionFlags.HasEngineParameter)]
    public static int SetInterval(ScriptEngine engine, FunctionInstance function, int timeout, params object[] args)
    {
      int newId = timerIds.AddOrUpdate(engine, 1, (scriptEngine, i) => i + 1);
      CancellationTokenSource cts = new CancellationTokenSource();
      cancelTokens.TryAdd(new Tuple<ScriptEngine, int>(engine, newId), cts);
      InternalSetInterval(cts, engine, function, timeout, args);
      return newId;
    }

    private static void InternalSetInterval(CancellationTokenSource cts, ScriptEngine engine, FunctionInstance function, int timeout, object[] args)
    {
      Task task = new Task(() =>
      {
        cts.Token.WaitHandle.WaitOne(timeout);
        cts.Token.ThrowIfCancellationRequested();
        new Thread(() => function.Call(function.Engine.Global, args)).Start();
        InternalSetInterval(cts, engine, function, timeout, args);
      }, cts.Token);
      task.Start();
    }

    [JSFunction(Name = "clearInterval", Flags = JSFunctionFlags.HasEngineParameter)]
    public static void ClearInterval(ScriptEngine engine, int id)
    {
      CancellationTokenSource cts;
      if (cancelTokens.TryGetValue(new Tuple<ScriptEngine, int>(engine, id), out cts)) cts.Cancel();
    }
  }
}
