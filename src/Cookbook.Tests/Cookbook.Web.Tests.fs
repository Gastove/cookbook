module Cookbook.Web.Tests

open Expecto
open Giraffe.ViewEngine

open Cookbook
open Cookbook.Web

[<Tests>]
let templatingTests =
    testList
        "Testing Cookbook.Templating"
        [ testCase "Testing cleanTag"
          <| fun _ ->
              let input = ":tag:"
              let expected = "Tag"
              let got = input |> BlogPost.cleanTag

              Expect.equal got expected "We should be able to clean a tag"

          testCase "Test Templating.splitAndFormatTags with one tag"
          <| fun _ ->
              let input = ["Tag"]

              let expected =
                  div [] [
                      str "Tags: "
                      a [ _href "/blog/filter/tag/tag" ] [
                          str "Tag"
                      ]
                  ]

              let got = input |> Templating.formatTags

              Expect.equal got expected "We should be able to HTML-ize a single tag"

          testCase "Test Templating.splitAndFormatTags with multiple tags"
          <| fun _ ->
              let input = ["Tag1"; "Tag2"; "Tag3"]
              let sep = str " | "
              let expected =
                  div [] [
                      str "Tags: "
                      a [ _href "/blog/filter/tag/tag1" ] [
                          str "Tag1"
                      ]
                      sep
                      a [ _href "/blog/filter/tag/tag2" ] [
                          str "Tag2"
                      ]
                      sep
                      a [ _href "/blog/filter/tag/tag3" ] [
                          str "Tag3"
                      ]
                  ]

              let got = input |> Templating.formatTags

              Expect.equal got expected "We should be able to HTML-ize a single tag"
          ]
