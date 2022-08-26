namespace Cookbook.Web

module Templating =

    open Giraffe.ViewEngine

    open Cookbook
    open Cookbook.Common

    let PostDateFormat = "dddd, MMMM d yyyy"

    let LinkedHome = a [ _href "/" ] [ str "$HOME" ]
    let LinkedBlog = a [ _href "/blog" ] [ str "blog" ]

    let linkedTitle links title =
        let withSep =
            links
            |> List.rev
            |> List.fold (fun newLinks link -> link :: (str "/") :: newLinks) List.empty

        [ h1 [ _class "page-title" ] ([ str "> " ] @ withSep @ [ str title ])
          hr [] ]

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
              a [ _href "https://gitlab.com/gastove" ] [
                  str "GitLab"
              ]
              sep
              a [ _href "https://github.com/Gastove" ] [
                  str "GitHub"
              ]
              sep
              a [ _href "/feed/atom" ] [ str "Atom" ]
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
        div [] [
            h3 [] [
                a [ _href $"/blog/{blogPost.Meta.Slug}" ] [
                    str $"{blogPost.Title}"
                ]
            ]
            str $"{blogPost.Meta.Summary}"
        ]

    let pageTitle title =
        [ h1 [ _class "page-title" ] [
            encodedText $"> {title}"
          ]
          hr [] ]

    let returnSnippet (tag: string) =
        div [ _class "post-filter-return"] [
            str $"└─ Currently showing posts tagged with: {tag |> String.capitalizeFirst} "
            a [ _href "/blog" ] [
                str "[clear filter]"
            ]
        ]

    let postSummaries (currentTag: string option) (posts: BlogPost array) =
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

        hdr @ returnToUnfiltered @ summaries @ [ hr [] ]

    let postFooterExtras = [ script [ _src "/js/prism.js" ] [] ]

    // TODO[gastove|2022-08-24] Move this into XML or Blog or something -> this
    // layer should be handed pre-parsed data.
    let cleanTag (tag: string) =
        tag.Trim().Trim(':') |> String.capitalizeFirst

    let formatTag (tag: string) =
        a [ _href $"/blog/filter/tag/{tag.ToLower()}" ] [
            str $"{tag}"
        ]

    let splitAndFormatTags (tags: string) =
        let tags =
            tags.Split([| ':' |])
            |> List.ofArray
            |> List.filter String.notNullOrWhiteSpace
            |> List.map (cleanTag >> formatTag)
            |> List.interpose (str " | ")

        div [] (str "Tags: " :: tags)

    let postView (blogPost: BlogPost) =
        let publicationDate =
            blogPost.Meta.PublicationDate.ToString(PostDateFormat)

        let lastUpdated =
            blogPost.Meta.LastUpdated.ToString(PostDateFormat)

        [ div [ _class "post" ] [
            h2 [ _class "post-title" ] [
                str "> "
                a [ _href "/" ] [ str "$HOME" ]
                str "/"
                a [ _href "/blog" ] [ str "blog" ]
                str $"/\"{blogPost.Title}\""
            ]
            hr []
            div [] [ rawText blogPost.Body ]
            hr []
          ]
          div [ _class "post-info" ] [
              p [] [
                  str $"Originally Posted: {publicationDate}"
                  br []
                  str $"Last Updated On: {lastUpdated}"
              ]
              p [] [
                  blogPost.Meta.Tags |> splitAndFormatTags
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
