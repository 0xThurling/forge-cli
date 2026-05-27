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
  ftxui::Element document = ftxui::text("Creating project: " + name + "\n") | ftxui::color(ftxui::Color::BlueLight);

  // Set the screen buffer so that we can send it to stdout
  auto screen = ftxui::Screen::Create(
    ftxui::Dimension::Fit(document),
    ftxui::Dimension::Fit(document)
  );

  ftxui::Render(screen, document);

  screen.Print();

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

  std::ofstream file(path / "src" / "main.cpp", std::ios::out | std::ios::trunc);

  if (!file) {
    std::cerr << "Failed to open file: " << path << '\n';
    return;
  }


}

void create_command(CLI::App &app, CreateCommandArgs& args) {
  auto *create = app.add_subcommand("create", "Creates a new forge project");

  create->add_option("name", args.name, "Name of the project")->required();

  create->add_option("-t,--type", args.type, "Type of project (Executable or Library)");

  create->callback([&](){ create_project(args.name, args.type); });
}
