namespace Cookbook

open WebSharper
open WebSharper.Sitelets
open WebSharper.UI
open WebSharper.UI.Server

type EndPoint =
    | [<EndPoint "/">] Home
    | [<EndPoint "/blog">] Blog of string

module Templating =
    open WebSharper.UI.Html

    type MainTemplate = Templating.Template<"templates/main.html">
    type PostTemplate = Templating.Template<"templates/post.html">

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
            |> Array.map(fun (post : BlogPost) ->
                         MainTemplate.PostSummary()
                             .PostTitle(post.Title)
                             .PostSlug(post.Meta.Slug)
                             .Summary(post.Meta.Summary)
                             .Doc()
                         )

        Content.Page(
            MainTemplate()
                .Title("A Blog?!")
                .PostSumaries(postSummaries)
                .Doc()
            )

    let Post (post: BlogPost) =
        Content.Page(
            PostTemplate()
                .Title(post.Title)
                .PostTitle(post.Title)
                .Body(post.Body)
                .PostDate(post.Meta.PublicationDate.ToLongDateString())
                .Tags(post.Meta.Tags)
                .Doc()
            )

module Site =
    open WebSharper.UI.Html

    let HomePage (cfg : Configuration) =
        match Dropbox.Auth.createDbxClient() with
        | Some client ->
            let posts = Blog.loadAllPosts cfg.BlogDir client
            Templating.Main posts
        | None -> Templating.Main [||]


    let BlogPost (cfg : Configuration) slug =
        let postFile = slug + ".html"
        match Dropbox.Auth.createDbxClient() with
        | Some client ->
            let post = Blog.loadPost cfg.BlogDir postFile client
            Templating.Post post
        | None -> Templating.Main [||]

    [<Website>]
    let Main =
        let cfg = Config.loadConfig()
        Application.MultiPage (fun ctx endpoint ->
            match endpoint with
            | EndPoint.Home -> HomePage cfg
            | EndPoint.Blog (slug) -> BlogPost cfg slug
        )
