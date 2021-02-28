namespace Cookbook

module Static =

    open Serilog

    module Media =

        open Cookbook.GCP

        type Upload =
            { Media: Media
              Config: Configuration
              Prefix: string }

        type UploadMaker = string -> System.IO.Stream -> Result<Upload, string>
        type UploadMakerMaker = Configuration -> string -> string -> UploadMaker

        let buildUploadMaker cfg prefix : UploadMaker =
            fun name stream ->
                Media.Create name stream
                |> Result.map
                    (fun media ->
                        { Media = media
                          Config = cfg
                          Prefix = prefix })

    module Sync =

        let uploadAsync (upload: Media.Upload) (logger: ILogger) =
            logger.Information(
                "Uploading file {name} to {bucket}/{prefix}",
                upload.Media.FileName,
                upload.Config.StaticAssetsBucket,
                upload.Prefix
            )

            GCP.Storage.put upload.Config.StaticAssetsBucket upload.Prefix upload.Media

        let synchronizeMedia
            (dbxClient: Dropbox.DbxClient)
            (mediaDir: string)
            (logger: ILogger)
            (uploadMaker: Media.UploadMaker)
            =
            async {
                let! fileList = Dropbox.Files.listFilesAsync mediaDir dbxClient

                let fileNames =
                    fileList.Entries
                    |> Seq.filter (fun entry -> entry.IsFile)
                    |> Seq.map (fun entry -> entry.Name)

                let! _ =
                    fileNames
                    |> Seq.map
                        (fun n ->
                            async {
                                use! download = Dropbox.Files.loadFileAsync mediaDir n dbxClient

                                let! stream =
                                    download.GetContentAsStreamAsync()
                                    |> Async.AwaitTask

                                match uploadMaker n stream with
                                | Ok (upload) ->
                                    let! url = uploadAsync upload logger
                                    logger.Information("Uploaded to {url}", url)
                                | Error (e) -> logger.Error($"Upload of {n} failed", e)
                            })
                    |> Async.Parallel

                return ()
            }

        let syncGifs (dbxClient: Dropbox.DbxClient) (logger: ILogger) (cfg: Configuration) =
            let uploadMaker =
                Media.buildUploadMaker cfg Constants.StaticAssetsGifsPrefix

            synchronizeMedia dbxClient Constants.GifsDir logger uploadMaker

        let syncImgs (dbxClient: Dropbox.DbxClient) (logger: ILogger) (cfg: Configuration) =
            let uploadMaker =
                Media.buildUploadMaker cfg Constants.StaticAssetsImgagesPrefix

            synchronizeMedia dbxClient Constants.GifsDir logger uploadMaker

        let runSync cfg =
            let logger = Log.Logger
            match Dropbox.Auth.createDbxClient () with
            | Some dbxClient ->
                logger.Debug("Client loaded")

                let sleepMilis =
                    Constants.StaticAssetsResyncIntervalSeconds * 1000

                async {
                    while true do
                        logger.Debug("Syncing static assets...")

                        do! syncGifs dbxClient logger cfg
                        do! syncImgs dbxClient logger cfg

                        logger.Debug($"Waiting {sleepMilis} millis to sync again...")

                        do! Async.Sleep sleepMilis
                }
            | None -> async { () }
