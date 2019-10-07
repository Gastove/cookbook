namespace Cookbook

module Logging =

    open Serilog

    let ConfigureLogging() =
        LoggerConfiguration().MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", Events.LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger()
