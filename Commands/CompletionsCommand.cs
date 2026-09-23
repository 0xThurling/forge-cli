using System.Text;
using DotMake.CommandLine;
using Spectre.Console;

namespace forge.Commands
{
  /// <summary>
  /// Prints a shell completion script for the CLI.
  /// </summary>
  /// <remarks>
  /// The script completes the root commands, their options, and the
  /// subcommands of <c>project</c> and <c>new</c>. Install it with, for
  /// example, <c>forge completions bash &gt; /etc/bash_completion.d/forge</c>.
  /// </remarks>
  [CliCommand(Name = "completions", Description = "Print a shell completion script (bash, zsh or fish).", Parent = typeof(RootCommand))]
  public class CompletionsCommand
  {
    [CliArgument(Description = "Shell: bash, zsh or fish.")]
    public string Shell { get; set; } = "bash";

    private static readonly string[] RootCommands =
    [
      "add", "bench", "build", "cache", "ci", "clean", "completions", "config",
      "create", "doctor", "download", "embed", "extract", "fetch", "format",
      "init", "install", "lint", "new", "outdated", "project", "publish",
      "remove", "run", "setup", "test", "upgrade", "vendor", "watch", "why",
      "workspace"
    ];

    private static readonly string[] ProjectSubcommands =
      ["dependencies", "info", "scripts", "stats", "tree"];

    private static readonly string[] NewSubcommands =
      ["class", "header", "source", "struct"];

    private static readonly string[] WorkspaceSubcommands =
      ["build", "list", "test"];

    private static readonly string[] BuildOptions =
      ["--verbose", "--standard", "--release", "--debug", "--preset", "--no-config-presets", "--jobs", "--help"];

    public Task<int> RunAsync()
    {
      var script = Shell.ToLowerInvariant() switch
      {
        "bash" => Bash(),
        "zsh" => Zsh(),
        "fish" => Fish(),
        _ => null
      };

      if (script is null)
      {
        AnsiConsole.MarkupLine($"[bold red]Error:[/] unsupported shell `{Shell}`. Use bash, zsh or fish.");
        return Task.FromResult(1);
      }

      Console.WriteLine(script);
      return Task.FromResult(0);
    }

    private static string Bash()
    {
      var sb = new StringBuilder();
      sb.AppendLine("# bash completion for forge — install with:");
      sb.AppendLine("#   forge completions bash > /etc/bash_completion.d/forge");
      sb.AppendLine("_forge() {");
      sb.AppendLine("  local cur prev words");
      sb.AppendLine("  COMPREPLY=()");
      sb.AppendLine("  cur=\"${COMP_WORDS[COMP_CWORD]}\"");
      sb.AppendLine("  prev=\"${COMP_WORDS[COMP_CWORD-1]}\"");
      sb.AppendLine($"  local root=\"{string.Join(' ', RootCommands)}\"");
      sb.AppendLine($"  local project=\"{string.Join(' ', ProjectSubcommands)}\"");
      sb.AppendLine($"  local newcmd=\"{string.Join(' ', NewSubcommands)}\"");
      sb.AppendLine($"  local workspace=\"{string.Join(' ', WorkspaceSubcommands)}\"");
      sb.AppendLine($"  local options=\"{string.Join(' ', BuildOptions)}\"");
      sb.AppendLine("  if [[ $COMP_CWORD -eq 1 ]]; then");
      sb.AppendLine("    COMPREPLY=( $(compgen -W \"$root\" -- \"$cur\") )");
      sb.AppendLine("    return 0");
      sb.AppendLine("  fi");
      sb.AppendLine("  case \"${COMP_WORDS[1]}\" in");
      sb.AppendLine("    project) COMPREPLY=( $(compgen -W \"$project\" -- \"$cur\") ) ;;");
      sb.AppendLine("    new)     COMPREPLY=( $(compgen -W \"$newcmd\" -- \"$cur\") ) ;;");
      sb.AppendLine("    workspace) COMPREPLY=( $(compgen -W \"$workspace\" -- \"$cur\") ) ;;");
      sb.AppendLine("    build|test) COMPREPLY=( $(compgen -W \"$options\" -- \"$cur\") ) ;;");
      sb.AppendLine("    run)     COMPREPLY=( $(compgen -W \"$(forge project scripts 2>/dev/null | grep -oE '^[a-z0-9_-]+' | tr '\\n' ' ')\" -- \"$cur\") ) ;;");
      sb.AppendLine("    completions) COMPREPLY=( $(compgen -W \"bash zsh fish\" -- \"$cur\") ) ;;");
      sb.AppendLine("  esac");
      sb.AppendLine("  return 0");
      sb.AppendLine("}");
      sb.AppendLine("complete -F _forge forge");
      return sb.ToString();
    }

    private static string Zsh()
    {
      var sb = new StringBuilder();
      sb.AppendLine("#compdef forge");
      sb.AppendLine("# zsh completion for forge — install with:");
      sb.AppendLine("#   forge completions zsh > \"${fpath[1]}/_forge\"");
      sb.AppendLine("_forge() {");
      sb.AppendLine("  local -a commands project newcmd");
      sb.AppendLine($"  commands=({string.Join(' ', RootCommands)})");
      sb.AppendLine($"  project=({string.Join(' ', ProjectSubcommands)})");
      sb.AppendLine($"  newcmd=({string.Join(' ', NewSubcommands)})");
      sb.AppendLine($"  workspacecmd=({string.Join(' ', WorkspaceSubcommands)})");
      sb.AppendLine("  if (( CURRENT == 2 )); then");
      sb.AppendLine("    _describe 'command' commands");
      sb.AppendLine("    return");
      sb.AppendLine("  fi");
      sb.AppendLine("  case $words[2] in");
      sb.AppendLine("    project) _describe 'subcommand' project ;;");
      sb.AppendLine("    new) _describe 'subcommand' newcmd ;;");
      sb.AppendLine("    workspace) _describe 'subcommand' workspacecmd ;;");
      sb.AppendLine("    completions) _values 'shell' bash zsh fish ;;");
      sb.AppendLine("  esac");
      sb.AppendLine("}");
      sb.AppendLine("_forge \"$@\"");
      return sb.ToString();
    }

    private static string Fish()
    {
      var sb = new StringBuilder();
      sb.AppendLine("# fish completion for forge — install with:");
      sb.AppendLine("#   forge completions fish > ~/.config/fish/completions/forge.fish");
      foreach (var command in RootCommands)
        sb.AppendLine($"complete -c forge -n __fish_use_subcommand -a {command}");
      foreach (var sub in ProjectSubcommands)
        sb.AppendLine($"complete -c forge -n '__fish_seen_subcommand_from project' -a {sub}");
      foreach (var sub in NewSubcommands)
        sb.AppendLine($"complete -c forge -n '__fish_seen_subcommand_from new' -a {sub}");
      foreach (var sub in WorkspaceSubcommands)
        sb.AppendLine($"complete -c forge -n '__fish_seen_subcommand_from workspace' -a {sub}");
      foreach (var option in BuildOptions)
        sb.AppendLine($"complete -c forge -n '__fish_seen_subcommand_from build test' -l {option.TrimStart('-')}");
      return sb.ToString();
    }
  }
}
