#include "create.hpp"
#include <filesystem>
#include <forge-utils/defer.hpp>
#include <fstream>
#include <ftxui/dom/elements.hpp>
#include <ftxui/dom/node.hpp>
#include <ftxui/screen/color.hpp>
#include <ftxui/screen/screen.hpp>
#include <ios>
#include <iostream>
#include <string>

void create_project(std::string &name, std::string &type) {
  // Initial
  ftxui::Element document =
    ftxui::text("Creating project: " + name) |
    ftxui::color(ftxui::Color::BlueLight);

  // Set the screen buffer so that we can send it to stdout
  auto screen = ftxui::Screen::Create(
    ftxui::Dimension::Fit(document),
    ftxui::Dimension::Fit(document)
  );

  ftxui::Render(screen, document);

  screen.Print();
  std::cout << "\n";

  auto path = std::filesystem::path(name);

  std::error_code ec;

  // Create project stuctured documents
  std::filesystem::create_directory(path);
  std::filesystem::create_directories(path / "src", ec);
  std::filesystem::create_directories(path / "external", ec);
  std::filesystem::create_directories(path / "assets", ec);
  std::filesystem::create_directories(path / ".config", ec);

  // Create Lua directories
  std::filesystem::create_directories(path / ".config" / "forge", ec);
  std::filesystem::create_directories(path / ".config" / "forge" / "commands", ec);
  std::filesystem::create_directories(path / ".config" / "forge" / "build", ec);
  std::filesystem::create_directories(path / ".config" / "forge" / "templates", ec);
  std::filesystem::create_directories(path / ".config" / "forge" / "definitions", ec);

  // Check if all directories for the project has been created
  if (ec) {
    std::cerr << "Failed to create directories: " << ec << "\n";
    return;
  }

  std::ofstream main_cpp_file(path / "src" / "main.cpp", std::ios::out | std::ios::trunc);

  if (!main_cpp_file) {
    std::cerr << "Failed to open file: " << path << '\n';
    return;
  }

  main_cpp_file << "#include <iostream>\n"
          "\n"
          "int main(int argc, char** argv) {\n"
          " std::cout << \"Hello, C++ World!\" << \"\\n\";\n"
          " return 0;\n"
          "}\n";

  std::ofstream forge_config_lua_file(path / "forge.lua", std::ios::out | std::ios::trunc);

  if (!forge_config_lua_file) {
    std::cerr << "Failed to open file: " << path << '\n';
    return;
  }

  std::string install_headers = type == "library" ? "install_headers = true" : ""; 
  std::string project_type = type == "library" ? "library" : "executable"; 

  forge_config_lua_file << 
    "return {\n"
    " project = {\n"
    "   name = \"" << name << "\",\n"
    "   type = \"" << project_type << "\",\n"
    "   standard = \"20\",\n"
    << install_headers <<
    " },\n"
    " testing = false,"
    " dependencies = {\n"
    "   direct = {},\n"
    "   conan = {},\n"
    " },\n"
    " scripts = {}\n"
    "}\n";

  std::ofstream git_ignore_file(path / ".gitignore", std::ios::out | std::ios::trunc);

  if (!git_ignore_file) {
    std::cerr << "Failed to open file: " << path << '\n';
    return;
  }

  git_ignore_file <<
    "build/\n"
    "lib/\n"
    "compile_commands.json\n"
    "conanfile.txt\n"
    "external/";
}

void create_command(CLI::App &app, CreateCommandArgs& args) {
  auto *create = app.add_subcommand("create", "Creates a new forge project");

  create->add_option("name", args.name, "Name of the project")->required();

  create->add_option("-t,--type", args.type, "Type of project (Executable or Library)");

  create->callback([&](){ create_project(args.name, args.type); });
}
