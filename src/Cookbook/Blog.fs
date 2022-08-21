namespace Cookbook

module Blog =

    let loadPost folder slug (client: GCP.Storage.IStorageClient) (logger: Serilog.ILogger) =
        task {
            let! stream = client.GetStream folder slug logger

            return Xml.readPostAndParse stream
        }

    let loadAllPosts bucket subPath (client: GCP.Storage.IStorageClient) (logger: Serilog.ILogger) =
        task {
            let! files = client.List bucket subPath logger

            return!
                files
                |> List.map (fun fileName ->
                    task {
                        let! stream = client.GetStream bucket fileName logger

                        return Xml.readPostAndParse stream
                    })
                |> List.map Async.AwaitTask
                |> Async.Parallel
        }
