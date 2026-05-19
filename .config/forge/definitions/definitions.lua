---@meta

---
--- Forge Lua API Type Definitions
--- Auto-generated for Lua Language Server (lua_ls)
---

--- Global forge API object
---@class forge
forge = {}


--- Forge logging utility
---@class forge.log
forge.log = {}


--- Access Forge's config
---@class forge.config
forge.config = {}


--- Adds custom CMake commands to the generated CMakeLists.txt
---@param snippet string The custom CMake command
function forge.add_cmake(snippet) end


--- Logs an information message to the console
---@param message string The message to log
function forge.log.info(message) end


--- Logs a warning message to the console
---@param message string The message to log
function forge.log.warn(message) end


--- Logs a error message to the console
---@param message string The message to log
function forge.log.error(message) end


--- Clones a Git repository to external/
---@param repo_url string The URL for the GitHub repository
---@param tag string The URL for the GitHub repository
---@return string The path location where it's saved'
function forge.pull_repo(repo_url, tag) end


--- Installs packages using the Specified package manager
---@param password string Password or 'nopass'
---@param package_manager string The package manager
---@param packages string[] List of packages
---@return number 0 on success
function forge.get_packages(password, package_manager, packages) end


--- Gets a config value by key
---@param key string Dot-notation key (e.g., 'project.name')
---@return string The config value
function forge.config.get(key) end


--- Checks if a feature is enabled
---@param feature string Feature name
---@return boolean True if enabled
function forge.config.has_feature(feature) end


--- Gets a feature option value
---@param feature string Feature name
---@param option string Option name
---@param default string Default value if not set
---@return string The option value
function forge.config.get_feature_option(feature, option, default) end


--- Operating System information
---@class forge.os
forge.os = {}


--- The current operating system
---@type string
forge.os.current = ""


--- The Windows operating system
---@type string
forge.os.windows = ""


--- The MacOS operating system
---@type string
forge.os.macos = ""


--- The Linux operating system
---@type string
forge.os.linux = ""


--- Linux distribution information
---@class forge.distro
forge.distro = {}


--- Current Linux distrobution
---@type string
forge.distro.my_distro = ""


--- The Arch Linux distrobution... btw
---@type string
forge.distro.arch = ""


--- The NixOs Linux distrobution
---@type string
forge.distro.nixos = ""


--- The Debian Linux distrobution
---@type string
forge.distro.debian = ""


--- The Ubuntu Linux Distrobution
---@type string
forge.distro.ubuntu = ""


--- The Manjaro Linux distrobution
---@type string
forge.distro.manjaro = ""


--- The Fedora Linux distrobution
---@type string
forge.distro.fedora = ""


--- Unknown Linux distrobution
---@type string
forge.distro.unknown = ""


--- Package manager constants
---@class forge.package_manager
forge.package_manager = {}


--- Use no password for package manager
---@type string
forge.package_manager.no_pass = ""


--- The WinGet package manager for Windows
---@type string
forge.package_manager.winget = ""


--- The Chocolatey package manager for Windows
---@type string
forge.package_manager.chocolatey = ""


--- The Homebrew package manager for MacOs
---@type string
forge.package_manager.brew = ""


--- The Pacman package manager for Arch
---@type string
forge.package_manager.pacman = ""


--- The APT package manager for Debian/Ubuntu
---@type string
forge.package_manager.aptget = ""


--- Downloads a file from a URL
---@param url string The URL to download
---@param output string The output file path
function forge.download(url, output) end


--- Extracts an archive file
---@param archive string Path to archive file
---@param output string Output directory
---@param string_components number The number of path components to strip (default: 1)
function forge.extract(archive, output, string_components) end


--- Downloads and extracts an archive in one step
---@param url string The URL to fetch
---@return string The path to the newly fetched directory
function forge.fetch(url) end


--- The current working directory
---@type string
forge.current_working_dir = ""


