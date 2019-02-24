namespace Cookbook

open WebSharper
open WebSharper.Sitelets
open WebSharper.UI
open WebSharper.UI.Server

type EndPoint =
    | [<EndPoint "/">] Home
    | [<EndPoint "/blog">] Blog of string
    | [<EndPoint "/feed">] Feed of string

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
            |> Array.sortBy (fun post -> post.Meta.PublicationDate)
            |> Array.rev
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

    let Feed (doc : System.Xml.XmlDocument) =
        Content.Custom(
            Status=Http.Status.Ok,
            Headers = [Http.Header.Custom "Content-Type" "application/atom+xml"],
            WriteBody = fun stream ->
            use w = new System.IO.StreamWriter(stream)
            doc.Save(w)
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

    let PublishFeed (cfg : Configuration) feedType =
        match feedType with
        | "atom" ->
            match Dropbox.Auth.createDbxClient() with
            | Some client ->
                let posts =
                    Blog.loadAllPosts cfg.BlogDir client
                    |> Array.toList
                let feed = Feed.formatFeedAtom posts
                Templating.Feed feed
            | None -> WebSharper.Sitelets.Content.NotFound
        | "rss" ->
            match Dropbox.Auth.createDbxClient() with
            | Some client ->
                let posts =
                    Blog.loadAllPosts cfg.BlogDir client
                    |> Array.toList
                let feed = Feed.formatFeedRss posts
                Templating.Feed feed
            | None -> WebSharper.Sitelets.Content.NotFound
        | _ -> WebSharper.Sitelets.Content.NotImplemented


    [<Website>]
    let Main =
        let cfg = Config.loadConfig()
        Application.MultiPage (fun ctx endpoint ->
            match endpoint with
            | EndPoint.Home -> HomePage cfg
            | EndPoint.Blog (slug) -> BlogPost cfg slug
            | EndPoint.Feed (feedType) -> PublishFeed cfg feedType
        )
