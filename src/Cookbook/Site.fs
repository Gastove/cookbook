namespace Cookbook

open WebSharper
open WebSharper.Sitelets
open WebSharper.UI
open WebSharper.UI.Server

type EndPoint =
    | [<EndPoint"/">] Home
    | [<EndPoint"/blog">] Blog of string
    | [<EndPoint"/feed/atom">] Feed

module Templating =
    open WebSharper.UI.Html

    open Serilog

    type MainTemplate = Templating.Template<"templates/main.html">

    type HeaderTemplate = Templating.Template<"templates/header.html">

    type PostTemplate = Templating.Template<"templates/post.html">

    let logger = LoggerConfiguration().WriteTo.Console().CreateLogger()

    // One hour in seconds
    let MaxAge = 3600

    let computeHeaders() =
        [ Http.Header.Custom "Cache-Control"
              (sprintf "public,max-age=%i" MaxAge) ]

    let WithCacheHeaders content =
        let headers = computeHeaders()
        content |> Content.WithHeaders headers

    // Compute a menubar where the menu item for the given endpoint is active
    let MenuBar (ctx : Context<EndPoint>) endpoint : Doc list =
        let (=>) txt act =
            li [ if endpoint = act then yield attr.``class`` "active" ]
                [ a [ attr.href (ctx.Link act) ] [ text txt ] ]
        [ "Home" => EndPoint.Home ]

    let PageHeader(title : string) =
        HeaderTemplate()
            .Title(title)
            .Doc()

    // Footer requires no templating, so we just compute it.
    let Footer(extra : Doc list) =
        let sep = text " | "

        let baseFooter =
            [ a [ attr.href "/" ] [ text "Home" ]
              sep
              a [ attr.href "https://gastove.com" ] [ text "gastove.com" ]
              sep
              a [ attr.href "https://gitlab.com/gastove" ] [ text "Gitlab" ]
              sep
              a [ attr.href "https://github.com/Gastove" ] [ text "Github" ]
              sep
              a [ attr.href "/feed/atom" ] [ text "Atom" ]
              br [] []
              br [] []
              text "© Ross Donaldson" ]
        footer [] (List.append baseFooter extra)

    let Main(blogPosts : BlogPost array) =
        let title = "blog.gastove.com"

        let postSummaries =
            blogPosts
            |> Array.sortBy (fun post -> post.Meta.PublicationDate)
            |> Array.rev
            |> Array.map
                (fun (post : BlogPost) ->
                MainTemplate.PostSummary().PostTitle(post.Title)
                            .PostSlug(post.Meta.Slug)
                            .Summary(post.Meta.Summary).Doc())
        Content.Page(MainTemplate()
                .Header(PageHeader title)
                .Title(title)
                .PostSumaries(postSummaries)
                .Footer(Footer List.Empty)
                .Doc()) |> WithCacheHeaders

    let Post(post : BlogPost) =
        let scripts = [ script [ attr.src "/js/prism.js" ] [] ]
        Content.Page(PostTemplate()
                .Header(PageHeader post.Title)
                .PostTitle(post.Title)
                .Body(post.Body)
                .PostDate(post.Meta.PublicationDate.ToLongDateString())
                .Tags(post.Meta.Tags)
                .Footer(Footer scripts)
                .Doc()) |> WithCacheHeaders

    let Feed(doc : System.Xml.XmlDocument) =
        let feedHeaders =
            [ [ Http.Header.Custom "Content-Type" "application/atom+xml" ]
              computeHeaders() ]
            |> List.concat
        Content.Custom(Status = Http.Status.Ok, Headers = feedHeaders,
                       WriteBody = fun stream ->
                           use w = new System.IO.StreamWriter(stream)
                           doc.Save(w))

module Site =

    let HomePage(cfg : Configuration) =
        match Dropbox.Auth.createDbxClient() with
        | Some client ->
            use client = client
            let posts = Blog.loadAllPosts cfg.BlogDir client
            Templating.Main posts
        | None -> Templating.Main [||]

    let BlogPost (cfg : Configuration) slug =
        let postFile = slug + ".html"
        match Dropbox.Auth.createDbxClient() with
        | Some client ->
            use client = client
            let post = Blog.loadPost cfg.BlogDir postFile client
            Templating.Post post
        | None -> Templating.Main [||]

    let PublishFeed(cfg : Configuration) =
        match Dropbox.Auth.createDbxClient() with
        | Some client ->
            use client = client
            let posts = Blog.loadAllPosts cfg.BlogDir client |> Array.toList
            let feed = Feed.formatFeed posts
            Templating.Feed feed
        | None -> WebSharper.Sitelets.Content.NotFound

    [<Website>]
    let Main =
        let logger = Logging.ConfigureLogging()
        let cfg = Config.loadConfig()
        Async.Start <| Static.Sync.runSync cfg logger
        Application.MultiPage(fun _ctx endpoint ->
            match endpoint with
            | EndPoint.Home -> HomePage cfg
            | EndPoint.Blog(slug) -> BlogPost cfg slug
            | EndPoint.Feed -> PublishFeed cfg)
