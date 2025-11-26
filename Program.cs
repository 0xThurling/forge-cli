using cpm.Commands;
using DotMake.CommandLine;
using forge.Commands.Lua;

// Start the Lua Sandbox Engine
LuaEngine.InitialiseLuaEngine();

// Cli.Run<RootCommand>(args);

await LuaBuilder.RunBuilderScripts();
