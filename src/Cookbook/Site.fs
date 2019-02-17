namespace Cookbook

open WebSharper
open WebSharper.Sitelets
open WebSharper.UI
open WebSharper.UI.Server

type EndPoint =
    | [<EndPoint "/">] Home
    // | [<EndPoint "/blog">] Blog of string

module Templating =
    open WebSharper.UI.Html

    type MainTemplate = Templating.Template<"templates/main.html">

    // Compute a menubar where the menu item for the given endpoint is active
    let MenuBar (ctx: Context<EndPoint>) endpoint : Doc list =

        let ( => ) txt act =
            li [if endpoint = act then yield attr.``class`` "active"] [
               a [attr.href (ctx.Link act)] [text txt]
            ]
        [
            "Home" => EndPoint.Home
        ]

    let Main (blogPosts: BlogPost array) =
        let postSummaries =
            blogPosts
            |> Array.map(fun (post : BlogPost) -> MainTemplate.PostSummary().PostTitle(post.Title).Summary(post.Meta.Summary).Doc())

        Content.Page(
            MainTemplate()
                .Title("A Blog?!")
                .PostSumaries(postSummaries)
                .Doc()
            )

module Site =
    open WebSharper.UI.Html

    let HomePage() =
        match Dropbox.Auth.createDbxClient() with
        | Some client ->
            let posts = Blog.loadAllPosts "/the_range/test/blog" client
            Templating.Main posts
        | None -> Templating.Main [||]

    [<Website>]
    let Main =
        Application.MultiPage (fun ctx endpoint ->
            match endpoint with
            | EndPoint.Home -> HomePage()
            // | EndPoint.Blog (slug) -> BlogPost ctx slug
        )
