#include "project.hpp"
#include "dependency.hpp"
#include "resources.hpp"

#include <string>
#include <unordered_map>

struct Feature {
  bool enabled;
  std::unordered_map<std::string, std::string> options;
};

struct Config {
  Project project;

  bool testing;

  std::unordered_map<std::string, Dependency> dependencies;
  std::unordered_map<std::string, std::string> conan_dependencies;

  Resources resources;

  std::unordered_map<std::string, std::string> scripts;
  std::unordered_map<std::string, Feature> Features;
  std::unordered_map<std::string, std::string> custom;
};
