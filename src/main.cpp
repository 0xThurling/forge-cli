#include "CLI/CLI.hpp"
#include "commands/create.hpp"
#define SOL_ALL_SAFETIES_ON 1
#include <sol/sol.hpp>

int main(int argc, char** argv) {
  CLI::App app {"Forge CLI"};

  // Initialise my arguments  
  CreateCommandArgs command_args {};

  // Add commands
  create_command(app, command_args);

  // Require at least one command
  app.require_subcommand(1);

  CLI11_PARSE(app, argc, argv);

  return 0;
}
