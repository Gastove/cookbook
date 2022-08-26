namespace Cookbook

module Blog =

    let loadPost folder slug (client: IStorageClient) =
        task {
            let! stream = client.GetStream folder slug

            return Xml.readPostAndParse stream
        }

    let loadAllPosts bucket subPath (client: IStorageClient) =
        task {
            let! files = client.List bucket subPath

            return!
                files
                |> List.map (fun fileName ->
                    task {
                        let! stream = client.GetStream bucket fileName

                        return Xml.readPostAndParse stream
                    })
                |> List.map Async.AwaitTask
                |> Async.Parallel
        }

    let projectPublicationDate (bp: BlogPost) = bp.Meta.PublicationDate

    let hasTag (tag: string) (bp: BlogPost) =
        bp.Meta.Tags.ToLower().Contains(tag.ToLower())
