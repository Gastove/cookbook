namespace Cookbook.Common

module String =

    let capitalizeFirst (s: string) =
        let start = s |> Seq.head |> System.Char.ToUpper

        [| [| start |]
           s |> Seq.toArray |> Array.tail |]
        |> Array.concat
        |> System.String

    let isNullOrWhiteSpace = System.String.IsNullOrWhiteSpace
    let notNullOrWhiteSpace = isNullOrWhiteSpace >> not


module List =

    let interpose (elem: 'T) (data: 'T list) =
        let rec work remaining acc =
            match remaining with
            | head :: [] -> (head :: elem :: acc) |> List.rev
            | head :: rest -> work rest (head :: elem :: acc)
            | [] -> acc |> List.rev

        match data with
        | [] -> []
        | _head :: [] -> data
        | head :: rest -> work rest [ head ]

module Result =
    let gather (results: Result<'T, 'E> seq) =
        let rec work remaining acc =
            match remaining with
                | [] -> acc |> List.rev |> Ok
                | res :: rest ->
                    res
                    |> Result.bind (fun ok -> work rest (ok :: acc))

        work (results |> Seq.toList) []
