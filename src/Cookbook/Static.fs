namespace Cookbook

module Static =

    open Serilog

    module Media =

        open GCP

        type Upload =
            {Media : Media
             Config : Configuration
             Logger : ILogger
             Prefix : string}

        type UploadMaker = string -> System.IO.Stream -> Upload
        type UploadMakerMaker = Configuration -> ILogger -> string -> string -> UploadMaker

        let buildUploadMaker cfg logger prefix mediaType: UploadMaker =
            fun name stream ->
                let media =
                    {MediaType = mediaType
                     FileName = name
                     Body = stream}

                {Media=media
                 Config = cfg
                 Logger = logger
                 Prefix = prefix}

    module Gifs =

        let makeGif name stream =
            {GCP.Media.MediaType="media/gif"
             GCP.Media.FileName=name
             GCP.Media.Body = stream}

        let uploadAsync (upload: Media.Upload) =
            upload.Logger.Information
                ("Uploading file {name} to {bucket}/{prefix}", upload.Media.FileName,
                 upload.Config.StaticAssetsBucket, upload.Prefix)
            GCP.Storage.put upload.Config.StaticAssetsBucket upload.Prefix upload.Media

        let synchronizeMedia (dbxClient : Dropbox.DbxClient) (mediaDir : string) (uploadMaker : Media.UploadMaker) =

            let task =
                async {
                    let! fileList = Dropbox.Files.listFilesAsync mediaDir dbxClient

                    let fileNames =
                        fileList.Entries
                        |> Seq.filter (fun entry -> entry.IsFile)
                        |> Seq.map (fun entry -> entry.Name)

                    fileNames
                    |> Seq.map (fun n ->
                           async {
                               use! download = Dropbox.Files.loadFileAsync mediaDir n dbxClient

                               let! stream = download.GetContentAsStreamAsync()
                                             |> Async.AwaitTask

                               let upload = uploadMaker n stream

                               let! url = uploadAsync upload

                               upload.Logger.Information("Uploaded to {url}", url)
                           })
                    |> Async.Parallel
                    |> Async.RunSynchronously
                    |> ignore
                }
            task

        let syncGifs (dbxClient : Dropbox.DbxClient) (logger : ILogger) (cfg : Configuration)=
            let uploader = Media.buildUploadMaker cfg logger Constants.StaticAssetsGifsPrefix "media/gif"
            synchronizeMedia dbxClient Constants.GifsDir uploader

        let runSync cfg (logger : ILogger) =
            match Dropbox.Auth.createDbxClient() with
            | Some dbxClient ->
                logger.Debug("Client loaded")
                let sleepSeconds = 60
                let sleepMilis = sleepSeconds * 1000
                async {
                    while true do
                    logger.Debug("Syncing gifs...")
                    syncGifs dbxClient logger cfg
                    |> Async.RunSynchronously
                    Async.Sleep sleepMilis |> Async.RunSynchronously
                }
            | None -> async { () }
