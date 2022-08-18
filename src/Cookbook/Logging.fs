namespace Cookbook

module Logging =
    open System

    open Serilog
    open Serilog.Sinks.GoogleCloudLogging
    open Microsoft.Extensions.Hosting

    let gclConfig =
        GoogleCloudLoggingSinkOptions(ProjectId = "kubernation", UseJsonOutput = true, ServiceName = "cookbook")

    let ConfigureBootstrapLogger () =
        LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", Events.LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateBootstrapLogger()

    let ConfigureRuntimeLogger
        (context: HostBuilderContext)
        (services: IServiceProvider)
        (configuration: LoggerConfiguration)
        =
        let cfg =
            configuration
                .Destructure
                .FSharpTypes()
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Console()

        if context.HostingEnvironment.IsProduction() then
            try
                cfg.WriteTo.GoogleCloudLogging(gclConfig)
                |> ignore
            with
            | exn -> Log.Error("Failed to configure Google Cloud Logging: {error}", exn.Message)
