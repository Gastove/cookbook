namespace Cookbook

open FSharp.Data

type PostMeta =
    XmlProvider<"""
    <POST-META>
        <EXPORT_FILE_NAME>/home/gastove/Dropbox/the_range/test/blog/post-one.html</EXPORT_FILE_NAME>
        <TITLE>Post One</TITLE>
        <CATEGORIES>
            <CATEGORY>Bloggery</CATEGORY>
        </CATEGORIES>
        <SLUG>post-one</SLUG>
        <PUBLICATION_DATE>2017-09-24 15:26:01</PUBLICATION_DATE>
        <LAST_UPDATED>2017-09-24 15:26:01</LAST_UPDATED>
        <TAGS>:live:</TAGS>
        <ITEM>Post One</ITEM>
        <SUMMARY>In this post, there will be the word, "Words", followed by a question mark.</SUMMARY>
    </POST-META>
    """>


type BlogPost =
    { Body: string
      Title: string
      Raw: string
      Meta: PostMeta.PostMeta }

module Xml =

    open System

    let loadPost path = IO.File.OpenRead(path)

    let separatePostAndMeta (doc: string) =
        let startTag = "<POST-META>"
        let endTag = "</POST-META>"

        let startIdx = doc.IndexOf(startTag)
        let endIdx = doc.IndexOf(endTag)

        let post = doc.[0..(startIdx - 1)]
        let metaStr = doc.[startIdx..(endIdx + endTag.Length)]

        // TODO[gastove|2022-08-27] Gotta wrap Parse in a try/catch, that sucker
        // detonates gracelessly.

        try
            let parsed = PostMeta.Parse(metaStr)
            (post, parsed) |> Ok
        with
        | exn -> exn |> Error

    let readPostAndParse (stream: IO.Stream) =
        use reader = new IO.StreamReader(stream)
        let contents = reader.ReadToEnd()

        separatePostAndMeta contents
        |> Result.map (fun (post, meta) ->
            { Body = post
              Title = meta.Title
              Raw = post
              Meta = meta })

    let parsePost (post: string) =
        separatePostAndMeta post
        |> Result.map (fun (post, meta) ->
            { Body = post
              Title = meta.Title
              Raw = post
              Meta = meta })

    let readPostAndParseAsync (stream: IO.Stream) =
        task {
            use reader = new IO.StreamReader(stream)

            let! contents = reader.ReadToEndAsync()

            return separatePostAndMeta contents
        }

module Feed =

    open System

    let hostName = "http://blog.gastove.com/"
    let blogPath = hostName + "blog/"
    let blogTitle = "blog.gastove.com"
    let dateFormat = "yyyy'-'MM'-'ddTHH':'mm':'ss'Z'"

    module Atom =

        // TODO: datetimes need to be RFC 3339 https://validator.w3.org/feed/docs/error/InvalidRFC3339Date.html
        let createBase () =
            let doc = System.Xml.XmlDocument()
            let feed = doc.CreateElement("feed")
            let feedNs = doc.CreateAttribute("xmlns")
            feedNs.Value <- "http://www.w3.org/2005/Atom"
            feed.Attributes.Append(feedNs) |> ignore

            doc.AppendChild(feed) |> ignore

            let title = doc.CreateElement("title")
            title.InnerText <- blogTitle
            doc.DocumentElement.AppendChild(title) |> ignore

            let link = doc.CreateElement("link")
            let linkHref = doc.CreateAttribute("href")
            let linkRel = doc.CreateAttribute("rel")
            linkHref.Value <- "http://35.185.199.14/feed/atom"
            linkRel.Value <- "self"
            link.Attributes.Append(linkHref) |> ignore
            link.Attributes.Append(linkRel) |> ignore
            doc.DocumentElement.AppendChild(link) |> ignore

            let updated = doc.CreateElement("updated")
            updated.InnerText <- DateTime.Now.ToString(dateFormat)
            doc.DocumentElement.AppendChild(updated) |> ignore

            let author = doc.CreateElement("author")
            let authorName = doc.CreateElement("name")
            authorName.InnerText <- "Ross Donaldson"
            author.AppendChild(authorName) |> ignore
            doc.DocumentElement.AppendChild(author) |> ignore

            let id = doc.CreateElement("id")
            id.InnerText <- hostName
            doc.DocumentElement.AppendChild(id) |> ignore

            doc


        let postToItem (doc: Xml.XmlDocument) (post: BlogPost) =
            let entry = doc.CreateElement("entry")
            let title = doc.CreateElement("title")
            let content = doc.CreateElement("content")

            let link = doc.CreateElement("link")
            let linkHref = doc.CreateAttribute("href")

            let id = doc.CreateElement("id")
            let summary = doc.CreateElement("summary")
            let published = doc.CreateElement("published")

            summary.InnerText <- post.Meta.Summary
            title.InnerText <- post.Title
            content.InnerText <- post.Body.ToString()
            published.InnerText <- post.Meta.PublicationDate.ToString(dateFormat)
            // TODO: Find a way not to hardcode like this :/
            linkHref.Value <- blogPath + post.Meta.Slug
            id.InnerText <- blogPath + post.Meta.Slug

            entry.AppendChild(title) |> ignore
            entry.AppendChild(summary) |> ignore
            entry.AppendChild(content) |> ignore
            entry.AppendChild(published) |> ignore
            link.Attributes.Append(linkHref) |> ignore
            entry.AppendChild(link) |> ignore
            entry.AppendChild(id) |> ignore

            entry

        let updateFeedWith (feed: Xml.XmlDocument) (post: BlogPost) =
            let item = postToItem feed post
            feed.DocumentElement.AppendChild(item) |> ignore
            feed

    let formatFeed (posts: BlogPost list) =
        let feed = Atom.createBase ()

        posts
        |> List.sortBy (fun post -> post.Meta.PublicationDate)
        |> List.rev
        |> List.fold Atom.updateFeedWith feed
