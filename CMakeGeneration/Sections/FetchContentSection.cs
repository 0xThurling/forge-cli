using System.Text;
using forge.Models;
using Spectre.Console;

namespace forge.CMakeGeneration.Sections;

public class FetchContentSection : CMakeSectionBase
{
  public override string Name => "fetchcontent";

  public override int Priority => 10;

  public override bool IsEnabled(BuildContext context) =>
    context.Config.Dependencies.Count != 0;

  public override string Generate(BuildContext context)
  {
    var config = context.Config;
    var sb = new StringBuilder();

    sb.AppendLine("include(FetchContent)");
    sb.AppendLine("");
    sb.AppendLine("# --- Dependencies ---");

    foreach (var dep in config.Dependencies)
    {
      var name = dep.Key;
      var details = dep.Value;

      // The test framework is fetched where the tests are built (inside the
      // testing block, which a consumer skips), not as a plain dependency.
      // Otherwise every consumer of this project would download it too.
      if (config.Testing && name == "googletest")
        continue;

      // A local checkout: declare the directory as the source and skip the
      // download entirely. Emitted relative to the project so the generated
      // file stays portable across machines that share the layout.
      if (!string.IsNullOrWhiteSpace(details.Path))
      {
        var local = details.Path.Trim();
        var absolute = System.IO.Path.IsPathRooted(local)
          ? local
          : System.IO.Path.GetFullPath(local, Directory.GetCurrentDirectory());

        if (!Directory.Exists(absolute))
        {
          AnsiConsole.MarkupLine(
            $"[bold yellow]Warning:[/] Dependency '{name}' points at '{local}', which does not exist (expected '{absolute}').");
        }
        else if (!File.Exists(System.IO.Path.Combine(absolute, "CMakeLists.txt")) &&
                 !File.Exists(System.IO.Path.Combine(absolute, ".config", "cmake", "CMakeLists.txt")))
        {
          // Forge writes a project's CMakeLists when that project is built, so
          // an unbuilt path dependency is silently skipped by CMake: the
          // consumer then fails with a missing header or an unknown target.
          AnsiConsole.MarkupLine(
            $"[bold yellow]Warning:[/] Dependency '{name}' at '{local}' has no CMakeLists.txt yet — " +
            "run `forge build` in it first (or `forge workspace build`).");
        }

        var emitted = System.IO.Path.IsPathRooted(local)
          ? local.Replace('\\', '/')
          : "${CMAKE_CURRENT_SOURCE_DIR}/" + local.Replace('\\', '/');

        sb.AppendLine($"FetchContent_Declare({name} SOURCE_DIR \"{emitted}\")");
        sb.AppendLine($"FetchContent_MakeAvailable({name})");
        continue;
      }

      if (string.IsNullOrEmpty(details.Git) || string.IsNullOrEmpty(details.Tag))
      {
        AnsiConsole.Markup($"[yellow]Warning[/]: Skipping invalid dependency '{name}'");
        continue;
      }

      // Prefer the commit recorded in forge.lock: tags and branches move, and
      // a reproducible build should not.
      var lockedCommit = LockfileManager.LockedCommitFor(name, details);
      var gitTag = lockedCommit ?? details.Tag;
      sb.AppendLine($"FetchContent_Declare({name} GIT_REPOSITORY \"{details.Git}\" GIT_TAG \"{gitTag}\")");
      sb.AppendLine($"FetchContent_MakeAvailable({name})");
    }

    return sb.ToString();
  }
}
