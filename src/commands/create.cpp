#include "create.hpp"
#include <algorithm>
#include <cctype>
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

void create_project(const std::string &name, const std::string &type) {
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
  std::filesystem::create_directories(path, ec);
  if (!ec) std::filesystem::create_directories(path / "src", ec);
  if (!ec) std::filesystem::create_directories(path / "external", ec);
  if (!ec) std::filesystem::create_directories(path / "assets", ec);
  if (!ec) std::filesystem::create_directories(path / ".config", ec);

  // Create Lua directories
  if (!ec) std::filesystem::create_directories(path / ".config" / "forge", ec);
  if (!ec) std::filesystem::create_directories(path / ".config" / "forge" / "commands", ec);
  if (!ec) std::filesystem::create_directories(path / ".config" / "forge" / "build", ec);
  if (!ec) std::filesystem::create_directories(path / ".config" / "forge" / "templates", ec);
  if (!ec) std::filesystem::create_directories(path / ".config" / "forge" / "definitions", ec);

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

  // Make sure we handle case insensitivity
  std::string type_lower = type;
  std::transform(type_lower.begin(), type_lower.end(), type_lower.begin(), [](unsigned char c){
    return std::tolower(c);
  });
  std::string project_type = type_lower == "library" ? "library" : "executable"; 

  forge_config_lua_file << 
    "return {\n"
    " project = {\n"
    "   name = \"" << name << "\",\n"
    "   type = \"" << project_type << "\",\n"
    "   standard = \"20\"" << (type_lower == "library" ? ",\n   install_headers = true" : "") << "\n"
    " },\n"
    " testing = false,\n"
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

  std::cout << "Successfully created " << name << '\n';
}

void create_command(CLI::App &app, CreateCommandArgs& args) {
  auto *create = app.add_subcommand("create", "Creates a new forge project");

  create->add_option("name", args.name, "Name of the project")->required();

  create->add_option("-t,--type", args.type, "Type of project (Executable or Library)");

  create->callback([&](){ create_project(args.name, args.type); });
}
