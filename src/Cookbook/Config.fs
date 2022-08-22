namespace Cookbook

module Constants =
    let BlogDirEnvVar = "BLOG_DIR"
    let BlogDirDefault = "html/blog"
    let PageDirDefault = "html/pages"
    let GifsDir = "/gifs"
    let StaticAssetsResyncIntervalSeconds = 600
    let StaticAssetsBucket = "static.gastove.com"
    let StaticAssetsGifsPrefix = "gifs"
    let StaticAssetsImgagesPrefix = "img"

type CookbookConfig() =
    member val BlogDir = Constants.BlogDirDefault with get, set
    member val PagesDir = Constants.PageDirDefault with get, set
    member val StaticAssetsBucket = Constants.StaticAssetsBucket with get, set
