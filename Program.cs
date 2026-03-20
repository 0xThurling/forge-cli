/// <summary>
/// Forge - A C++ Project Manager CLI Tool
/// 
/// This is the entry point for the Forge application. Forge simplifies C++ project
/// management by providing project scaffolding, CMake integration, dependency management,
/// resource embedding, and scriptable build automation through Lua.
/// </summary>
/// <remarks>
/// The application initializes the Lua sandbox engine and then delegates to LuaBuilder
/// to execute any custom build scripts defined in the project configuration.
/// </remarks>
using DotMake.CommandLine;
using forge.Commands;
using forge.Commands.Lua;

// Start the Lua Sandbox Engine
LuaEngine.InitialiseLuaEngine();

// Cli.Run<RootCommand>(args);

await LuaBuilder.RunBuilderScripts();
