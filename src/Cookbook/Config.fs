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
    static member CookbookConfig = "CookbookConfig"

    member val BlogDir = "" with get, set
    member val PagesDir = "" with get, set
    member val StaticAssetsBucket = "" with get, set
