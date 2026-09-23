using System.Text;
using forge.Models;

namespace forge.CMakeGeneration.Sections;

/// <summary>
/// Emits the test target and, when enabled, the benchmark target.
/// </summary>
/// <remarks>
/// Runs at priority 50 — after the project target and the link line. The test
/// framework is selected by <c>testing.framework</c> (<c>gtest</c> by default,
/// <c>catch2</c> or <c>doctest</c>), and <c>testing.benchmark</c> adds a Google
/// Benchmark target built from <c>bench/</c>.
/// </remarks>
public class TestingSection : CMakeSectionBase
{
  public override string Name => "testing";

  public override int Priority => 50;

  // The framework's dependency comes from forge.lua for gtest and from
  // find_package/FetchContent for catch2/doctest, so Testing alone decides.
  public override bool IsEnabled(BuildContext context) =>
    context.Config.Testing || context.Config.Benchmark;

  public override string Generate(BuildContext context)
  {
    var config = context.Config;
    var block = new StringBuilder();

    if (config.Testing)
      AppendTestTarget(block, context);

    if (config.Benchmark)
      AppendBenchmarkTarget(block, config);

    if (block.Length == 0)
      return string.Empty;

    // Tests and benchmarks belong to the project itself: a consumer that adds
    // this project as a dependency should not build them (nor pay for the test
    // framework). `CMAKE_SOURCE_DIR` differs from `CMAKE_CURRENT_SOURCE_DIR`
    // exactly when this project was added as a subdirectory.
    var sb = new StringBuilder();
    sb.AppendLine("# --- Testing and benchmarks ---");
    sb.AppendLine("# Only the top-level project builds these; a consumer of this project");
    sb.AppendLine("# does not build (or fetch a framework for) another project's tests.");
    sb.AppendLine("if(CMAKE_SOURCE_DIR STREQUAL CMAKE_CURRENT_SOURCE_DIR OR FORGE_BUILD_DEPENDENCY_TESTS)");
    foreach (var line in block.ToString().Split('\n'))
      sb.AppendLine(line.Length == 0 ? string.Empty : "  " + line);
    sb.AppendLine("endif()");
    return sb.ToString();
  }

  /// <summary>
  /// How the generated file obtains GoogleTest: from the dependency the
  /// project declares (a local checkout or a git repository), or with
  /// <c>find_package</c> when it comes from Conan, vcpkg or the system.
  /// </summary>
  private static string GoogleTestWiring(ProjectConfig config)
  {
    if (config.Dependencies.TryGetValue("googletest", out var googleTest))
    {
      if (!string.IsNullOrWhiteSpace(googleTest.Path))
      {
        var local = googleTest.Path.Trim().Replace('\\', '/');
        var emitted = System.IO.Path.IsPathRooted(local)
          ? local
          : "${CMAKE_CURRENT_SOURCE_DIR}/" + local;
        return $"include(FetchContent)\n" +
               $"FetchContent_Declare(googletest SOURCE_DIR \"{emitted}\")\n" +
               "FetchContent_MakeAvailable(googletest)\ninclude(GoogleTest)";
      }

      if (!string.IsNullOrWhiteSpace(googleTest.Git) && !string.IsNullOrWhiteSpace(googleTest.Tag))
      {
        var locked = LockfileManager.LockedCommitFor("googletest", googleTest);

        // The framework is a fetched dependency like any other, so it uses the
        // shared cache too: without it, every project re-clones GoogleTest.
        var cached = DependencyCache.Enabled(config.Build.Cache)
          ? DependencyCache.CMakeVariableFor("googletest", googleTest.Git, googleTest.Tag, locked)
          : string.Empty;

        return "include(FetchContent)\n" +
               (cached.Length > 0 ? cached + "\n" : string.Empty) +
               $"FetchContent_Declare(googletest GIT_REPOSITORY \"{googleTest.Git}\" GIT_TAG \"{locked ?? googleTest.Tag}\")\n" +
               "FetchContent_MakeAvailable(googletest)\ninclude(GoogleTest)";
      }
    }

    // Conan/vcpkg/the system provide the imported target.
    return "find_package(GTest REQUIRED)\ninclude(GoogleTest)";
  }

