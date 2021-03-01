namespace Cookbook

type Configuration =
    {BlogDir : string
     StaticAssetsBucket : string}

module Constants =
    let BlogDirEnvVar = "BLOG_DIR"
    let BlogDirDefault = "/the_range/test/blog"
    let GifsDir = "/gifs"
    let StaticAssetsResyncIntervalSeconds = 600
    let StaticAssetsBucket = "static.gastove.com"
    let StaticAssetsGifsPrefix = "gifs"
    let StaticAssetsImgagesPrefix = "img"

module Config =
    open System

    let loadConfig() =
        let maybeBlogDir = Environment.GetEnvironmentVariable(Constants.BlogDirEnvVar)
        let blogDir =
            if String.IsNullOrEmpty(maybeBlogDir)
            then Constants.BlogDirDefault
            else maybeBlogDir

        {BlogDir = blogDir
         StaticAssetsBucket = Constants.StaticAssetsBucket}
