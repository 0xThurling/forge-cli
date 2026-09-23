using System.Diagnostics;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace forge;

/// <summary>A tool Forge uses, what it is for, and how to install it.</summary>
/// <param name="Name">The executable to look for on PATH.</param>
/// <param name="Purpose">Why Forge wants it (shown in the table).</param>
/// <param name="MinimumVersion">Minimum acceptable version, or empty.</param>
/// <param name="Packages">Package name per package manager.</param>
/// <param name="Required">True when a plain build needs it.</param>
public sealed record ToolRequirement(
  string Name,
  string Purpose,
  string MinimumVersion,
  Dictionary<string, string> Packages,
  bool Required)
{
  /// <summary>The version reported by the tool, or null when it is missing.</summary>
  public string? Detect()
  {
    try
    {
      var startInfo = new ProcessStartInfo(Name)
      {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
      };
      startInfo.ArgumentList.Add("--version");

      using var process = Process.Start(startInfo);
      if (process == null)
        return null;

      var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
      process.WaitForExit();

      // The version is the first x.y[.z] in the output ("cmake version 4.4.3",
      // "ccache version 4.10.2", "git version 2.51.0").
      var match = Regex.Match(output, @"(\d+)\.(\d+)(?:\.(\d+))?");
      return match.Success ? match.Value : (process.ExitCode == 0 ? "unknown" : null);
    }
    catch (Exception)
    {
      return null; // not installed
    }
  }

  /// <summary>Whether the detected version satisfies <see cref="MinimumVersion"/>.</summary>
  public bool IsVersionAcceptable(string? version)
  {
    if (string.IsNullOrEmpty(MinimumVersion) || version is null || version == "unknown")
      return version is not null;

    static int[] Parts(string value) =>
      value.Split('.').Select(part => int.TryParse(part, out var n) ? n : 0).ToArray();

    var found = Parts(version);
    var required = Parts(MinimumVersion);
    for (var i = 0; i < Math.Max(found.Length, required.Length); i++)
    {
      var left = i < found.Length ? found[i] : 0;
      var right = i < required.Length ? required[i] : 0;
      if (left != right)
        return left > right;
    }
    return true;
  }

  /// <summary>The package name for a manager, falling back to the tool name.</summary>
  public string PackageFor(string manager) =>
    Packages.TryGetValue(manager, out var package) ? package : Name;
}

