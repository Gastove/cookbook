namespace Cookbook

module Static =

    open Config
    open GCP.MediaTypes

    module Gifs =

        open Serilog

        let makeGif name stream =
            new Gif(name, stream)

        let uploadAsync (cfg : Configuration) (prefix : string) (file : IMedia) (logger : ILogger) =
            logger.Information("Uploading file {name} to {bucket}/{prefix}", file.FileName(), cfg.StaticAssetsBucket, prefix)
            GCP.Storage.put cfg.StaticAssetsBucket prefix file

        let synchronizeGifs (dbxClient : Dropbox.DbxClient) (cfg : Configuration) (logger : ILogger)=
            let dbxGifsDir = Constants.GifsDir
            let task = async {
                let! fileList = Dropbox.Files.listFilesAsync dbxGifsDir dbxClient

                let fileNames =
                    fileList.Entries
                    |> Seq.filter (fun entry -> entry.IsFile)
                    |> Seq.map (fun entry -> entry.Name)


                fileNames
                |> Seq.map (fun n -> async {
                                    logger.Information("Syncing gif {n}", n)
                                    use! download = Dropbox.Files.loadFileAsync dbxGifsDir n dbxClient
                                    let! stream = download.GetContentAsStreamAsync() |> Async.AwaitTask

                                    let gif = makeGif n stream

                                    let! url = uploadAsync cfg Constants.StaticAssetsGifsPrefix gif logger
                                    logger.Information("Uploaded to {url}", url)
                                })
                |> Async.Parallel
                |> Async.RunSynchronously
                |> ignore
            }

            task

        let runSync cfg (logger : ILogger) =
            match Dropbox.Auth.createDbxClient() with
            | Some dbxClient ->
                logger.Debug("Client loaded")
                let sleepSeconds = 60
                let sleepMilis = sleepSeconds * 1000
                async {
                    while true do
                        logger.Debug("Syncing gifs...")
                        synchronizeGifs dbxClient cfg logger |> Async.RunSynchronously
                        Async.Sleep sleepMilis |> Async.RunSynchronously
                }
            | None -> async {()}
