namespace Cookbook.Web
  
module Templating =

    open Giraffe.ViewEngine

    open Cookbook
    open Cookbook.Common

    let PostDateFormat = "dddd, MMMM d yyyy @ htt"

    let LinkedHome = a [ _href "/" ] [ str "$HOME" ]
    let LinkedBlog = a [ _href "/blog" ] [ str "blog" ]

    let linkedTitle links title =
        let withSep =
            links
            |> List.rev
            |> List.fold (fun newLinks link -> link :: (str "/") :: newLinks) List.empty

        [ h1 [ _class "page-title" ] ([ str "> " ] @ withSep @ [ str title ])
          hr [] ]

    let header pageTitle extras =
        let headContents =
            [ meta [ _charset "utf-8" ]
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
            @ extras

        head [] headContents

    let blogPostPageMeta (bp: BlogPost) =
        [ meta [ _name "descrpition"
                 _content bp.Meta.Summary ]
          meta [ _name "og:descrpition"
                 _content bp.Meta.Summary ]
          meta [ _property "og:title"
                 _content bp.Title ]
          meta [ _property "og:url"
                 _content $"http://gastove.com/blog/{bp.Meta.Slug}" ] ]

    let blogPostHtmlizeTitle (title: string) =
        title.Split([| ' ' |])
        |> Array.map (fun word ->
            Serilog.Log.Information("Checking {Word}", word)

            let word =
                if word.StartsWith '~' then
                    $"<code>{word.TrimStart('~')}"
                else
                    word

            let word =
                if word.EndsWith '~' then
                    $"{word.TrimEnd('~')}</code>"
                else
                    word

            word)
        |> String.concat " "

    let footer extra =
        let sep = str " | "

        let baseFooter =
            [ a [ _href "/" ] [ str "Home" ]
              sep
              a [ _href "https://gitlab.com/gastove" ] [
                  str "GitLab"
              ]
              sep
              a [ _href "https://github.com/Gastove" ] [
                  str "GitHub"
              ]
              sep
              a [ _href "/blog/feed/atom.xml" ] [
                  str "Atom"
              ]
              sep
              a [ _href "/blog" ] [ str "Blog!" ]
              sep
              a [ _href "/about" ] [ str "About" ]
              sep
              a [ _href "/projects" ] [
                  str "Projects"
              ]
              sep
              a [ _href "/colophon" ] [
                  str "Colophon"
              ]
              br []
              br []
              str "© Ross M. Donaldson, 2022" ]

        footer [] (List.append baseFooter extra)

    let postSummary (blogPost: BlogPost) =
        let posted =
            blogPost.Meta.PublicationDate.ToString("yyyy-MM-dd")

        let updated =
            blogPost.Meta.LastUpdated.ToString("yyyy-MM-dd")

        let timeData =
            if posted = updated then
                $"└─ Posted: {posted}"
            else
                $"└─ Posted: {posted}; Updated: {updated}"
            |> str

        div [] [
            h3 [] [
                a [ _href $"/blog/{blogPost.Meta.Slug}" ] [
                    rawText $"{blogPost.Title |> blogPostHtmlizeTitle}"
                ]
                br []
                span [ _class "post-summary-time" ] [
                    timeData
                ]
            ]
            rawText $"{blogPost.Meta.Summary |> blogPostHtmlizeTitle}"
        ]

    let pageTitle title =
        [ h1 [ _class "page-title" ] [
            encodedText $"> {title}"
          ]
          hr [] ]

    let tagSelector (tags: string list) =
        tags
        |> List.map (fun tag ->
            a [ _href $"/blog/filter/tag/{tag}" ] [
                tag |> String.capitalizeFirst |> str
            ])
        |> List.interpose (str " | ")
        |> List.append [ str "└─ See only posts tagged with: " ]
        |> div [ _class "post-filter-subtext" ]

    let returnSnippet (tag: string) =
        div [ _class "post-filter-subtext" ] [
            str $"└─ Currently showing posts tagged with: {tag |> String.capitalizeFirst} "
            a [ _href "/blog" ] [
                str "[clear filter]"
            ]
        ]

    let postSummaries (currentTag: string option) (allTags: string list) (posts: BlogPost array) =
        let hdr =
            linkedTitle [ LinkedHome; LinkedBlog ] "index"

        let returnToUnfiltered =
            currentTag
            |> Option.map (returnSnippet >> List.singleton)
            |> Option.defaultValue List.empty

        let summaries =
            posts
            |> Array.sortByDescending (fun p -> p.Meta.PublicationDate)
            |> Array.map postSummary
            |> List.ofArray

        let tagLinks = allTags |> tagSelector

        hdr
        @ [ tagLinks ]
          @ returnToUnfiltered @ summaries @ [ hr [] ]

    let postFooterExtras = [ script [ _src "/js/prism.js" ] [] ]

    let formatTag (tag: string) =
        a [ _href $"/blog/filter/tag/{tag.ToLower()}" ] [
            str $"{tag}"
        ]
 
    let formatTags (tags: string list) =
        let tags =
            tags
            |> List.map formatTag
            |> List.interpose (str " | ")

        div [] (str "Tags: " :: tags)

    let postView (blogPost: BlogPost) =

        let publicationDate =
            blogPost.Meta.PublicationDate.ToString(PostDateFormat)

        let lastUpdated =
            blogPost.Meta.LastUpdated.ToString(PostDateFormat)

        let dateMeta =
            if publicationDate = lastUpdated then
                [ tr [] [
                      td [] [ str "Posted:" ]
                      td [] [ str publicationDate ]
                  ] ]
            else
                [ tr [] [
                    td [] [ str "Originally Posted:" ]
                    td [] [ str publicationDate ]
                  ]
                  tr [] [
                      td [] [ str "Last Updated On:" ]
                      td [] [ str lastUpdated ]
                  ] ]

        [ div [ _class "post" ] [
            h2 [ _class "post-title" ] [
                str "> "
                a [ _href "/" ] [ str "$HOME" ]
                str "/"
                a [ _href "/blog" ] [ str "blog" ]
                str "/\""
                rawText (blogPost.Title |> blogPostHtmlizeTitle)
                str "\""
            ]
            hr []
            div [] [ rawText blogPost.Body ]
            hr []
          ]
          div [ _class ".post-info" ] [
              table [ _class ".post-dates" ] dateMeta
              p [] [
                  blogPost.Tags |> formatTags
              ]
          ] ]

    let page pageTitle headerExtras footerExtras pageBody =
        html [] [
            header pageTitle headerExtras
            body [] [
                div [ _class "content" ] pageBody
            ]
            footer footerExtras
        ]