/// <summary>
/// The tools Forge drives, and the package manager of the machine it runs on.
/// One table feeds <c>forge setup</c>, <c>forge doctor</c> and the install step
/// of a generated CI workflow, so they cannot disagree about what is needed.
/// </summary>
public static class ToolRequirements
{
  /// <summary>Everything Forge can use, in report order.</summary>
  public static readonly ToolRequirement[] All =
  [
    new("git", "fetch dependencies and read version information", "", new()
    {
      ["apt-get"] = "git", ["dnf"] = "git", ["zypper"] = "git", ["apk"] = "git",
      ["pacman"] = "git", ["brew"] = "git", ["winget"] = "Git.Git", ["choco"] = "git"
    }, Required: true),

    new("cmake", "configure and build the project", "3.23", new()
    {
      ["apt-get"] = "cmake", ["dnf"] = "cmake", ["zypper"] = "cmake", ["apk"] = "cmake",
      ["pacman"] = "cmake", ["brew"] = "cmake", ["winget"] = "Kitware.CMake", ["choco"] = "cmake"
    }, Required: true),

    new("c++", "compile the project (g++ or clang++)", "", new()
    {
      ["apt-get"] = "g++", ["dnf"] = "gcc-c++", ["zypper"] = "gcc-c++", ["apk"] = "g++",
      ["pacman"] = "gcc", ["brew"] = "llvm", ["winget"] = "LLVM.LLVM", ["choco"] = "llvm"
    }, Required: true),

    new("ninja", "build in parallel; required by `build.modules`", "", new()
    {
      ["apt-get"] = "ninja-build", ["dnf"] = "ninja-build", ["zypper"] = "ninja", ["apk"] = "ninja",
      ["pacman"] = "ninja", ["brew"] = "ninja", ["winget"] = "Ninja-build.Ninja", ["choco"] = "ninja"
    }, Required: false),

    new("make", "build with the default generator (Ninja is preferred)", "", new()
    {
      ["apt-get"] = "make", ["dnf"] = "make", ["zypper"] = "make", ["apk"] = "make",
      ["pacman"] = "make", ["brew"] = "make", ["winget"] = "GnuWin32.Make", ["choco"] = "make"
    }, Required: false),

    new("pkg-config", "resolve `dependencies.pkgconfig` modules", "", new()
    {
      // Arch and the RPM family ship pkgconf, which provides the binary.
      ["apt-get"] = "pkg-config", ["dnf"] = "pkgconf", ["zypper"] = "pkgconf",
      ["apk"] = "pkgconf", ["pacman"] = "pkgconf", ["brew"] = "pkg-config",
      ["winget"] = "pkgconf.pkgconf", ["choco"] = "pkgconfiglite"
    }, Required: false),

    new("tar", "`forge extract` and `forge fetch`", "", new()
    {
      ["apt-get"] = "tar", ["dnf"] = "tar", ["zypper"] = "tar", ["apk"] = "tar",
      ["pacman"] = "tar", ["brew"] = "gnu-tar", ["winget"] = "GnuWin32.Tar", ["choco"] = "tar"
    }, Required: false),

    new("unzip", "`forge extract` and `forge fetch` for zip archives", "", new()
    {
      ["apt-get"] = "unzip", ["dnf"] = "unzip", ["zypper"] = "unzip", ["apk"] = "unzip",
      ["pacman"] = "unzip", ["brew"] = "unzip", ["winget"] = "GnuWin32.UnZip", ["choco"] = "unzip"
    }, Required: false),

    new("ccache", "cache compilation between builds", "", new()
    {
      ["apt-get"] = "ccache", ["dnf"] = "ccache", ["zypper"] = "ccache", ["apk"] = "ccache",
      ["pacman"] = "ccache", ["brew"] = "ccache", ["winget"] = "ccache", ["choco"] = "ccache"
    }, Required: false),

    new("clang-format", "`forge format`", "", new()
    {
      ["apt-get"] = "clang-format", ["dnf"] = "clang-tools-extra", ["zypper"] = "clang-tools",
      ["apk"] = "clang-extra-tools", ["pacman"] = "clang", ["brew"] = "clang-format",
      ["winget"] = "LLVM.LLVM", ["choco"] = "llvm"
    }, Required: false),

    new("clang-tidy", "`forge lint`", "", new()
    {
      ["apt-get"] = "clang-tidy", ["dnf"] = "clang-tools-extra", ["zypper"] = "clang-tools",
      ["apk"] = "clang-extra-tools", ["pacman"] = "clang", ["brew"] = "llvm",
      ["winget"] = "LLVM.LLVM", ["choco"] = "llvm"
    }, Required: false),

    new("cpack", "`forge publish` (ships with CMake)", "", new()
    {
      ["apt-get"] = "cmake", ["dnf"] = "cmake", ["zypper"] = "cmake", ["apk"] = "cmake",
      ["pacman"] = "cmake", ["brew"] = "cmake", ["winget"] = "Kitware.CMake", ["choco"] = "cmake"
    }, Required: false),

    new("python3", "the test suite and download scenarios", "", new()
    {
      ["apt-get"] = "python3", ["dnf"] = "python3", ["zypper"] = "python3", ["apk"] = "python3",
      ["pacman"] = "python", ["brew"] = "python@3", ["winget"] = "Python.Python.3.12", ["choco"] = "python3"
    }, Required: false)
  ];

  /// <summary>Tools that need a step of their own (not a package).</summary>
  public static readonly (string Name, string Purpose, string Hint)[] ExtraSteps =
  [
    ("conan", "Conan packages (`dependencies.conan`)", "pipx install conan"),
    ("vcpkg", "vcpkg packages (`dependencies.vcpkg`)", "set VCPKG_ROOT, or clone vcpkg and bootstrap it")
  ];

  /// <summary>
  /// Where an ecosystem tool lives, when it is not on <c>PATH</c> — the same
  /// places the build looks: vcpkg in <c>$VCPKG_ROOT</c> or the project's
  /// <c>external/vcpkg</c>, Conan in pipx's bin directory.
  /// </summary>
  /// <returns>The path found, or null.</returns>
  public static string? ResolveExtra(string name)
  {
    if (ToolLocator.Find(name) is not null)
      return name; // on PATH

    if (name == "vcpkg")
    {
      var roots = new List<string>();
      var configured = Environment.GetEnvironmentVariable("VCPKG_ROOT");
      if (!string.IsNullOrWhiteSpace(configured))
        roots.Add(configured);
      roots.Add(Path.Combine("external", "vcpkg"));
      // Where `forge setup --install` puts it outside a project.
      roots.Add(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "forge", "vcpkg"));

      foreach (var root in roots)
      {
        var candidate = Path.Combine(root, OperatingSystem.IsWindows() ? "vcpkg.exe" : "vcpkg");
        if (File.Exists(candidate))
          return candidate;
      }
    }

