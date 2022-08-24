namespace Cookbook

module Templating =

    open Giraffe.ViewEngine

    open Cookbook.Common

    let PostDateFormat = "dddd, MMMM d yyyy"

    let LinkedHome = a [ _href "/" ] [ str "$HOME" ]
    let LinkedBlog = a [ _href "/blog" ] [ str "blog" ]

    let linkedTitle links title =
        let withSep =
            links
            |> List.rev
            |> List.fold (fun newLinks link -> link :: (str "/") :: newLinks) List.empty

        [ h1 [ _class "page-title" ] ([ str "> " ] @ withSep @ [ str title ])
          hr [] ]

    let header pageTitle =
        head [] [
            meta [ _charset "utf-8" ]
            meta [ _name "viewport"
                   _content "width=device-width, initial-scale=1.0" ]
            link [ _rel "stylesheet"
                   _type "text/css"
                   _href "/css/screen.css" ]

            link [ _rel "stylesheet"
                   _type "text/css"
                   _href "/css/prism.css" ]

            link [ _rel "stylesheet"
                   _href "https://fonts.googleapis.com/css?family=Fira+Sans|Roboto+Mono" ]

            title [] [ encodedText pageTitle ]
        ]

    let footer extra =
        let sep = str " | "

        let baseFooter =
            [ a [ _href "/" ] [ str "Home" ]
              sep
              a [ _href "https://gitlab.com/gastove" ] [
                  str "GitLab"
              ]
              sep
              a [ _href "https://github.com/Gastove" ] [
                  str "GitHub"
              ]
              sep
              a [ _href "/feed/atom" ] [ str "Atom" ]
              sep
              a [ _href "/blog" ] [ str "Blog!" ]
              sep
              a [ _href "/about" ] [ str "About" ]
              sep
              a [ _href "/projects" ] [
                  str "Projects"
              ]
              sep
              a [ _href "/colophon" ] [
                  str "Colophon"
              ]
              br []
              br []
              str "© Ross M. Donaldson, 2022" ]

        footer [] (List.append baseFooter extra)

    let postSummary (blogPost: BlogPost) =
        div [] [
            h3 [] [
                a [ _href $"/blog/{blogPost.Meta.Slug}" ] [
                    str $"{blogPost.Title}"
                ]
            ]
            str $"{blogPost.Meta.Summary}"
        ]

    let pageTitle title =
        [ h1 [ _class "page-title" ] [
            encodedText $"> {title}"
          ]
          hr [] ]

    let postSummaries (posts: BlogPost array) =
        let hdr =
            linkedTitle [ LinkedHome; LinkedBlog ] "index"

        let summaries =
            posts
            |> Array.sortByDescending (fun p -> p.Meta.PublicationDate)
            |> Array.map postSummary
            |> List.ofArray

        hdr @ summaries @ [ hr [] ]

    let postFooterExtras = [ script [ _src "/js/prism.js" ] [] ]

    let formatTag (tag: string) =
        tag.Trim().Trim(':')
        |> String.toTitleCase

    let postView (blogPost: BlogPost) =
        let publicationDate =
            blogPost.Meta.PublicationDate.ToString(PostDateFormat)

        let lastUpdated =
            blogPost.Meta.LastUpdated.ToString(PostDateFormat)

        [ div [ _class "post" ] [
            h2 [ _class "post-title" ] [
                str "> "
                a [ _href "/" ] [ str "$HOME" ]
                str "/"
                a [ _href "/blog" ] [ str "blog" ]
                str $"/\"{blogPost.Title}\""
            ]
            hr []
            div [] [ rawText blogPost.Body ]
            hr []
          ]
          div [ _class "post-info" ] [
              p [] [
                  str $"Originally Posted: {publicationDate}"
                  br []
                  str $"Last Updated On: {lastUpdated}"
              ]
              p [] [
                  str $"Tags: {blogPost.Meta.Tags}"
              ]
          ] ]

    let page pageTitle footerExtras pageBody =
        html [] [
            header pageTitle
            body [] [
                div [ _class "content" ] pageBody
            ]
            footer footerExtras
        ]

