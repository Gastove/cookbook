module Cookbook.Server.Tests

open System
open System.Net
open System.Net.Http

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.TestHost
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting

open Expecto
open Serilog

open Cookbook

let getTestHost () =
    Log.Logger <- Logging.ConfigureBootstrapLogger()

    HostBuilder()
        .ConfigureWebHost(fun whb ->
            whb
                .UseTestServer()
                .Configure(Server.configureApp)
                .ConfigureServices(Server.configureServices)
                .ConfigureServices(fun services -> services.AddRouting() |> ignore)
            |> ignore)
        .UseSerilog(Logging.ConfigureRuntimeLogger).StartAsync()

let testRequest (request: HttpRequestMessage) =
    let resp =
        task {
            use! server = getTestHost()
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
        [ testCase "Does / return a 200?"
          <| fun _ ->
              let response =
                  testRequest (new HttpRequestMessage(HttpMethod.Get, "/"))

              printfn $"Server sez: {response.Content |> contentReader}"
              Expect.equal response.StatusCode HttpStatusCode.OK "The root resource should return a 200" ]
