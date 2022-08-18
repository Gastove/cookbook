namespace Cookbook

module Blog =

    let loadPost folder slug (client: Dropbox.DbxClient) =
        task {
            use! postResponse = Dropbox.Files.loadFileAsync folder slug client

            let! stream = postResponse.GetContentAsStreamAsync()

            return Xml.readPostAndParse stream
        }

    let loadAllPosts folder (client: Dropbox.DbxClient) =
        task {
            let! files = Dropbox.Files.listFilesAsync folder client

            let fileNames =
                files.Entries
                |> Seq.filter (fun (entry: Dropbox.Api.Files.Metadata) -> entry.IsFile)
                |> Seq.map (fun entry -> entry.Name)
                |> Seq.toList

            return!
                fileNames
                |> List.map (fun n ->
                    task {
                        let! file = Dropbox.Files.loadFileAsync folder n client
                        let! stream = file.GetContentAsStreamAsync()

                        return Xml.readPostAndParse stream
                    })
                |> List.map Async.AwaitTask
                |> Async.Parallel
        }
