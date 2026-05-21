return {
  project = {
    name = "forge",
    type = "executable",
    standard = "20",
  },
  testing = true,
  dependencies = {
    direct = {
      googletest = {
        git = "https://github.com/google/googletest.git",
        tag = "v1.14.0",
      },
      ftxui = {
        git = "https://github.com/ArthurSonzogni/FTXUI",
        tag = "v5.0.0",
        target = "ftxui::screen ftxui::dom ftxui::component"
      },
      cli11 = {
        git = "https://github.com/CLIUtils/CLI11",
        tag = "v2.4.2",
        target = "CLI11::CLI11"
      },
      forge_utils = {
        git = "https://github.com/0xThurling/forge-utils",
        tag = "main",
        target = "forge-utils"
      }
    },
    conan = {
      -- This is added since it handles the Lua integration as well
      sol2 = "3.5.0"
    },
  },
  resources = {
    files = {},
  },
  scripts = {
    ["check-proj"] = "clang-tidy -p build src/*.cpp --checks='bugprone-*,modernize-*,readability-*,performance-*'",
    ["check-mem"] = "valgrind --leak-check=full --show-leak-kinds=all --error-exitcode=1 ./build/forge"
  },
  features = {
  },
  custom = {
    testing = "true",
  },
}
