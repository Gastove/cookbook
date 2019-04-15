namespace Cookbook

open WebSharper
open WebSharper.Sitelets
open WebSharper.UI
open WebSharper.UI.Server

type EndPoint =
    | [<EndPoint "/">] Home
    | [<EndPoint "/blog">] Blog of string
    | [<EndPoint "/feed/atom">] Feed

module Templating =
    open WebSharper.UI.Html

    type MainTemplate = Templating.Template<"templates/main.html">
    type HeaderTemplate = Templating.Template<"templates/header.html">
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

    let Header (title : string) =
        HeaderTemplate()
            .Title(title)
            .Doc()

    // Footer requires no templating, so we just compute it.
    let Footer (extra : Doc list) =
        let sep = text " | "
        let baseFooter = [
            a [attr.href "/"] [text "Home"]
            sep
            a [attr.href "https://gastove.com"] [text "gastove.com"]
            sep
            a [attr.href "https://gitlab.com/gastove"] [text "Gitlab"]
            sep
            a [attr.href "https://github.com/Gastove"] [text "Github"]
            sep
            a [attr.href "/feed/atom"] [text "Atom"]
            br [] []
            br [] []
            text "© Ross Donaldson"
            ]

        footer [] (List.append baseFooter extra)

    let Main (blogPosts: BlogPost array) =
        let title = "blog.gastove.com"
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
                .Header(Header title)
                .Title(title)
                .PostSumaries(postSummaries)
                .Footer(Footer List.Empty)
                .Doc()
            )

    let Post (post: BlogPost) =
        let scripts = [
                script [attr.src "/js/prism.js"] []
            ]

        Content.Page(
            PostTemplate()
                .Header(Header post.Title)
                .PostTitle(post.Title)
                .Body(post.Body)
                .PostDate(post.Meta.PublicationDate.ToLongDateString())
                .Tags(post.Meta.Tags)
                .Footer(Footer scripts)
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
            client.Dispose()
            Templating.Main posts
        | None -> Templating.Main [||]


    let BlogPost (cfg : Configuration) slug =
        let postFile = slug + ".html"
        match Dropbox.Auth.createDbxClient() with
        | Some client ->
            let post = Blog.loadPost cfg.BlogDir postFile client
            Templating.Post post
        | None -> Templating.Main [||]

    let PublishFeed (cfg : Configuration) =
        match Dropbox.Auth.createDbxClient() with
        | Some client ->
            let posts =
                Blog.loadAllPosts cfg.BlogDir client
                |> Array.toList
            let feed = Feed.formatFeed posts
            Templating.Feed feed
        | None -> WebSharper.Sitelets.Content.NotFound

    [<Website>]
    let Main =
        let cfg = Config.loadConfig()
        Application.MultiPage (fun ctx endpoint ->
            match endpoint with
            | EndPoint.Home -> HomePage cfg
            | EndPoint.Blog (slug) -> BlogPost cfg slug
            | EndPoint.Feed -> PublishFeed cfg
        )
