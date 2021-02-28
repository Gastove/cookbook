namespace Cookbook.GCP

type Media =
    { MediaType: string
      FileName: string
      Body: System.IO.Stream }

    static member Create (fileName: string) (body: System.IO.Stream) =
        Media.ComputeMediaType fileName
        |> Result.map
            (fun mediaType ->
                { MediaType = mediaType
                  FileName = fileName
                  Body = body })

    static member ComputeMediaType(fileName: string) =
        let (|Png|_|) (fileName: string) =
            let pngRe =
                System.Text.RegularExpressions.Regex(".*\.png$")

            if pngRe.IsMatch(fileName) then
                Some Png
            else
                None

        let (|Jpeg|_|) fileName =
            let jpegRe =
                System.Text.RegularExpressions.Regex(".*\.jpe?g$")

            if jpegRe.IsMatch(fileName) then
                Some Jpeg
            else
                None

        let (|Gif|_|) fileName =
            let gifRe =
                System.Text.RegularExpressions.Regex(".*\.gif$")

            if gifRe.IsMatch(fileName) then
                Some Gif
            else
                None

        match fileName with
        | Png -> Ok "image/png"
        | Jpeg -> Ok "image/jpeg"
        | Gif -> Ok "image/gif"
        | _ -> Error $"couldn't compute media type for {fileName}"

module Storage =

    open Google.Cloud.Storage.V1

    let getClient () = StorageClient.Create()

    let put bucket prefix (file: Media) =
        let acl =
            Some(PredefinedObjectAcl.PublicRead)
            |> Option.toNullable

        let options = UploadObjectOptions(PredefinedAcl = acl)
        let client = getClient ()

        let objectName =
            [| prefix; file.FileName |] |> String.concat "/"

        async {
            let! obj =
                client.UploadObjectAsync(
                    bucket = bucket,
                    objectName = objectName,
                    contentType = file.MediaType,
                    source = file.Body,
                    options = options
                )
                |> Async.AwaitTask

            return obj.MediaLink
        }
