namespace Cookbook

module Server =

    open System

    open Microsoft.AspNetCore.Builder
    open Microsoft.AspNetCore.Cors.Infrastructure
    open Microsoft.AspNetCore.Hosting
    open Microsoft.Extensions.Configuration
    open Microsoft.Extensions.Hosting
    open Microsoft.Extensions.DependencyInjection
    open Microsoft.Extensions.Logging

    open Giraffe
    open Prometheus

    open Cookbook.Web

    module Routes =

        let webApp =
            choose [ GET
                     >=> choose [ route "/feed/atom"
                                  >=> Handlers.cachingFeedHandler ()

                                  route "/blog"
                                  >=> Handlers.cachingBlogIndexHandler ()

                                  routef "/blog/filter/tag/%s" Handlers.cachingFilteredBlogIndexHandler

                                  routef "/blog/%s" Handlers.cachingBlogPostHandler

                                  route "/" >=> Handlers.cachingPageHandler "welcome"
                                  routef "/%s" Handlers.cachingPageHandler ]
                     setStatusCode 404 >=> text "Not Found" ]

    // Google Cloud Run requires we respect the Port environment variable. I
    // can't find a satisfying way to configure that outside of code; the
    // appsettings.json system doesn't support env var reading, and anything I
    // set in the container will be set at build-time, not run-time. So. We'll
    // munge it here.
    module Ports =
        let DefaultHttp = 5000
        let Metrics = 5005

        let httpPortOrDefault() =
            try
                match (System.Environment.GetEnvironmentVariable "PORT").Trim() with
                | "" -> DefaultHttp
                | httpPort -> httpPort |> int
            with
            | _ -> DefaultHttp


    /// Display errors generated while serving traffic. Uses a totally different
    /// ILogger interface than Serilog, so due to namespace silliness, has to
    /// live here.
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
        let sp = services.BuildServiceProvider()
        let cfg = sp.GetService<IConfiguration>()
        let env = sp.GetService<IWebHostEnvironment>()

        services.Configure<CookbookConfig>(cfg.GetSection(CookbookConfig.CookbookConfig)) |> ignore
        services.AddCors() |> ignore
        services.AddGiraffe() |> ignore
        services.AddResponseCaching() |> ignore
        services.AddHealthChecks() |> ignore
        services.AddMemoryCache() |> ignore

        if env.IsDevelopment() then
            services.AddSingleton<IStorageClient, Cookbook.Storage.FileSystemStorageClient>() |> ignore
        else
            services.AddSingleton<Cookbook.IStorageClient, Cookbook.Storage.CachingGcsStorageClient>() |> ignore

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
                    .RequireHost([| $"*:{Ports.Metrics}" |])
                |> ignore

                endpoints.MapHealthChecks("/healthz") |> ignore)
            .UseHttpMetrics()
            .UseGiraffe(Routes.webApp)

module Main =
    open System
    open System.IO

    open Microsoft.AspNetCore.Builder
    open Microsoft.AspNetCore.Hosting
    open Microsoft.Extensions.Hosting

    open Serilog

    [<EntryPoint>]
    let main args =
        Log.Logger <- Logging.ConfigureBootstrapLogger()
        let contentRoot = Directory.GetCurrentDirectory()
        let webRoot = Path.Combine(contentRoot, "wwwroot")

        // TODO[gastove|2022-08-20] We're at minimum moving syncing elsewhere.
        //
        // match Static.Sync.runSync (Config.loadConfig ()) with
        // | Ok (syncer) -> syncer.Start()
        // | Error (errorValue) -> Log.Error("Failed to start sync process; error was, {errorValue}", errorValue)

        Host
            .CreateDefaultBuilder(args)
            .UseSerilog(Logging.ConfigureRuntimeLogger)
            .ConfigureWebHost(fun host ->
                host.ConfigureKestrel (fun kestrelConfig ->
                    kestrelConfig.ListenAnyIP(Server.Ports.httpPortOrDefault())
                    kestrelConfig.ListenAnyIP(Server.Ports.Metrics))
                |> ignore)
            .ConfigureWebHostDefaults(fun webHostBuilder ->
                webHostBuilder
                    .UseContentRoot(contentRoot)
                    .UseWebRoot(webRoot)
                    .Configure(Action<IApplicationBuilder> Server.configureApp)
                    .ConfigureServices(Server.configureServices)
                |> ignore)
            .Build()
            .Run()

        0
