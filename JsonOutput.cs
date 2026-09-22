using System.Text;

namespace forge;

/// <summary>
/// Tiny JSON writer for command output. Hand-rolled on purpose: the CLI is
/// published with AOT, where reflection-based serialization is disabled.
/// </summary>
internal static class JsonOutput
{
  public static string Quote(string value)
  {
    var sb = new StringBuilder(value.Length + 2);
    sb.Append('"');
    foreach (var c in value)
    {
      switch (c)
      {
        case '"': sb.Append("\\\""); break;
        case '\\': sb.Append("\\\\"); break;
        case '\n': sb.Append("\\n"); break;
        case '\r': sb.Append("\\r"); break;
        case '\t': sb.Append("\\t"); break;
        default:
          if (c < 0x20)
            sb.Append("\\u").Append(((int)c).ToString("x4"));
          else
            sb.Append(c);
          break;
      }
    }
    sb.Append('"');
    return sb.ToString();
  }

  public static string Bool(bool value) => value ? "true" : "false";
}
