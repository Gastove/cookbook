namespace Cookbook.Web

module Content =

    open System.Threading.Tasks

    open Giraffe.ViewEngine.HtmlElements
    open Serilog

    open Cookbook

    type Content =
        | HTMLView of XmlNode
        | StringData of contentType: string * data: string

    type ContentResponse = Result<Content, int * string>

    type ContentFunc = IStorageClient -> CookbookConfig -> ILogger -> Task<ContentResponse>

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

    [<RequireQualifiedAccess>]
    type BlogFilter =
        | Id
        | Tag of string

        static member Default = BlogFilter.Id

        static member TryFromStringAndTerm (term: string option) (s: string) =
            match s.ToLower(), term with
            | "id", _ -> BlogFilter.Id |> Ok
            | "tag", Some (t) -> t |> Tag |> Ok
            | "tag", None ->
                "Can't filter by tag without a tag (term) specified"
                |> Error
            | wrong -> $"Can't filter on {wrong}" |> Error


        member this.RenderForHTML() =
            match this with
                | Id -> "all"
                | Tag s -> s

    let filterBlogPosts (filter: BlogFilter) (posts: BlogPost array) =
        let filterFn =
            match filter with
            | BlogFilter.Id -> fun _ -> true
            | BlogFilter.Tag tag -> fun (bp: BlogPost) -> bp.Meta.Tags.Contains(tag)

        posts |> Array.filter filterFn

    let filteredBlogIndexContent
        (filter: BlogFilter)
        (client: IStorageClient)
        (cfg: CookbookConfig)
        logger
        =
        task {
            let blogTitle = "gastove.com/blog"

            let! posts = Blog.loadAllPosts cfg.StaticAssetsBucket cfg.BlogDir client logger

            logger.Information("Filtering by {Filter}", filter)

            let summaries =
                posts
                |> filterBlogPosts filter
                |> Array.sortByDescending Blog.projectPublicationDate
                |> Templating.postSummaries (filter.RenderForHTML() |> Some)

            let view =
                Templating.page blogTitle List.empty summaries

            return view |> HTMLView |> Ok
        }


    let blogIndexContent (client: IStorageClient) (cfg: CookbookConfig) logger =
        task {
            let blogTitle = "gastove.com/blog"

            let! posts = Blog.loadAllPosts cfg.StaticAssetsBucket cfg.BlogDir client logger

            let summaries =
                posts
                |> Array.sortByDescending Blog.projectPublicationDate
                |> Templating.postSummaries None

            let view =
                Templating.page blogTitle List.empty summaries

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