module Handlers =

    open Giraffe
    open Prometheus
    open Serilog

    open Microsoft.Extensions.Options

    let IndexHits =
        Metrics.CreateCounter("index_gets", "How many hits to the root resource of the blog?")

    let PostHits =
        Metrics.CreateCounter(
            "blog_post_hits",
            "How many hits to blog posts?",
            CounterConfiguration(LabelNames = [| "post" |])
        )

    // Caching TTL
    let oneHour = System.TimeSpan.FromHours(1)

    module Content =
        open System.Threading.Tasks
        open Giraffe.ViewEngine.HtmlElements

        type Content =
            | HTMLView of XmlNode
            | StringData of contentType: string * data: string

        type ContentResponse = Result<Content, int * string>

        type ContentFunc = GCP.Storage.IStorageClient -> CookbookConfig -> ILogger -> Task<ContentResponse>

        let pageContent (slug: string) =
            fun client (cfg: CookbookConfig) logger ->
                task {
                    let! maybeContents =
                        HomePage.tryLoadContent cfg.StaticAssetsBucket $"{cfg.PagesDir}/{slug}.markdown" client logger

                    match maybeContents with
                    | Ok contents ->

                        let header =
                            Templating.linkedTitle [ Templating.LinkedHome ] $"{slug}.md"

                        let page =
                            Templating.page "gastove.com" List.empty (header @ [ (rawText contents) ])

                        return page |> HTMLView |> Ok
                    | Error exn -> return (500, exn |> string) |> Error
                }

        let blogIndexContent (client: GCP.Storage.IStorageClient) (cfg: CookbookConfig) logger =
            task {
                let blogTitle = "gastove.com/blog"

                let! posts = Blog.loadAllPosts cfg.StaticAssetsBucket cfg.BlogDir client logger

                let summaries = posts |> Templating.postSummaries

                let view =
                    Templating.page blogTitle List.empty summaries

                IndexHits.Inc()
                return view |> HTMLView |> Ok
            }

        let blogPostContent (slug: string) =
            fun client (cfg: CookbookConfig) logger ->
                task {
                    let blogTitle = "gastove.com/blog"
                    let postFile = $"{slug}.html"

                    let! post = Blog.loadPost cfg.StaticAssetsBucket $"{cfg.BlogDir}/{postFile}" client logger

                    let postPage = post |> Templating.postView

                    let view =
                        Templating.page blogTitle Templating.postFooterExtras postPage

                    PostHits.Labels([| slug |]).Inc()

                    return view |> HTMLView |> Ok
                }

        let feedContent client (cfg: CookbookConfig) logger =
            task {
                let! posts = Blog.loadAllPosts cfg.StaticAssetsBucket cfg.BlogDir client logger

                let feed = Feed.formatFeed <| List.ofArray posts

                return
                    ("application/atom+xml", feed.OuterXml)
                    |> StringData
                    |> Ok
            }

    let handlerMaker (func: Content.ContentFunc) =
        handleContext (fun ctx ->
            let cfg =
                ctx.GetService<IOptions<CookbookConfig>>().Value

            let storageClient =
                ctx.GetService<GCP.Storage.IStorageClient>()

            task {
                let! result = func storageClient cfg Log.Logger

                match result with
                | Ok content ->
                    match content with
                    | Content.HTMLView view -> return! ctx.WriteHtmlViewAsync view
                    | Content.StringData (contentType, data) ->
                        ctx.SetContentType contentType
                        return! ctx.WriteStringAsync data

                | Error ((statusCode, errMsg)) ->
                    ctx.SetStatusCode statusCode
                    return! ctx.WriteStringAsync errMsg
            })


    let pageHandler (slug: string) =
        slug |> Content.pageContent |> handlerMaker

    let blogIndexHandler () = handlerMaker Content.blogIndexContent

    let blogPostHandler (slug: string) =
        slug |> Content.blogPostContent |> handlerMaker

    let feedHandler () = Content.feedContent |> handlerMaker

    let cache =
        publicResponseCaching (oneHour.TotalSeconds |> int) None

    let cachingBlogIndexHandler () = cache >=> blogIndexHandler ()

    let cachingFeedHandler () = cache >=> feedHandler ()

    let cachingBlogPostHandler blogPost = cache >=> (blogPostHandler blogPost)

    let cachingPageHandler slug = cache >=> (pageHandler slug)
