namespace Cookbook

module Logging =

    open Serilog
    open Serilog.Sinks.GoogleCloudLogging

    let gclConfig = GoogleCloudLoggingSinkOptions ( ProjectId = "kubernation", UseJsonOutput = true, ServiceName = "cookbook" )

    let ConfigureLogging() =
        LoggerConfiguration().MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", Events.LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.GoogleCloudLogging(gclConfig)
            .CreateLogger()