  private static void AppendTestTarget(StringBuilder sb, BuildContext context)
  {
    var config = context.Config;
    var framework = config.TestFramework.Length > 0 ? config.TestFramework : "gtest";

    // The framework's own target, how it is obtained, and how its tests are
    // registered with CTest.
    var (frameworkTarget, wiring, discovery) = framework switch
    {
      "catch2" => ("Catch2::Catch2WithMain", "find_package(Catch2 3 REQUIRED)\ninclude(Catch)",
                   "catch_discover_tests(${PROJECT_NAME}_tests)"),
      "doctest" => ("doctest_with_main",
                    "FetchContent_Declare(doctest GIT_REPOSITORY \"https://github.com/doctest/doctest.git\" GIT_TAG \"v2.4.11\")\nFetchContent_MakeAvailable(doctest)",
                    "add_test(NAME ${PROJECT_NAME}_tests COMMAND ${PROJECT_NAME}_tests)"),
      _ => ("GTest::gtest_main", GoogleTestWiring(config), "gtest_discover_tests(${PROJECT_NAME}_tests)"),
    };

    // Project dependencies the tests also link (excluding the test framework).
    var testDeps = new List<string> { frameworkTarget };
    foreach (var dep in config.Dependencies)
    {
      if (dep.Key != "googletest")
        testDeps.Add(string.IsNullOrEmpty(dep.Value.Target) ? dep.Key : dep.Value.Target);
    }
    testDeps.AddRange(context.LinkDependencies);

    // Extra libraries of the project are part of it, so the tests link them too
    // (extra executables are programs, not libraries).
    foreach (var target in config.Targets.Where(target => target.Type == "library"))
      testDeps.Add(target.Name);

    sb.AppendLine("enable_testing()");
    sb.AppendLine(wiring);
    sb.AppendLine("file(GLOB_RECURSE TEST_SOURCES \"${PROJECT_SOURCE_DIR}/test/*.cpp\")");
    sb.AppendLine("list(FILTER TEST_SOURCES EXCLUDE REGEX \"/(build|build-[^/]*|CMakeFiles)/\")");
    sb.AppendLine();
    sb.AppendLine("# Build list of sources WITHOUT src/main.cpp (to avoid multiple main)");
    sb.AppendLine("file(GLOB_RECURSE APP_SOURCES \"${PROJECT_SOURCE_DIR}/src/*.cpp\")");
    sb.AppendLine("list(FILTER APP_SOURCES EXCLUDE REGEX \"/(build|build-[^/]*|CMakeFiles)/\")");
    sb.AppendLine("list(FILTER APP_SOURCES EXCLUDE REGEX \".*main\\\\.cpp$\")");
    sb.AppendLine();
    sb.AppendLine("add_executable(${PROJECT_NAME}_tests ${TEST_SOURCES} ${APP_SOURCES})");
    ProjectTargetSection.AppendModuleFileSet(sb, config, "${PROJECT_NAME}_tests", ["src", "test"]);
    sb.AppendLine("target_include_directories(${PROJECT_NAME}_tests PRIVATE");
    sb.AppendLine("  ${CMAKE_CURRENT_SOURCE_DIR}/src");
    sb.AppendLine("  ${CMAKE_CURRENT_SOURCE_DIR}/include");
    sb.AppendLine(")");
    sb.AppendLine($"target_link_libraries(${{PROJECT_NAME}}_tests PUBLIC {string.Join(" ", testDeps)})");
    sb.AppendLine(discovery);
  }

  private static void AppendBenchmarkTarget(StringBuilder sb, ProjectConfig config)
  {
    sb.AppendLine();
    sb.AppendLine("# --- Benchmarks ---");
    sb.AppendLine("FetchContent_Declare(googlebenchmark");
    sb.AppendLine("  GIT_REPOSITORY \"https://github.com/google/benchmark.git\"");
    sb.AppendLine("  GIT_TAG \"v1.8.3\")");
    sb.AppendLine("set(BENCHMARK_ENABLE_TESTING OFF CACHE BOOL \"\" FORCE)");
    sb.AppendLine("FetchContent_MakeAvailable(googlebenchmark)");
    sb.AppendLine();
    sb.AppendLine("file(GLOB_RECURSE BENCH_SOURCES \"${PROJECT_SOURCE_DIR}/bench/*.cpp\")");
    sb.AppendLine("if(BENCH_SOURCES)");
    sb.AppendLine("  add_executable(${PROJECT_NAME}_bench ${BENCH_SOURCES})");
    ProjectTargetSection.AppendModuleFileSet(sb, config, "${PROJECT_NAME}_bench", ["src", "bench"]);
    sb.AppendLine("  target_link_libraries(${PROJECT_NAME}_bench PRIVATE benchmark::benchmark)");
    sb.AppendLine("  target_include_directories(${PROJECT_NAME}_bench PRIVATE");
    sb.AppendLine("    ${PROJECT_SOURCE_DIR}/src");
    sb.AppendLine("    ${PROJECT_SOURCE_DIR}/include)");
    sb.AppendLine("endif()");
  }
}
