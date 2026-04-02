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
    return @"
# --- Testing ---
enable_testing()
file(GLOB_RECURSE TEST_SOURCES ""${PROJECT_SOURCE_DIR}/test/*.cpp"")
set(SOURCES_FOR_TESTS ${SOURCES})
if(EXISTS ""${PROJECT_SOURCE_DIR}/src/main.cpp"")
list(REMOVE_ITEM SOURCES_FOR_TESTS ""${PROJECT_SOURCE_DIR}/src/main.cpp"")
endif()
add_executable(run_tests ${TEST_SOURCES} ${SOURCES_FOR_TESTS})
target_include_directories(run_tests PRIVATE 
    ${CMAKE_CURRENT_SOURCE_DIR}/src
    ${CMAKE_CURRENT_BINARY_DIR}/googletest-src/googletest/include
    ${CMAKE_CURRENT_BINARY_DIR}/googletest-src/googletest
)
target_link_libraries(run_tests PUBLIC GTest::gtest_main)
include(GoogleTest)
gtest_discover_tests(run_tests)
      ";
  }
}
