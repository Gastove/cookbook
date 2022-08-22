namespace Cookbook

module Templating =

    open Giraffe.ViewEngine

    let LinkedHome = a [ _href "/" ] [ str "$HOME/" ]
    let LinkedBlog = a [ _href "/blog" ] [ str "blog" ]

    let linkedTitle links title =
        [ h1 [ _class "page-title" ] ([ str "> " ] @ links @ [ str title ])
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
                  str "Gitlab"
              ]
              sep
              a [ _href "https://github.com/Gastove" ] [
                  str "Github"
              ]
              sep
              a [ _href "/feed/atom" ] [ str "Atom" ]
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
            linkedTitle [ LinkedHome; LinkedBlog ] "/index"

        let summaries =
            posts
            |> Array.sortByDescending (fun p -> p.Meta.PublicationDate)
            |> Array.map postSummary
            |> List.ofArray

        hdr @ summaries @ [ hr [] ]

    let postFooterExtras = [ script [ _src "/js/prism.js" ] [] ]

    let postView (blogPost: BlogPost) =
        let publicationDate =
            blogPost.Meta.PublicationDate.ToString("dddd, MMMM d yyyy")

        [ div [ _class "post" ] [
            h2 [ _class "post-title" ] [
                a [ _href "/" ] [ str "> $HOME" ]
                a [ _href "/blog" ] [ str "/blog" ]
                str $"/{blogPost.Title}"
            ]
            hr []
            div [] [ rawText blogPost.Body ]
            hr []
          ]
          div [ _class "post-info" ] [
              p [] [
                  str $"Posted: {publicationDate}"
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

    type ContentFunc =
        GCP.Storage.IStorageClient
            -> CookbookConfig
            -> ILogger
            -> System.Threading.Tasks.Task<Giraffe.ViewEngine.HtmlElements.XmlNode>

    let htmlHandlerMaker (func: ContentFunc) =
        handleContext (fun ctx ->
            let cfg =
                ctx.GetService<IOptions<CookbookConfig>>().Value

            task {
                match GCP.Storage.StorageClient.TryCreate() with
                | Ok client ->
                    let! result = func client cfg Log.Logger
                    return! ctx.WriteHtmlViewAsync result
                | Error exn ->
                    ctx.SetStatusCode 500
                    return! ctx.WriteTextAsync(exn |> string)
            })

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
                        Templating.page
                            "gastove.com"
                            List.empty
                            (header
                             @ [ (Giraffe.ViewEngine.HtmlElements.rawText contents) ])

                    return page
                // TODO[gastove|2022-08-21] Need a 404/500 view.
                | Error _exn -> return Giraffe.ViewEngine.HtmlElements.div [] []
            }

    let blogIndexContent (client: GCP.Storage.IStorageClient) (cfg: CookbookConfig) logger =
        task {
            let blogTitle = "gastove.com/blog"

            let! posts = Blog.loadAllPosts cfg.StaticAssetsBucket cfg.BlogDir client logger

            let summaries = posts |> Templating.postSummaries

            let view =
                Templating.page blogTitle List.empty summaries

            IndexHits.Inc()
            return view
        }

    let blogPostContent (slug: string) =
        fun client (cfg: CookbookConfig) logger ->
            task {
                let blogTitle = "blog.gastove.com"
                let postFile = $"{slug}.html"

                let! post = Blog.loadPost cfg.StaticAssetsBucket $"{cfg.BlogDir}/{postFile}" client logger

                let postPage = post |> Templating.postView

                let view =
                    Templating.page blogTitle Templating.postFooterExtras postPage

                PostHits.Labels([| slug |]).Inc()

                return view
            }

    let pageHandler (slug: string) = slug |> pageContent |> htmlHandlerMaker

    let blogIndexHandler () = htmlHandlerMaker blogIndexContent

    let blogPostHandler (slug: string) =
        slug |> blogPostContent |> htmlHandlerMaker

    // It's sort of irksome that this doesn't fit into the pattern I've
    // established above, but, it has to set a specialized content type, and
    // it's writing an XmlDocument, not an XmlNode. Alas.
    let feedHandler () =
        handleContext (fun ctx ->
            let cfg =
                ctx.GetService<IOptions<CookbookConfig>>().Value

            task {
                match GCP.Storage.StorageClient.TryCreate() with
                | Ok client ->

                    let! posts = Blog.loadAllPosts cfg.StaticAssetsBucket cfg.BlogDir client Log.Logger

                    let feed = Feed.formatFeed <| List.ofArray posts

                    ctx.SetContentType "application/atom+xml"

                    return! ctx.WriteStringAsync feed.OuterXml

                | Error _ ->
                    ctx.SetStatusCode 500
                    return Some ctx
            })

    let cachingBlogIndexHandler () =
        publicResponseCaching (oneHour.TotalSeconds |> int) None
        >=> blogIndexHandler ()

    let cachingFeedHandler () =
        publicResponseCaching (oneHour.TotalSeconds |> int) None
        >=> feedHandler ()

    let cachingBlogPostHandler blogPost =
        publicResponseCaching (oneHour.TotalSeconds |> int) None
        >=> (blogPostHandler blogPost)

    let cachingPageHandler slug =
        publicResponseCaching (oneHour.TotalSeconds |> int) None
        >=> (pageHandler slug)
