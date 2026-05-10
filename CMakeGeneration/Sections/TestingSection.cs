using forge.Models;

namespace forge.CMakeGeneration.Sections;

public class TestingSection : CMakeSectionBase
{
  public override string Name => "testing";

  public override int Priority => 50;

  public override bool IsEnabled(ProjectConfig config)
      => config.Testing && config.Dependencies.ContainsKey("googletest");

  public override string Generate(ProjectConfig config)
  {
    // Collect all dependencies for linking
    var testDeps = new List<string> { "GTest::gtest_main" };

    foreach (var dep in config.Dependencies)
    {
      if (dep.Key != "googletest")
      {
        var target = string.IsNullOrEmpty(dep.Value.Target) ? dep.Key : dep.Value.Target;
        testDeps.Add(target);
      }
    }

    return $@"
# --- Testing ---
enable_testing()
file(GLOB_RECURSE TEST_SOURCES ""${{PROJECT_SOURCE_DIR}}/test/*.cpp"")

# Build list of sources WITHOUT src/main.cpp (to avoid multiple main)
file(GLOB_RECURSE APP_SOURCES ""${{PROJECT_SOURCE_DIR}}/src/*.cpp"")
list(FILTER APP_SOURCES EXCLUDE REGEX "".*main\\.cpp$"")

add_executable(run_tests ${{TEST_SOURCES}} ${{APP_SOURCES}})
target_include_directories(run_tests PRIVATE
  ${{CMAKE_CURRENT_SOURCE_DIR}}/src
  ${{CMAKE_CURRENT_SOURCE_DIR}}/include
  ${{CMAKE_CURRENT_SOURCE_DIR}}/build/googletest-src/googletest/include
  ${{CMAKE_CURRENT_SOURCE_DIR}}/build/googletest-src/googletest
)
target_link_libraries(run_tests PUBLIC {string.Join(" ", testDeps)})
include(GoogleTest)
gtest_discover_tests(run_tests)
       ";
  }
}
