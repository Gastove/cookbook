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
