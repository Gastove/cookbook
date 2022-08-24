namespace Cookbook.Common

module String =

    let capitalizeFirst (s: string) =
        let start = s |> Seq.head |> System.Char.ToUpper
        [| [|start|]; s |> Seq.toArray |> Array.tail |]
        |> Array.concat
        |> System.String

    let isNullOrWhiteSpace = System.String.IsNullOrWhiteSpace
    let notNullOrWhiteSpace = isNullOrWhiteSpace>>not
