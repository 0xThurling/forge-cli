using System.Diagnostics;
using Spectre.Console;

namespace forge.ForgeEngine.CoreUtils;

public static class CoreUtils
{
  /// <summary>
  /// Detects the current Linux distribution from /etc/os-release.
  /// </summary>
  /// <returns>
  /// The canonical distribution name (e.g., "ubuntu", "arch", "fedora").
  /// </returns>
  public static string GetLinuxDistro()
  {
    var dict = new Dictionary<string, string>();
    try
    {
      var lines = File.ReadAllLines("/etc/os-release");

      foreach (var line in lines)
      {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
          continue;

        var parts = line.Split('=', 2);
        if (parts.Length == 2)
        {
          var key = parts[0].Trim();
          var value = parts[1].Trim().Trim('"'); // remove quotes
          dict[key] = value;
        }
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine("Error reading distro info: " + ex.Message);
      return "unknown";
    }

    return dict.TryGetValue("NAME", out var name) ? DistroName(name) : "unknown";
  }

  /// <summary>
  /// Detects the current operating system.
  /// </summary>
  /// <returns>"linux", "macos", or "windows".</returns>
  public static string GetOperatingSystem()
  {
    if (OperatingSystem.IsLinux()) return "linux";
    if (OperatingSystem.IsMacOS()) return "macos";
    if (OperatingSystem.IsWindows()) return "windows";
    return string.Empty;
  }

  private static string DistroName(string name) => name switch
  {
    var n when n.Contains("arch", StringComparison.OrdinalIgnoreCase) => "arch",
    var n when n.Contains("debian", StringComparison.OrdinalIgnoreCase) => "debian",
    var n when n.Contains("ubuntu", StringComparison.OrdinalIgnoreCase) => "ubuntu",
    var n when n.Contains("mint", StringComparison.OrdinalIgnoreCase) => "mint",
    var n when n.Contains("kali", StringComparison.OrdinalIgnoreCase) => "kali",
    var n when n.Contains("red hat", StringComparison.OrdinalIgnoreCase) => "redhat",
    var n when n.Contains("fedora", StringComparison.OrdinalIgnoreCase) => "fedora",
    var n when n.Contains("centos", StringComparison.OrdinalIgnoreCase) => "centos",
    var n when n.Contains("rocky", StringComparison.OrdinalIgnoreCase) => "rocky",
    var n when n.Contains("manjaro", StringComparison.OrdinalIgnoreCase) => "manjaro",
    var n when n.Contains("garuda", StringComparison.OrdinalIgnoreCase) => "garuda",
    var n when n.Contains("alpine", StringComparison.OrdinalIgnoreCase) => "alpine",
    var n when n.Contains("amazon", StringComparison.OrdinalIgnoreCase) => "amazon",
    var n when n.Contains("nixos", StringComparison.OrdinalIgnoreCase) => "nixos",
    _ => "unknown"
  };

  /// <summary>
  /// Installs system packages using the specified package manager.
  /// </summary>
  /// <param name="hasPass">Whether sudo password is required.</param>
  /// <param name="packageManager">The package manager to use.</param>
  /// <param name="packages">List of package names to install.</param>
  /// <param name="pass">Optional sudo password.</param>
  public static void InstallPackages(bool hasPass, string packageManager, List<string> packages, string? pass = null)
  {
    string command =
      packageManager switch
      {
        "brew" or "winget" or "apt-get" or "choco" => " install ",
        "pacman" => " -S ",
        _ => " "
      } +
      $"{string.Join(" ", packages)}" +
      packageManager switch
      {
        "pacman" => " --noconfirm ",
        _ => string.Empty
      };


    var installationProcess = new ProcessStartInfo();
    if (hasPass && packageManager switch { "brew" or "apt-get" or "pacman" => true, _ => false })
    {
      installationProcess = new ProcessStartInfo("sudo", $"-S {packageManager} {command}")
      {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
      };
    }
    else
    {
      installationProcess = new ProcessStartInfo($"{packageManager}", $"{command}")
      {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
      };
    }

    using var process = Process.Start(installationProcess) ??
      throw new Exception("Failed to start CMake process.");

    AnsiConsole.WriteLine("Installing packages...");

    process.WaitForExit();
  }
}
