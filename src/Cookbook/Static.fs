namespace Cookbook

module Static =

    open System
    open System.Threading.Tasks

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

            client.Put upload.Config.StaticAssetsBucket upload.Prefix upload.Media logger

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

    // NOTE[gastove|2022-08-20] Yeah, I have *no idea* what it could mean to
    // sync media anymore. We... will see.
    //
    // let synchronizeMedia
    //     (dbxClient: Dropbox.DbxClient)
    //     client
    //     (mediaDir: string)
    //     (logger: ILogger)
    //     (cfg: Configuration)
    //     (uploadMaker: Media.UploadMaker)
    //     =
    //     task {
    //         let! fileList = Dropbox.Files.listFilesAsync mediaDir dbxClient

    //         let fileNames =
    //             fileList.Entries
    //             |> Seq.filter (fun entry -> entry.IsFile)
    //             |> Seq.map (fun entry -> entry.Name)

    //         let! _ =
    //             fileNames
    //             |> Seq.map (fun n ->
    //                 task {
    //                     use! download = Dropbox.Files.loadFileAsync mediaDir n dbxClient

    //                     let! stream =
    //                         download.GetContentAsStreamAsync()
    //                         |> Async.AwaitTask

    //                     match uploadMaker n stream with
    //                     | Ok (upload) ->
    //                         let! url = uploadAsync client upload logger
    //                         logger.Information("Uploaded to {url}", url)
    //                     | Error (e) -> logger.Error($"Upload of {n} failed", e)
    //                 })
    //             |> Seq.map Async.AwaitTask
    //             |> Async.Parallel

    //         match createAndUploadIndex client fileNames cfg logger with
    //         | Ok (indexUpload) ->
    //             let! indexResult = indexUpload
    //             logger.Information("Uploaded index to {indexResult}", indexResult)
    //         | Error (e) -> logger.Error("Failed to create or upload index", e)

    //         return ()
    //     }

    // let syncGifs (dbxClient: Dropbox.DbxClient) gcpClient (logger: ILogger) (cfg: Configuration) =
    //     let uploadMaker =
    //         Media.buildUploadMaker cfg Constants.StaticAssetsGifsPrefix

    //     synchronizeMedia dbxClient gcpClient Constants.GifsDir logger cfg uploadMaker

    // // let syncImgs (dbxClient: Dropbox.DbxClient) (logger: ILogger) (cfg: Configuration) =
    // //     let uploadMaker =
    // //         Media.buildUploadMaker cfg Constants.StaticAssetsImgagesPrefix

    // //     synchronizeMedia dbxClient Constants.GifsDir logger uploadMaker

    // let runSync cfg =
    //     let logger = Log.Logger

    //     let maybeDbxClient = Dropbox.Auth.createDbxClient ()
    //     let maybeGcpClient = Cookbook.GCP.Storage.getClient ()

    //     match maybeDbxClient, maybeGcpClient with
    //     | Some dbxClient, Ok gcpClient ->
    //         logger.Debug("Clients loaded")

    //         let sleepMilis =
    //             Constants.StaticAssetsResyncIntervalSeconds * 1000

    //         task {
    //             while true do
    //                 logger.Debug("Syncing static assets...")

    //                 do! syncGifs dbxClient gcpClient logger cfg
    //                 // do! syncImgs dbxClient logger cfg

    //                 logger.Debug($"Waiting {sleepMilis} millis to sync again...")

    //                 do! Async.Sleep sleepMilis
    //         }
    //         |> Ok
    //     | None, _ ->
    //         "Failed to load Dropbox API key"
    //         |> DropboxClientError
    //         |> Error
    //     | _, Error (gcpError) -> gcpError.Message |> GCPClientError |> Error

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
