namespace Keep.GCP

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

    let put bucket (file : IMedia) =
        let acl = Some(PredefinedObjectAcl.PublicRead) |> Option.toNullable
        let options = new UploadObjectOptions(PredefinedAcl = acl )
        let client = getClient()

        async {
            let! obj =
                client.UploadObjectAsync(
                    bucket=bucket,
                    objectName=file.FileName(),
                    contentType=file.MediaType(),
                    source=file.Stream(),
                    options=options
                ) |> Async.AwaitTask

            return obj.MediaLink
        }


    [<EntryPoint>]
    let main argv =
        let filePath = "/home/gastove/Dropbox/gifs/attack-penguin.gif"
        use fileStream = System.IO.File.OpenRead(filePath)

        let gif = MediaTypes.Gif("LOCAL-TEST-GIF.gif", fileStream)

        let bucket = "gs://static.gastove.com/gifs/"

        let mediaLink = put bucket gif |> Async.RunSynchronously

        printfn "Got back: %s" (mediaLink.ToString())
        0
