namespace Cookbook.GCP

module MediaTypes =

    type IMedia =
        abstract member MediaType : unit -> string
        abstract member FileName : unit -> string
        abstract member Stream : unit -> System.IO.Stream

    type Gif(name: string, stream: System.IO.Stream) =
        let mediaType = "media/gif"

        interface IMedia with
            member this.MediaType() =
                mediaType

            member this.FileName() =
                name

            member this.Stream() =
                stream


module Storage =

    open Google.Cloud.Storage.V1

    open MediaTypes

    let getClient() =
        StorageClient.Create()

    let put bucket prefix (file : IMedia) =
        let acl = Some(PredefinedObjectAcl.PublicRead) |> Option.toNullable
        let options = new UploadObjectOptions(PredefinedAcl = acl )
        let client = getClient()

        let objectName = [| prefix; file.FileName() |] |> String.concat "/"

        async {
            let! obj =
                client.UploadObjectAsync(
                    bucket=bucket,
                    objectName=objectName,
                    contentType=file.MediaType(),
                    source=file.Stream(),
                    options=options
                ) |> Async.AwaitTask

            return obj.MediaLink
        }
