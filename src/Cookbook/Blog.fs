namespace Cookbook

module Blog =

    let loadPost folder slug (client : Dropbox.DbxClient) =
        async {
            use! postResponse =
                Dropbox.Files.loadFileAsync folder slug client

            let! stream =
                postResponse.GetContentAsStreamAsync()
                |> Async.AwaitTask

            return Xml.readPostAndParse stream
        } |> Async.RunSynchronously

    let loadAllPosts folder (client : Dropbox.DbxClient) : BlogPost array =
        let postNames = async {
            let! files = Dropbox.Files.listFilesAsync folder client

            let fileNames =
                files.Entries
                |> Seq.filter(fun (entry: Dropbox.Api.Files.Metadata) -> entry.IsFile)
                |> Seq.map(fun entry -> entry.Name)
                |> Seq.toList

            return fileNames
        }

        postNames
        |> Async.RunSynchronously
        |> List.map(fun n -> async {
            let! file = Dropbox.Files.loadFileAsync folder n client
            let! stream =
                file.GetContentAsStreamAsync()
                |> Async.AwaitTask
            return Xml.readPostAndParse stream
            })
        |> Async.Parallel
        |> Async.RunSynchronously
