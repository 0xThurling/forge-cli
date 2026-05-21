#include "create.hpp"
#include <forge-utils/defer.hpp>
#include <string>

void create_project(std::string &name, std::string &type) {
  Defer defer([&]{std::cout << "defered" << "\n";});

  std::cout << "Project name: " << name << "\n";

  if (!type.empty()) {
    std::cout << "Project type: " << type << "\n";
  }
}

void create_command(CLI::App &app, CreateCommandArgs& args) {
  auto *create = app.add_subcommand("create", "Creates a new forge project");

  create->add_option("name", args.name, "Name of the project")->required();

  create->add_option("-t,--type", args.type, "Type of project (Executable or Library)");

  create->callback([&](){ create_project(args.name, args.type); });
}
