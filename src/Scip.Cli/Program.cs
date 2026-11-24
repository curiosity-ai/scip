using System.CommandLine;
using Scip.Cli;

return await CliApp.Build().Parse(args).InvokeAsync();
