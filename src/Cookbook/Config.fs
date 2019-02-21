namespace Cookbook

type Configuration =
    {BlogDir : string}

module Constants =
    let BlogDirEnvVar = "BLOG_DIR"
    let BlogDirDefault = "/the_range/test/blog"

module Config =
    open System

    let loadConfig() =
        let maybeBlogDir = Environment.GetEnvironmentVariable(Constants.BlogDirEnvVar)
        let blogDir =
            if String.IsNullOrEmpty(maybeBlogDir)
            then Constants.BlogDirDefault
            else maybeBlogDir

        {BlogDir = blogDir}
