namespace Cookbook.Web

module Content =

    open System.Threading.Tasks

    open Giraffe.ViewEngine.HtmlElements
    open Serilog
    open Thoth.Json.Net

    open Cookbook
    open Cookbook.Common

    type Content =
        | HTMLView of XmlNode
        | StringData of contentType: string * data: string

    type ContentResponse = Result<Content, int * string>

    type ContentFunc = IStorageClient -> CookbookConfig -> Task<ContentResponse>

    let pageContent (slug: string) =
        fun client (cfg: CookbookConfig) ->
            task {
                let! maybeContents =
                    HomePage.tryLoadContent cfg.StaticAssetsBucket $"{cfg.PagesDir}/{slug}.markdown" client

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

    let loadTags (client: IStorageClient) (cfg: CookbookConfig) =
        task {
            let! tagsJson = client.Get cfg.StaticAssetsBucket $"{cfg.BlogDir}/tags-manifest.json"
            let tagsManifest = tagsJson |> Decode.fromString (Decode.field "tags" (Decode.list Decode.string))
            return tagsManifest
        }

    let filteredBlogIndexContent (filter: BlogFilter) (client: IStorageClient) (cfg: CookbookConfig) =
        task {
            let blogTitle = "gastove.com/blog"

            let! postResult = Blog.loadAllPosts cfg.StaticAssetsBucket cfg.BlogDir client

            Log.Information("Filtering by {Filter}", filter)

            let! tagsManifest = loadTags client cfg

            match (postResult |> Result.gather, tagsManifest)  with
            | Ok posts, Ok allTags ->
                let summaries =
                    posts
                    |> List.toArray
                    |> filterBlogPosts filter
                    |> Array.sortByDescending Blog.projectPublicationDate
                    |> Templating.postSummaries (filter.RenderForHTML() |> Some) allTags

                let view =
                    Templating.page blogTitle List.empty summaries

                return view |> HTMLView |> Ok

            | (_, Error e) -> return (500, e) |> Error
            | (Error e, _) -> return (500, e.Message) |> Error
        }

    let blogIndexContent (client: IStorageClient) (cfg: CookbookConfig) =
        task {
            let blogTitle = "gastove.com/blog"

            let! postResult = Blog.loadAllPosts cfg.StaticAssetsBucket cfg.BlogDir client

            let! tagsManifest = loadTags client cfg
            match (postResult |> Result.gather, tagsManifest) with
            | Ok posts, Ok allTags ->
                let summaries =
                    posts
                    |> List.toArray
                    |> Array.sortByDescending Blog.projectPublicationDate
                    |> Templating.postSummaries None allTags

                let view =
                    Templating.page blogTitle List.empty summaries

                return view |> HTMLView |> Ok
            | (_, Error e) -> return (500, e) |> Error
            | (Error e, _) -> return (500, e.Message) |> Error
        }

    let blogPostContent (slug: string) =
        fun client (cfg: CookbookConfig) ->
            task {
                let blogTitle = "gastove.com/blog"
                let postFile = $"{slug}.html"

                let! postResult = Blog.loadPost cfg.StaticAssetsBucket $"{cfg.BlogDir}/{postFile}" client

                match postResult with
                | Ok post ->
                    let postPage = post |> Templating.postView

                    let view =
                        Templating.page blogTitle Templating.postFooterExtras postPage

                    return view |> HTMLView |> Ok
                | Error e -> return (500, e.Message) |> Error
            }

    let feedContent client (cfg: CookbookConfig) =
        task {
            let! postResult = Blog.loadAllPosts cfg.StaticAssetsBucket cfg.BlogDir client

            match (postResult |> Result.gather) with
            | Ok posts ->

                let feed = Feed.formatFeed posts

                return
                    ("application/atom+xml", feed.OuterXml)
                    |> StringData
                    |> Ok
            | Error e -> return (500, e.Message) |> Error
        }
