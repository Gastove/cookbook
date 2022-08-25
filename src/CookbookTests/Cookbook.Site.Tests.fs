module Cookbook.Site.Tests

open Expecto
open Giraffe.ViewEngine

open Cookbook

[<Tests>]
let templatingTests =
    testList
        "Testing Cookbook.Templating"
        [ testCase "Testing cleanTag"
          <| fun _ ->
              let input = ":tag:"
              let expected = "Tag"
              let got = input |> Templating.cleanTag

              Expect.equal got expected "We should be able to clean a tag"

          testCase "Test Templating.splitAndFormatTags with one tag"
          <| fun _ ->
              let input = ":tag:"

              let expected =
                  div [] [
                      str "Tags: "
                      a [ _href "/blog/filter/tag/tag" ] [
                          str "Tag "
                      ]
                  ]

              let got = input |> Templating.splitAndFormatTags

              Expect.equal got expected "We should be able to HTML-ize a single tag" ]
