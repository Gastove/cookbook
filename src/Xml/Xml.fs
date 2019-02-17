namespace Cookbook

open FSharp.Data


type PostMeta = XmlProvider<"""
    <POST-META>
        <EXPORT_FILE_NAME>/home/gastove/Dropbox/the_range/test/blog/post-one.html</EXPORT_FILE_NAME>
        <TITLE>Post One</TITLE>
        <CATEGORIES>
            <CATEGORY>Bloggery</CATEGORY>
        </CATEGORIES>
        <SLUG>post-one</SLUG>
        <PUBLICATION_DATE>2017-09-24 15:26:01</PUBLICATION_DATE>
        <TAGS>:live:</TAGS>
        <ITEM>Post One</ITEM>
        <SUMMARY>In this post, there will be the word, "Words", followed by a question mark.</SUMMARY>
    </POST-META>
    """>


type BlogPost =
    {Body: WebSharper.UI.Doc
     Title: string
     Raw: string
     Meta: PostMeta.PostMeta
     }


module Xml =

    open System

    let loadPost path =
        IO.File.OpenRead(path)

    let separatePostAndMeta (doc : string) =
        let startTag = "<POST-META>"
        let endTag = "</POST-META>"

        let startIdx = doc.IndexOf(startTag)
        let endIdx = doc.IndexOf(endTag)

        let post = doc.[0..(startIdx - 1)]
        let metaStr = doc.[startIdx..(endIdx + endTag.Length)]

        (post, PostMeta.Parse(metaStr))

    let readPostAndParse (stream : IO.Stream) =
        use reader = new IO.StreamReader(stream)
        let contents = reader.ReadToEnd()
        let (post, meta) = separatePostAndMeta contents
        let postBody = WebSharper.UI.Doc.Verbatim post
        {Body = postBody
         Title = meta.Title
         Raw = post
         Meta = meta}

    let readPostAndParseAsync (stream : IO.Stream) =
        async {
            use reader = new IO.StreamReader(stream)

            let! contents =
                reader.ReadToEndAsync()
                |> Async.AwaitTask

            return separatePostAndMeta contents
        }
