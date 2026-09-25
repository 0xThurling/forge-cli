using forge.Models;
using Spectre.Console;

namespace forge.CMakeGeneration.Sections;

public class LinkingSection : CMakeSectionBase
{
  public override string Name => "linking";

  public override int Priority => 40;

  public override string Generate(BuildContext context)
  {
    var config = context.Config;
    var linkTargets = LinkTargets.For(config, context);

    // The project's own extra libraries are part of it, so its targets link
    // them: an application plus the library it is built from.
    foreach (var target in config.Targets.Where(target => target.Type == "library"))
      linkTargets.Add(target.Name);

    if (linkTargets.Count == 0) return string.Empty;

    // A library project installs an export set. Dependencies that declare
    // export metadata travel with it — the build-tree target here, their
    // imported target in the installed interface — and the rest cannot be
    // referenced from the export, so they stay in the build tree only.
    var exported = config.Project.Type == "library"
      ? LinkTargets.Exported(config)
      : new Dictionary<string, string>(StringComparer.Ordinal);

    var buildOnly = new HashSet<string>(StringComparer.Ordinal);
    if (config.Project.Type == "library")
    {
      foreach (var target in LinkTargets.Fetched(config))
        if (!exported.ContainsKey(target))
          buildOnly.Add(target);
      foreach (var target in config.Targets.Where(target => target.Type == "library"))
        buildOnly.Add(target.Name);
    }

    if (buildOnly.Count > 0)
    {
      var names = string.Join(", ", buildOnly.OrderBy(name => name, StringComparer.Ordinal));
      var plural = buildOnly.Count > 1;
      AnsiConsole.MarkupLine(
        $"[bold yellow]Warning:[/] {names} {(plural ? "are" : "is")} not exported by this project: " +
        $"the installed package will not propagate {(plural ? "them" : "it")}.");
    }

    string Link(string target)
    {
      if (buildOnly.Contains(target))
        return $"$<BUILD_INTERFACE:{target}>";
      if (exported.TryGetValue(target, out var installed))
        return $"$<BUILD_INTERFACE:{target}>$<INSTALL_INTERFACE:{installed}>";
      return target;
    }

    var links = string.Join(" ", linkTargets.Select(Link));

    // The main target only exists when src/ has sources, and a source-less
    // library is created as an INTERFACE target (see ProjectTargetSection), so
    // the keyword has to follow: PRIVATE on an INTERFACE target is a CMake
    // error, and a target that was never created cannot be linked at all.
    return $@"
# --- Linking ---
if(SOURCES)
  target_link_libraries({config.Project.Name} PRIVATE {links})
elseif(TARGET {config.Project.Name})
  target_link_libraries({config.Project.Name} INTERFACE {links})
endif()
      ";
  }
}
