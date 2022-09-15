module Cookbook.Server.Tests

open System
open System.Net
open System.Net.Http

open Microsoft.AspNetCore.TestHost
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting

open Expecto
open Serilog

open Cookbook

let TestConfig =
    let cfg = CookbookConfig()
    cfg.BlogDir <- "blog"
    cfg.PagesDir <- "pages"
    cfg.StaticAssetsBucket <- IO.Path.GetTempPath()
    cfg

let setTestConfiguration (config: CookbookConfig) =
    config.BlogDir <- TestConfig.BlogDir
    config.PagesDir <- TestConfig.PagesDir
    config.StaticAssetsBucket <- TestConfig.StaticAssetsBucket

let writePage (destDir: string) (fileName: string) (content: string) =
    let outDir =
        IO.Path.Join(TestConfig.StaticAssetsBucket, destDir)

    let outPath = IO.Path.Join(outDir, fileName)

    if IO.Directory.Exists outDir |> not then
        outDir |> IO.Directory.CreateDirectory |> ignore

    IO.File.WriteAllText(outPath, content)

let getTestHost env =
    Log.Logger <- Logging.ConfigureBootstrapLogger()

    HostBuilder()
        .ConfigureWebHost(fun whb ->
            whb
                .UseTestServer()
                .Configure(Server.configureApp)
                .ConfigureServices(Server.configureServices)
                .ConfigureServices(fun services ->
                    services
                        .AddRouting()
                        .Configure<CookbookConfig>(setTestConfiguration)
                    |> ignore)
            |> ignore)
        .UseSerilog(Logging.ConfigureRuntimeLogger)
        .UseEnvironment(env)
        .StartAsync()

let testRequest env (request: HttpRequestMessage) =
    let resp =
        task {
            use! server = getTestHost env
            let testServer = server.GetTestServer()
            use client = testServer.CreateClient()
            let! response = request |> client.SendAsync
            return response
        }

    resp.Result

let contentReader (content: HttpContent) =
    let result =
        task { return! content.ReadAsStringAsync() }

    result.Result

[<Tests>]
let webserverTests =
    testList
        "Testing our HTTP Server"
        [ testCase "Does / return a 200 in Test?"
          <| fun _ ->
              writePage TestConfig.PagesDir "welcome.markdown" "hi"

              let response =
                  testRequest "Test" (new HttpRequestMessage(HttpMethod.Get, "/"))

              Expect.equal response.StatusCode HttpStatusCode.OK "The root resource should return a 200" ]
