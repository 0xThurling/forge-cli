using System.Diagnostics;
using System.Text;
using forge.ForgeEngine.CoreUtils;
using Lua;
using Spectre.Console;

namespace forge.Commands.Lua;

/// <summary>
/// The workflow half of the <c>forge</c> Lua API: running programs and reading
/// their output, touching files, rendering templates, and reading Git state.
/// </summary>
/// <remarks>
/// The Lua runtime Forge embeds has no <c>io.popen</c>, so a script can run a
/// command but not read what it printed. <c>forge.exec</c> fills that gap —
/// the missing piece for code generation that shells out to a compiler or a
/// binding generator.
/// </remarks>
/// <example>
/// <code>
/// -- .config/forge/build/shaders.lua
/// local code, output = forge.exec("glslangValidator -V shader.vert")
/// if code ~= 0 then error("shader compilation failed:\n" .. output) end
///
/// forge.write_file("src/shaders.h", "#pragma once\n")
/// forge.template("templates/version.h.in", "src/version.h", { VERSION = forge.git.describe() })
/// </code>
/// </example>
public class LuaWorkflowModule : LuaFunctionModule
{
  public override string ModuleName => "forge";

  public override void RegisterFunctions(ref LuaTable table)
  {
    table[new LuaValue("exec")] = new LuaValue(CreateExecFunction());
    table[new LuaValue("read_file")] = new LuaValue(CreateReadFileFunction());
    table[new LuaValue("write_file")] = new LuaValue(CreateWriteFileFunction());
    table[new LuaValue("copy_file")] = new LuaValue(CreateCopyFileFunction());
    table[new LuaValue("mkdir")] = new LuaValue(CreateMkdirFunction());
    table[new LuaValue("template")] = new LuaValue(CreateTemplateFunction());

    var git = new LuaTable();
    git[new LuaValue("describe")] = new LuaValue(CreateGitValue(GitInfo.Describe));
    git[new LuaValue("rev")] = new LuaValue(CreateGitValue(GitInfo.Rev));
    git[new LuaValue("tag")] = new LuaValue(CreateGitValue(GitInfo.Tag));
    git[new LuaValue("branch")] = new LuaValue(CreateGitValue(GitInfo.Branch));
    git[new LuaValue("dirty")] = new LuaValue(CreateGitDirtyFunction());
    table[new LuaValue("git")] = new LuaValue(git);
  }

