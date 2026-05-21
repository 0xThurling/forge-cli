#pragma once

#include <CLI/CLI.hpp>

struct CreateCommandArgs {
  std::string name;
  std::string type;
};

void create_command(CLI::App& app, CreateCommandArgs& args);
