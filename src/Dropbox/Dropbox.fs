namespace Cookbook.Dropbox


module Auth =

    open System
    open Dropbox.Api

    [<Literal>]
    let DbxKeyEnvVar = "DBX_ACCESS_TOKEN"

    let (|SomeString|NoneString|) (s : string) =
        if String.IsNullOrWhiteSpace(s) then NoneString
        else SomeString

    let loadDbxKey() =
        let got = Environment.GetEnvironmentVariable(DbxKeyEnvVar)
        match got with
        | SomeString -> Some got
        | NoneString -> None

    let createDbxClientFromKey (key : string option) =
        match key with
        | Some k -> Some (new DropboxClient(k))
        | None -> None

    let createDbxClient () =
        loadDbxKey() |> createDbxClientFromKey


module Files =

    open System
    open Dropbox.Api

    let listFilesAsync folder (dbxClient : DropboxClient) =
        folder
        |> Files.ListFolderArg
        |> dbxClient.Files.ListFolderAsync
        |> Async.AwaitTask

    let loadFilAsync folder file (dbxClient : DropboxClient) =
        [|folder; file|]
        |> IO.Path.Combine
        |> dbxClient.Files.DownloadAsync
        |> Async.AwaitTask
