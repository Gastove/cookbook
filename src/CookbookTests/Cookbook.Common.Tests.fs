module Cookbook.Common.Tests

open Expecto

open Cookbook.Common

[<Tests>]
let stringTests =
    testList
        "Testing extensions to String"
        [ testCase "Testing String.capitalizeFirst"
          <| fun _ ->
              let input =
                  [ "beep"
                    "beep boop"
                    "cAt"
                    "the thing in the place" ]

              let expected =
                  [ "Beep"
                    "Beep boop"
                    "CAt"
                    "The thing in the place" ]

              let got = input |> List.map String.capitalizeFirst

              Expect.equal got expected "We should only capitalize the first letter" ]

[<Tests>]
let listTests = testList "Testing extensions to List" [
    testCase "Testing List.interpose with an odd number of elements > 1" <| fun _ ->
        let input = [1; 2; 3]
        let elem = 0
        let expected = [1; elem; 2; elem; 3]
        let got = input |> List.interpose 0

        Expect.equal got expected "We should get correct imposition on odd-number element lists"

    testCase "Testing List.interpose with an even number of elements > 1" <| fun _ ->
        let input = [1; 2; 3; 4]
        let elem = 0
        let expected = [1; elem; 2; elem; 3; elem; 4]
        let got = input |> List.interpose 0

        Expect.equal got expected "We should get correct imposition on even-number element lists"

    testCase "Testing List.interpose with one element and no elements" <| fun _ ->
        Expect.equal (List.interpose 1 List.empty) [] "Interposing on an empty list should return empty list"
        Expect.equal (List.interpose 1 [1]) [1] "Interposing on a list of one element should return that element"
    ]
