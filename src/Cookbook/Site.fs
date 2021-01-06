namespace Cookbook

module Templating =

    open Giraffe.ViewEngine

    let header pageTitle =
        head [] [
            meta [ _charset "utf-8" ]
            meta [ _name "viewport"
                   _content "width=device-width, initial-scale=1.0" ]
            link [ _rel "stylesheet"
                   _type "text/css"
                   _href "/css/screen.css" ]

            link [ _rel "stylesheet"
                   _type "text/css"
                   _href "/css/prism.css" ]

            link [ _rel "stylesheet"
                   _href "https://fonts.googleapis.com/css?family=Fira+Sans|Roboto+Mono" ]

            title [] [ encodedText pageTitle ]
        ]

    let footer extra =
        let sep = str " | "

        let baseFooter =
            [ a [ _href "/" ] [ str "Home" ]
              sep
              a [ _href "https://gastove.com" ] [
                  str "gastove.com"
              ]
              sep
              a [ _href "https://gitlab.com/gastove" ] [
                  str "Gitlab"
              ]
              sep
              a [ _href "https://github.com/Gastove" ] [
                  str "Github"
              ]
              sep
              a [ _href "/feed/atom" ] [ str "Atom" ]
              br []
              br []
              str "© Ross Donaldson" ]

        footer [] (List.append baseFooter extra)

    let postSummary (blogPost: BlogPost) =
        div [] [
            h3 [] [
                a [ _href $"/blog/{blogPost.Meta.Slug}" ] [
                    str $"{blogPost.Title}"
                ]
            ]
            str $"{blogPost.Meta.Summary}"
        ]

    let postSummaries blogTitle (posts: BlogPost array) =
        let hdr =
            h1 [ _class "blog-title" ] [
                encodedText $"> {blogTitle}_"
            ]

        let summaries =
            posts
            |> Array.sortByDescending (fun p -> p.Meta.PublicationDate)
            |> Array.map postSummary
            |> List.ofArray

        [ hdr; hr [] ] @ summaries @ [ hr [] ]

    let postFooterExtras = [ script [ _src "/js/prism.js" ] [] ]

    let postView (blogPost: BlogPost) =
        [ div [ _class "post" ] [
            h2 [ _class "post-title" ] [
                a [ _href "/" ] [ str "> $HOME" ]
                str $"/blog/{blogPost.Title}"
            ]
            hr []
            div [] [ rawText blogPost.Body ]
            hr []
          ]
          div [ _class "post-info" ] [
              p [] [
                  str $"Posted: {blogPost.Meta.PublicationDate}"
              ]
              p [] [
                  str $"Tags: {blogPost.Meta.Tags}"
              ]
          ] ]

    let page pageTitle footerExtras pageBody =
        html [] [
            header pageTitle
            body [] [
                div [ _class "content" ] pageBody
            ]
            footer footerExtras
        ]

module Handlers =

    open FSharp.Control.Tasks

    open Giraffe

    // Caching TTL
    let oneHour = 3600

    let indexHandler () =
        handleContext
            (fun ctx ->
                task {
                    let cfg = Config.loadConfig ()
                    let blogTitle = "blog.gastove.com"

                    match Dropbox.Auth.createDbxClient () with
                    | Some client ->
                        use client = client
                        let posts = Blog.loadAllPosts cfg.BlogDir client

                        let summaries =
                            posts |> (Templating.postSummaries blogTitle)

                        let view =
                            Templating.page blogTitle List.empty summaries

                        return! ctx.WriteHtmlViewAsync view

                    | None ->
                        ctx.SetStatusCode 500
                        return Some ctx
                })

    let feedHandler () =
        let cfg = Config.loadConfig ()

        handleContext (fun ctx ->
            task {
                    match Dropbox.Auth.createDbxClient () with
                    | Some client ->
                        use client = client
                        let posts = Blog.loadAllPosts cfg.BlogDir client

                        let feed = Feed.formatFeed <| List.ofArray posts

                        ctx.SetContentType "application/atom+xml"

                        return! ctx.WriteBytesAsync <| System.Text.Encoding.UTF8.GetBytes(feed.OuterXml)

                    | None ->
                        ctx.SetStatusCode 500
                        return Some ctx
                }
            )

    let blogPostHandler (slug: string) =

        handleContext
            (fun ctx ->
                task {
                    let cfg = Config.loadConfig ()
                    let blogTitle = "blog.gastove.com"
                    let postFile = $"{slug}.html"

                    match Dropbox.Auth.createDbxClient () with
                    | Some client ->
                        use client = client
                        let post = Blog.loadPost cfg.BlogDir postFile client

                        let postPage = post |> Templating.postView

                        let view =
                            Templating.page blogTitle Templating.postFooterExtras postPage

                        return! ctx.WriteHtmlViewAsync view

                    | None ->
                        ctx.SetStatusCode 500
                        return Some ctx
                })

    let cachingIndexHandler () =
        publicResponseCaching oneHour None
        >=> indexHandler()

    let cachingFeedHandler () =
        publicResponseCaching oneHour None
        >=> feedHandler()

    let cachingBlogPostHandler blogPost =
        publicResponseCaching oneHour None
        >=> (blogPostHandler blogPost)