    if (name == "conan")
    {
      var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
      var candidate = Path.Combine(home, ".local", "bin", OperatingSystem.IsWindows() ? "conan.exe" : "conan");
      if (IsUsableFile(candidate))
        return candidate;
    }

    return null;
  }

  /// <summary>
  /// The command that installs an extra tool on this machine. The table's
  /// fallback is used when the manager has no sensible package for it — Conan
  /// on Debian/Ubuntu, for instance, where the archive package is still 1.x.
  /// </summary>
  public static string HintFor(string name, string fallback, string? manager) => (name, manager) switch
  {
    ("conan", "pacman") => "pipx install conan  (sudo pacman -S python-pipx)",
    ("conan", "apk") => "sudo apk add conan",
    ("conan", "dnf") => "sudo dnf install conan",
    ("conan", "zypper") => "sudo zypper install conan",
    ("conan", "brew") => "brew install conan",
    ("conan", "winget") => "winget install Conan.Conan",
    ("conan", "choco") => "choco install conan",
    _ => fallback
  };

  /// <summary>
  /// The steps that install an extra tool on this machine, in order. Empty when
  /// there is no sensible package — Conan on Debian/Ubuntu, for instance, where
  /// the archive package is still 1.x, so it goes through pipx instead.
  /// </summary>
  public static List<(string FileName, List<string> Arguments, bool NeedsSudo)> InstallStepsFor(
    string name, string? manager)
  {
    if (name != "conan")
      return [];

    return manager switch
    {
      // Arch has no `conan` package: `python-pipx` + `pipx install conan` is
      // the route Conan's own documentation recommends.
      "pacman" =>
      [
        ("pacman", ["-S", "--noconfirm", "python-pipx"], true),
        ("pipx", ["install", "conan"], false)
      ],
      "apk" => [("apk", ["add", "--no-cache", "conan"], true)],
      "dnf" => [("dnf", ["install", "-y", "conan"], true)],
      "zypper" => [("zypper", ["install", "-y", "conan"], true)],
      "brew" => [("brew", ["install", "conan"], false)],
      "winget" => [("winget", ["install", "Conan.Conan"], false)],
      "choco" => [("choco", ["install", "-y", "conan"], false)],
      // Debian/Ubuntu: `conan` in the archive is 1.x, so install pipx and let it
      // provide the current release.
      "apt-get" =>
      [
        ("apt-get", ["install", "-y", "pipx"], true),
        ("pipx", ["install", "conan"], false)
      ],
      _ => []
    };
  }

  /// <summary>
  /// Package names for arbitrary tools on this machine, skipping the ones that
  /// are already present. Used for vcpkg's own prerequisites.
  /// </summary>
  public static List<string> PackageNamesFor(string owner, string? manager, IReadOnlyList<string> tools)
  {
    if (manager is null)
      return [];

    return tools
      .Where(tool => ToolLocator.Find(tool) is null)
      .Select(tool => Find(tool)?.PackageFor(manager) ?? tool)
      .ToList();
  }

  /// <summary>
  /// A tool that is installed but *not on PATH*: found where Forge looks for it,
  /// yet the command line could not run it. Returned with the location and the
  /// command that fixes the PATH.
  /// </summary>
  public static List<(string Name, string Location, string Fix)> PathProblems()
  {
    var problems = new List<(string, string, string)>();

    foreach (var (name, _, _) in ExtraSteps)
    {
      var location = ResolveExtra(name);
      if (location is null || location == name)
        continue; // missing, or already on PATH

      var fix = name switch
      {
        "conan" => "pipx ensurepath",
        // A vcpkg in the project needs nothing; one in the user's data
        // directory needs VCPKG_ROOT so Forge can find it.
        "vcpkg" when !IsInsideProject(location) =>
          $"export VCPKG_ROOT={Path.GetDirectoryName(Path.GetFullPath(location))}",
        "vcpkg" => string.Empty,
        _ => string.Empty
      };

      if (fix.Length > 0)
        problems.Add((name, location, fix));
    }

    return problems;
  }

  /// <summary>Whether a path is inside the current project's external directory.</summary>
  private static bool IsInsideProject(string location)
  {
    var project = Path.GetFullPath(Path.Combine("external", "vcpkg"));
    var candidate = Path.GetFullPath(Path.GetDirectoryName(location) ?? location);
    return candidate.StartsWith(project, StringComparison.Ordinal);
  }

  /// <summary>
  /// Whether a path is a usable file. A symlink whose target is missing does
  /// not count — and <c>File.Exists</c> alone cannot tell: on .NET 10 it is
  /// true for a dangling link, which is exactly the case that matters here.
  /// </summary>
  private static bool IsUsableFile(string path)
  {
    if (!File.Exists(path))
      return false;

    var info = new FileInfo(path);
    if (info.LinkTarget is null)
      return true;

    var target = Path.IsPathRooted(info.LinkTarget)
      ? info.LinkTarget
      : Path.Combine(info.DirectoryName!, info.LinkTarget);
    return File.Exists(target);
  }

  /// <summary>
  /// Tools that are installed but unusable from the command line: a dangling
  /// symlink (a <c>pipx install</c> that ran under sudo points into root's
  /// home), or a pipx venv whose command was never exposed. Both look installed
  /// and cannot run, so they are named for what they are.
  /// </summary>
  public static List<(string Name, string Reason, string Fix)> NeedsRepair()
  {
    var repaired = new List<(string, string, string)>();
    if (ToolLocator.Find("conan") is not null)
      return repaired; // on PATH: nothing to repair

    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    var link = Path.Combine(home, ".local", "bin", OperatingSystem.IsWindows() ? "conan.exe" : "conan");
    if (IsUsableFile(link))
      return repaired;

    var target = new FileInfo(link).LinkTarget;
    if (target is not null)
    {
      repaired.Add(("conan", $"points at {target}, which does not exist", "pipx install --force conan"));
      return repaired;
    }

    // pipx has a venv for it but never exposed the command: `pipx install conan`
    // would refuse ("already seems to be installed"), so --force is the fix.
    var venv = Path.Combine(home, ".local", "share", "pipx", "venvs", "conan");
    if (Directory.Exists(venv))
      repaired.Add(("conan", "is installed by pipx but not exposed as a command", "pipx install --force conan"));

    return repaired;
  }

  /// <summary>Finds a tool by name (case-insensitive), or null.</summary>
  public static ToolRequirement? Find(string name) =>
    All.FirstOrDefault(tool => string.Equals(tool.Name, name, StringComparison.OrdinalIgnoreCase));

  /// <summary>Detects the machine's package manager, or null when unknown.</summary>
  public static string? DetectPackageManager()
  {
    if (OperatingSystem.IsMacOS())
      return Has("brew") ? "brew" : null;

    var manager = distro switch
    {
      "debian" or "ubuntu" or "linuxmint" or "pop" or "raspbian" => "apt-get",
      "fedora" or "rhel" or "centos" or "rocky" or "almalinux" => "dnf",
      "opensuse" or "opensuse-leap" or "opensuse-tumbleweed" or "sles" => "zypper",
      "alpine" => "apk",
      "arch" or "manjaro" or "endeavouros" => "pacman",
      _ => null
    };

    if (manager is not null && Has(manager))
      return manager;

    // Unknown distro (or a container without the manager): try the usual ones.
    foreach (var candidate in new[] { "apt-get", "dnf", "pacman", "zypper", "apk", "brew" })
    {
      if (Has(candidate))
        return candidate;
    }

    return null;
  }

  /// <summary>The distro id from /etc/os-release, lower-cased.</summary>
  private static string distro
  {
    get
    {
      try
      {
        if (!File.Exists("/etc/os-release"))
          return string.Empty;

        foreach (var line in File.ReadAllLines("/etc/os-release"))
        {
          if (!line.StartsWith("ID=", StringComparison.Ordinal))
            continue;
          return line[3..].Trim().Trim('"').ToLowerInvariant();
        }
      }
      catch (IOException)
      {
        // Unreadable: fall through to detection by trying the managers.
      }
      return string.Empty;
    }
  }

  private static bool Has(string executable) => ToolLocator.Find(executable) is not null;

  /// <summary>The command line that installs packages with the given manager.</summary>
  public static (string FileName, List<string> Arguments, bool NeedsSudo) InstallCommand(
    string manager, IReadOnlyList<string> packages)
  {
    var list = string.Join(" ", packages);
    var (arguments, needsSudo) = manager switch
    {
      "apt-get" => (new List<string> { "install", "-y" }.Concat(packages), true),
      "dnf" => (new List<string> { "install", "-y" }.Concat(packages), true),
      "zypper" => (new List<string> { "install", "-y" }.Concat(packages), true),
      "apk" => (new List<string> { "add", "--no-cache" }.Concat(packages), true),
      "pacman" => (new List<string> { "-S", "--noconfirm" }.Concat(packages), true),
      "brew" => (new List<string> { "install" }.Concat(packages), false),
      "winget" => (new List<string> { "install", "--accept-source-agreements" }.Concat(packages), false),
      "choco" => (new List<string> { "install", "-y" }.Concat(packages), false),
      _ => (new List<string> { "install" }.Concat(packages), false)
    };

    return (manager, arguments.ToList(), needsSudo);
  }
}
