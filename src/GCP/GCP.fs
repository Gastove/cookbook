namespace Cookbook.GCP



type Media =
    {MediaType: string
     FileName: string
     Body: System.IO.Stream}


module Storage =

    open Google.Cloud.Storage.V1

    let getClient() =
        StorageClient.Create()

    let put bucket prefix (file : Media) =
        let acl = Some(PredefinedObjectAcl.PublicRead) |> Option.toNullable
        let options = UploadObjectOptions(PredefinedAcl = acl )
        let client = getClient()

        let objectName = [| prefix; file.FileName |] |> String.concat "/"

        async {
            let! obj =
                client.UploadObjectAsync(
                    bucket=bucket,
                    objectName=objectName,
                    contentType=file.MediaType,
                    source=file.Body,
                    options=options
                ) |> Async.AwaitTask

            return obj.MediaLink
        }
