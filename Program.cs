using cpm.Commands;
using DotMake.CommandLine;
using cpm.Commands.Lua;

// Cli.Run<RootCommand>(args);
var luaBuilder = new LuaBuilder();

await luaBuilder.RunBuilderScripts();
