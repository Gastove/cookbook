namespace Cookbook

module Server =

    open System

    open Microsoft.AspNetCore.Builder
    open Microsoft.AspNetCore.Cors.Infrastructure
    open Microsoft.AspNetCore.Hosting
    open Microsoft.Extensions.Hosting
    open Microsoft.Extensions.DependencyInjection

    open Giraffe

    open Prometheus

    open Microsoft.Extensions.Logging

    module Ports =
        let Http = 5000
        let Metrics = 5005

    let webApp =
        choose [ GET
                 >=> choose [ route "/" >=> Handlers.cachingIndexHandler ()

                              routef "/blog/%s" Handlers.cachingBlogPostHandler

                              route "/feed/atom"
                              >=> Handlers.cachingFeedHandler () ]
                 setStatusCode 404 >=> text "Not Found" ]


    let errorHandler (ex: Exception) (logger: ILogger) =
        logger.LogError(ex, "An unhandled exception has occurred while executing the request.")

        clearResponse
        >=> setStatusCode 500
        >=> text ex.Message

    let configureCors (builder: CorsPolicyBuilder) =
        builder
            .WithOrigins("http://localhost:5000", "https://localhost:5001")
            .AllowAnyMethod()
            .AllowAnyHeader()
        |> ignore

    let configureServices (services: IServiceCollection) =
        services.AddCors() |> ignore
        services.AddGiraffe() |> ignore
        services.AddResponseCaching() |> ignore

    let configureApp (app: IApplicationBuilder) =
        let env =
            app.ApplicationServices.GetService<IWebHostEnvironment>()

        (match env.IsDevelopment() with
         | true -> app.UseDeveloperExceptionPage()
         | false -> app.UseGiraffeErrorHandler(errorHandler))
            .UseCors(configureCors)
            .UseStaticFiles()
            .UseResponseCaching()
            .UseRouting()
            .UseEndpoints(fun endpoints ->
                endpoints
                    .MapMetrics()
                    .RequireHost([| $"localhost:{Ports.Metrics}" |])
                |> ignore)
            .UseHttpMetrics()
            .UseGiraffe(webApp)

module Main =
    open System
    open System.IO

    open Microsoft.AspNetCore.Builder
    open Microsoft.AspNetCore.Hosting
    open Microsoft.Extensions.Hosting

    open Serilog

    [<EntryPoint>]
    let main args =
        Log.Logger <- Logging.ConfigureLogging()
        let contentRoot = Directory.GetCurrentDirectory()
        let webRoot = Path.Combine(contentRoot, "wwwroot")
        // Eventually:
        //         Async.Start <| Static.Sync.runSync cfg logger
        Host
            .CreateDefaultBuilder(args)
            .ConfigureWebHost(fun host ->
                host.ConfigureKestrel
                    (fun kestrelConfig ->
                        kestrelConfig.ListenLocalhost(Server.Ports.Http)
                        kestrelConfig.ListenLocalhost(Server.Ports.Metrics)
                        )
                |> ignore)
            .ConfigureWebHostDefaults(fun webHostBuilder ->
                webHostBuilder
                    .UseContentRoot(contentRoot)
                    .UseWebRoot(webRoot)
                    .Configure(Action<IApplicationBuilder> Server.configureApp)
                    .ConfigureServices(Server.configureServices)
                    .UseSerilog()
                |> ignore)
            .Build()
            .Run()

        0
