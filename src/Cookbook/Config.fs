namespace Cookbook

type Configuration =
    { BlogDir: string
      PagesDir: string
      StaticAssetsBucket: string }

module Constants =
    let BlogDirEnvVar = "BLOG_DIR"
    let BlogDirDefault = "html/blog"
    let PageDirDefault = "html/pages"
    let GifsDir = "/gifs"
    let StaticAssetsResyncIntervalSeconds = 600
    let StaticAssetsBucket = "static.gastove.com"
    let StaticAssetsGifsPrefix = "gifs"
    let StaticAssetsImgagesPrefix = "img"

module Config =
    open System

    let loadConfig () =
        let maybeBlogDir =
            Environment.GetEnvironmentVariable(Constants.BlogDirEnvVar)

        let blogDir =
            if String.IsNullOrEmpty(maybeBlogDir) then
                Constants.BlogDirDefault
            else
                maybeBlogDir

        { BlogDir = blogDir
          PagesDir = Constants.PageDirDefault
          StaticAssetsBucket = Constants.StaticAssetsBucket }
