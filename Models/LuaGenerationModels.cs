using System.Text;

namespace forge.Models;

public class LuaDefinitionCategory
{
  private readonly string _name;
  private readonly string _description;
  private readonly List<LuaParameter> _parameters = [];
  private readonly List<LuaParameterReturn> _returns = [];
  private readonly Dictionary<string, string> _constants = [];

  // Accepts both methods
  public LuaDefinitionCategory(
      string name,
      string description,
      params LuaParameter[] parameters)
  {
    _name = name;
    _description = description;
    _parameters.AddRange(parameters);
  }

  // Constructor Overload
  public LuaDefinitionCategory(
      string name,
      string description,
      LuaParameterReturn? returnType,
      params LuaParameter[] parameters)
    : this(name, description, parameters)
  {
    if (returnType != null) _returns.Add(returnType);
  }

  public LuaDefinitionCategory AddReturn(LuaParameterReturn returnParam)
  {
    _returns.Add(returnParam);
    return this;
  }

  public LuaDefinitionCategory AddConstantInformation(string type, string name)
  {
    // Limit this constants by 1
    if (_constants.Count > 0) return this;

    _constants.Add(type, name);
    return this;
  }

  public string ToDefinition()
  {
    var sb = new StringBuilder();

    sb.AppendLine($"--- {_description}");

    foreach (var param in _parameters)
    {
      if (!string.IsNullOrEmpty(param.Description))
      {
        sb.AppendLine($"---@param {param.Name} {param.Type} {param.Description}");
      }
      else
      {
        sb.AppendLine($"---@param {param.Name} {param.Type}");
      }
    }

    foreach (var ret in _returns)
    {
      if (!string.IsNullOrEmpty(ret.Description))
      {
        sb.AppendLine($"---@return {ret.Type} {ret.Description}");
      }
      else
      {
        sb.AppendLine($"---@return {ret.Type}");
      }
    }

    foreach (var kv in _constants)
    {
      sb.AppendLine($"---@{kv.Key} {kv.Value}");

      if (kv.Key == "class")
      {
        sb.AppendLine($"{_name} = {{}}");
      }
      else if (kv.Key == "type" && kv.Value == "string")
      {
        sb.AppendLine($"{_name} = \"\"");
      }
    }

    var paramNames = string.Join(", ", _parameters.Select(p => p.Name));

    if (_name.Contains('.'))
    {
      var parts = _name.Split('.');
      var funcName = parts[^1];
      var prefix = string.Join(".", parts[..^1]);
      sb.AppendLine($"function {prefix}.{funcName}({paramNames}) end");
    }
    else
    {
      sb.AppendLine($"function {_name}({paramNames}) end");
    }

    sb.AppendLine();
    return sb.ToString();
  }
}

public class LuaParameter(string name, string type, string description = "")
{
  public string Name { get; set; } = name;
  public string Type { get; set; } = type;
  public string Description { get; set; } = description;
}

public class LuaParameterReturn(string type, string description = "")
{
  public string Type { get; set; } = type;
  public string Description { get; set; } = description;
}
