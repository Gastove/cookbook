namespace Cookbook

module Xml =

    open System
    open System.Xml.Linq

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
    </POST-META>
    """>

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
        separatePostAndMeta contents

    let readPostAndParseAsync (stream : IO.Stream) =
        async {
            use reader = new IO.StreamReader(stream)

            let! contents =
                reader.ReadToEndAsync()
                |> Async.AwaitTask

            return separatePostAndMeta contents
        }
