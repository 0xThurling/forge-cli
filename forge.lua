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
      }
    },
    conan = {
      sol2 = "3.5.0"
    },
  },
  resources = {
    files = {},
  },
  scripts = {},
  features = {
  },
  custom = {
    testing = "true",
  },
}
