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

        let (|Html|_|) fileName =
            let htmlRe =
                System.Text.RegularExpressions.Regex(".*\.html$")

            if htmlRe.IsMatch(fileName) then
                Some Html
            else
                None

        match fileName with
        | Png -> Ok "image/png"
        | Jpeg -> Ok "image/jpeg"
        | Gif -> Ok "image/gif"
        | Html -> Ok "text/html"
        | _ -> Error $"couldn't compute media type for {fileName}"

module Storage =

    open Google.Cloud.Storage.V1
    open Google.Apis.Upload

    open Serilog

    let getClient () = StorageClient.Create()

    let put bucket prefix (file: Media) (logger: ILogger) =
        let acl =
            Some(PredefinedObjectAcl.PublicRead)
            |> Option.toNullable

        let progress =
            System.Progress<IUploadProgress>(fun p -> logger.Information("Uploading {FileName}; wrote {BytesSent}, status: {Status}", file.FileName, p.BytesSent, p.Status))

        let options = UploadObjectOptions(PredefinedAcl = acl)
        let client = getClient ()

        let objectName = $"{prefix}/{file.FileName}"

        async {
            let! obj =
                client.UploadObjectAsync(
                    bucket = bucket,
                    objectName = objectName,
                    contentType = file.MediaType,
                    source = file.Body,
                    options = options,
                    progress = progress
                )
                |> Async.AwaitTask

            return obj.MediaLink
        }
