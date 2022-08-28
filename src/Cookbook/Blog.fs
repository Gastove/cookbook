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
                |> List.choose (fun fileName ->
                    match System.IO.Path.GetExtension fileName with
                    | ".html" ->
                        task {
                            let! content = client.Get bucket fileName

                            return Xml.parsePost content
                        } |> Some
                    | _ -> None)
                |> List.map Async.AwaitTask
                |> Async.Parallel
        }

    let projectPublicationDate (bp: BlogPost) = bp.Meta.PublicationDate

    let hasTag (tag: string) (bp: BlogPost) =
        bp.Meta.Tags.ToLower().Contains(tag.ToLower())
