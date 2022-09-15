namespace Cookbook.Web

module Handlers =

    open Giraffe
    open Prometheus

    open Microsoft.Extensions.Options
    open Microsoft.Extensions.Caching.Memory

    open Cookbook

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

    let handlerMaker (func: Content.ContentFunc) =
        handleContext (fun ctx ->
            let cfg =
                ctx.GetService<IOptions<CookbookConfig>>().Value

            let storageClient =
                ctx.GetService<IStorageClient>()

            let _memCache = ctx.GetService<IMemoryCache>()

            task {
                let! result = func storageClient cfg

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

    let filteredBlogIndexHandler (tag: string) =
        tag
        |> Content.BlogFilter.Tag
        |> Content.filteredBlogIndexContent
        |> handlerMaker

    let blogPostHandler (slug: string) =
        slug |> Content.blogPostContent |> handlerMaker

    let feedHandler () = Content.feedContent |> handlerMaker

    let cache =
        publicResponseCaching (oneHour.TotalSeconds |> int) None

    let cachingBlogIndexHandler () = cache >=> blogIndexHandler ()

    let cachingFilteredBlogIndexHandler tag =
        cache >=> (filteredBlogIndexHandler tag)

    let cachingFeedHandler () = cache >=> feedHandler ()

    let cachingBlogPostHandler blogPost = cache >=> (blogPostHandler blogPost)

    let cachingPageHandler slug = cache >=> (pageHandler slug)
