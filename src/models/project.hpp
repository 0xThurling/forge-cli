#include <string>

struct Project {
  std::string name;
  std::string type    {"executable"};
  std::string linkage {"static"};
  std::string standard;
  std::string cmake_policy_version;
  bool install_headers;
};
