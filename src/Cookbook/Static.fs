namespace Cookbook

module Static =

    open System

    open Serilog

    module Media =

        type Upload =
            { Media: Media
              Config: CookbookConfig
              Prefix: string }

            static member Create media cfg pre =
                { Media = media
                  Config = cfg
                  Prefix = pre }

        type UploadMaker = string -> System.IO.Stream -> Result<Upload, string>
        type UploadMakerMaker = CookbookConfig -> string -> string -> UploadMaker

        let buildUploadMaker cfg prefix : UploadMaker =
            fun name stream ->
                Media.Create name stream
                |> Result.map (fun media ->
                    { Media = media
                      Config = cfg
                      Prefix = prefix })

    module Sync =

        type SyncError = GCPClientError of string

        module Html =
            open Giraffe.ViewEngine

            let link fileName url =
                li [] [
                    a [ _href url ] [ str fileName ]
                ]

            let index links =
                html [] [
                    head [] [
                        meta [ _charset "utf-8" ]
                        meta [ _name "viewport"
                               _content "width=device-width, initial-scale=1.0" ]
                    ]
                    body [] [
                        div [] [
                            h3 [] [
                                str "Look upon my gifs ye mighty, and despair ヽ(⌐■_■)ノ♪♬"
                            ]
                            ul [] links
                        ]
                    ]
                ]


        let uploadAsync (client: IStorageClient) (upload: Media.Upload) (logger: ILogger) =
            logger.Information(
                "Uploading file {name} to {bucket}/{prefix}",
                upload.Media.FileName,
                upload.Config.StaticAssetsBucket,
                upload.Prefix
            )

            client.Put upload.Config.StaticAssetsBucket upload.Prefix upload.Media

        let createIndex (fileNames: string seq) (cfg: CookbookConfig) =
            fileNames
            |> Seq.map (fun fileName ->
                $"http://{cfg.StaticAssetsBucket}/gifs/{fileName}"
                |> Html.link fileName)
            |> Seq.toList
            |> Html.index
            |> Giraffe.ViewEngine.RenderView.AsBytes.htmlDocument

        let createAndUploadIndex client (fileNames: string seq) (cfg: CookbookConfig) (logger: ILogger) =
            let index = createIndex fileNames cfg
            let indexStream = new System.IO.MemoryStream(index)

            Media.Create "index.html" indexStream
            |> Result.map (fun media -> Media.Upload.Create media cfg "gifs")
            |> Result.map (fun upload -> uploadAsync client upload logger)

    module Markdown =

        let parse (content: string) = Markdig.Markdown.ToHtml(content)

        let loadDocument (path: string) =
            if IO.File.Exists(path) then
                task { return! IO.File.ReadAllTextAsync(path) }
                |> Ok
            else
                sprintf $"Path {path} does not exist" |> Error

        let loadDocumentFromResources (path: string) =
            let root = IO.Directory.GetCurrentDirectory()

            let resourcesDir =
                IO.Path.Combine(root, "resources", "pages", path)

            loadDocument resourcesDir