  // forge.exec(command) -> exit_code, output
  private static LuaFunction CreateExecFunction() =>
    new("exec", async (context, token) =>
    {
      if (context.ArgumentCount == 0)
      {
        AnsiConsole.MarkupLine("[bold red]Error:[/] forge.exec needs a command.");
        return context.Return(new LuaValue(1), new LuaValue(string.Empty));
      }

      var command = context.GetArgument<string>(0);
      try
      {
        var startInfo = new ProcessStartInfo("bash")
        {
          UseShellExecute = false,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(command);

        using var process = Process.Start(startInfo);
        if (process == null)
        {
          return context.Return(new LuaValue(1), new LuaValue("could not start bash"));
        }

        var output = new StringBuilder();
        var stdout = process.StandardOutput.ReadToEndAsync(token);
        var stderr = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        output.Append(await stdout);
        output.Append(await stderr);

        var text = output.ToString().TrimEnd('\n', '\r');
        AnsiConsole.MarkupLine($"[dim]exec: {command} → exit {process.ExitCode}[/]");

        // Two values must be returned together: the one-value overload resets
        // the return frame, so a second call would replace the first.
        return context.Return(new LuaValue(process.ExitCode), new LuaValue(text));
      }
      catch (Exception exception)
      {
        AnsiConsole.MarkupLine($"[bold red]Error:[/] forge.exec failed: {exception.Message}");
        return context.Return(new LuaValue(1), new LuaValue(exception.Message));
      }
    });

  // forge.read_file(path) -> contents | nil
  private static LuaFunction CreateReadFileFunction() =>
    new("read_file", (context, _) =>
    {
      var path = context.GetArgument<string>(0);
      if (!File.Exists(path))
      {
        AnsiConsole.MarkupLine($"[bold red]Error:[/] forge.read_file: '{path}' does not exist.");
        context.Return(LuaValue.Nil);
        return ValueTask.FromResult(1);
      }

      context.Return(new LuaValue(File.ReadAllText(path)));
      return ValueTask.FromResult(1);
    });

  // forge.write_file(path, contents) -> path
  private static LuaFunction CreateWriteFileFunction() =>
    new("write_file", (context, _) =>
    {
      var path = context.GetArgument<string>(0);
      var contents = context.ArgumentCount > 1 ? context.GetArgument<string>(1) : string.Empty;
      WriteAllText(path, contents);
      AnsiConsole.MarkupLine($"[green]Wrote[/] [dim]{path}[/]");
      context.Return(new LuaValue(path));
      return ValueTask.FromResult(1);
    });

  // forge.copy_file(source, destination) -> destination
  private static LuaFunction CreateCopyFileFunction() =>
    new("copy_file", (context, _) =>
    {
      var source = context.GetArgument<string>(0);
      var destination = context.GetArgument<string>(1);
      var directory = Path.GetDirectoryName(Path.GetFullPath(destination));
      if (!string.IsNullOrEmpty(directory))
        Directory.CreateDirectory(directory);
      File.Copy(source, destination, overwrite: true);
      AnsiConsole.MarkupLine($"[green]Copied[/] [dim]{source} → {destination}[/]");
      context.Return(new LuaValue(destination));
      return ValueTask.FromResult(1);
    });

  // forge.mkdir(path) -> path
  private static LuaFunction CreateMkdirFunction() =>
    new("mkdir", (context, _) =>
    {
      var path = context.GetArgument<string>(0);
      Directory.CreateDirectory(path);
      context.Return(new LuaValue(path));
      return ValueTask.FromResult(1);
    });

  // forge.template(source, destination, values) -> destination
  private static LuaFunction CreateTemplateFunction() =>
    new("template", (context, _) =>
    {
      var source = context.GetArgument<string>(0);
      var destination = context.GetArgument<string>(1);
      var values = context.ArgumentCount > 2 && context.GetArgument<LuaTable>(2) is { } table
        ? table
        : new LuaTable();

      if (!File.Exists(source))
      {
        AnsiConsole.MarkupLine($"[bold red]Error:[/] forge.template: '{source}' does not exist.");
        context.Return(LuaValue.Nil);
        return ValueTask.FromResult(1);
      }

      var contents = File.ReadAllText(source);
      foreach (var (key, value) in values)
        contents = contents.Replace($"@{key}@", value.ToString());

      WriteAllText(destination, contents);
      AnsiConsole.MarkupLine($"[green]Rendered[/] [dim]{source} → {destination}[/]");
      context.Return(new LuaValue(destination));
      return ValueTask.FromResult(1);
    });

  private static LuaFunction CreateGitValue(Func<string, string?> read) =>
    new("git", (context, _) =>
    {
      var value = read(Directory.GetCurrentDirectory());
      context.Return(value is null ? LuaValue.Nil : new LuaValue(value));
      return ValueTask.FromResult(1);
    });

  private static LuaFunction CreateGitDirtyFunction() =>
    new("dirty", (context, _) =>
    {
      context.Return(new LuaValue(GitInfo.Dirty(Directory.GetCurrentDirectory())));
      return ValueTask.FromResult(1);
    });

  private static void WriteAllText(string path, string contents)
  {
    var directory = Path.GetDirectoryName(Path.GetFullPath(path));
    if (!string.IsNullOrEmpty(directory))
      Directory.CreateDirectory(directory);
    File.WriteAllText(path, contents, Encoding.UTF8);
  }
}
