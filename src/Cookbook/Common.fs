namespace Cookbook.Common

module String =

    let toTitleCase (s: string) =
        let start = s |> Seq.head |> System.Char.ToUpper
        [| [|start|]; s |> Seq.toArray |> Array.tail |]
        |> Array.concat
        |> System.String
