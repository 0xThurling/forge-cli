using Lua;

namespace forge.Commands.Lua;

public abstract class LuaFunctionModule
{
  public abstract string ModuleName { get; }
  public abstract void RegisterFunctions(Dictionary<LuaValue, LuaValue> table); 
}

// TODO: Come back and implement this 
public class CoreFunctionModule : LuaFunctionModule
{
    public override string ModuleName => "forge";
    public override void RegisterFunctions(Dictionary<LuaValue, LuaValue> table)
    {}
}
