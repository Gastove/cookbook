﻿namespace Cookbook

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

    let loadXmlDoc path =
        let file = IO.File.OpenRead(path)
        let reader = new IO.StreamReader(file)
        let doc = Xml.XmlDocument()
        doc.Load(reader)
        doc

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


module Feed =

    open System

    let hostName = "http://35.185.199.14/"
    let blogPath = hostName + "blog/"

    module Atom =

        let loadBase() =
            Xml.loadXmlDoc("atom.xml")

        let postToItem (doc : Xml.XmlDocument) (post : BlogPost) =
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
            published.InnerText <- post.Meta.PublicationDate.ToString("s")
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

        let updateFeedWith (feed : Xml.XmlDocument) (post : BlogPost) =
            let item = postToItem feed post
            feed.DocumentElement.AppendChild(item) |> ignore
            feed

    module RSS =

        let loadBase() =
            Xml.loadXmlDoc("rss.xml")

        let postToItem (doc : Xml.XmlDocument) (post : BlogPost) =
            let item = doc.CreateElement("item")
            let title = doc.CreateElement("title")
            let link = doc.CreateElement("link")
            let summary = doc.CreateElement("description")
            let published = doc.CreateElement("pubDate")

            summary.InnerText <- post.Meta.Summary
            title.InnerText <- post.Title
            link.InnerText <- blogPath + post.Meta.Slug
            published.InnerText <- post.Meta.PublicationDate.ToString("s")

            item.AppendChild(title) |> ignore
            item.AppendChild(summary) |> ignore
            item.AppendChild(link) |> ignore
            item.AppendChild(published) |> ignore

            item

        let updateFeedWith (feed : Xml.XmlDocument) (post : BlogPost) =
            let item = postToItem feed post
            feed.DocumentElement.AppendChild(item) |> ignore
            feed


    let formatFeedAtom (posts : BlogPost list) =
        let feed = Atom.loadBase()
        posts
        |> List.sortBy (fun post -> post.Meta.PublicationDate)
        |> List.rev
        |> List.fold Atom.updateFeedWith feed

    let formatFeedRss (posts : BlogPost list) =
        let feed = RSS.loadBase()
        posts
        |> List.sortBy (fun post -> post.Meta.PublicationDate)
        |> List.rev
        |> List.fold RSS.updateFeedWith feed
