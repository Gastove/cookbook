namespace Cookbook.GCP

type Media =
    { MediaType: string
      FileName: string
      Body: System.IO.Stream }

    static member Create (fileName: string) (body: System.IO.Stream) =
        Media.ComputeMediaType fileName
        |> Result.map (fun mediaType ->
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

    open System
    open System.Threading
    open System.Threading.Tasks

    open Google.Cloud.Storage
    open Google.Apis.Upload
    open Google.Apis.Download

    open Serilog

    type IStorageClient =
        abstract Get : string -> string -> ILogger -> Task<string>
        abstract GetStream : string -> string -> ILogger -> Task<IO.Stream>
        abstract Put : string -> string -> Media -> ILogger -> Task<string>
        abstract List : string -> string -> ILogger -> Task<string list>
        abstract TryExists : string -> string -> Task<Result<unit, exn>>

    type GcsClient =
        { Client: V1.StorageClient}


        static member Create() = { Client = V1.StorageClient.Create() }

        member __.DownloadProgress fileName (logger: ILogger) =
            System.Progress<IDownloadProgress> (fun p ->
                logger.Information(
                    "Downloading {FileName}; wrote {BytesDownloaded}, status: {Status}",
                    fileName,
                    p.BytesDownloaded,
                    p.Status
                ))

        member __.UploadProgress file (logger: ILogger) =
                System.Progress<IUploadProgress> (fun p ->
                    logger.Information(
                        "Uploading {FileName}; wrote {BytesSent}, status: {Status}",
                        file.FileName,
                        p.BytesSent,
                        p.Status
                    ))

        member this.Put bucket prefix (file: Media) (logger: ILogger) =
            let acl =
                Some(V1.PredefinedObjectAcl.PublicRead)
                |> Option.toNullable

            let options =
                V1.UploadObjectOptions(PredefinedAcl = acl)

            let objectName = $"{prefix}/{file.FileName}"

            task {
                let! obj =
                    this.Client.UploadObjectAsync(
                        bucket = bucket,
                        objectName = objectName,
                        contentType = file.MediaType,
                        source = file.Body,
                        options = options,
                        progress = (this.UploadProgress file logger)
                    )

                return obj.MediaLink
            }

        member this.Get (bucket: string) (path: string) (logger: ILogger) : Task<string> =
            task {
                let! stream = this.GetStream bucket path logger
                use reader = new IO.StreamReader(stream)
                let! content = reader.ReadToEndAsync()
                logger.Information("Downloaded string of length {Length}", content.Length)
                return content
            }

        member this.GetStream (bucket: string) (path: string) (logger: ILogger) : Task<IO.Stream> =
            task {
                let stream =
                    new IO.BufferedStream(new IO.MemoryStream()) :> IO.Stream

                use tokenSource = new CancellationTokenSource()
                let token = tokenSource.Token

                let! _meta =
                    this.Client.DownloadObjectAsync(
                        bucket,
                        path,
                        stream,
                        null,
                        token,
                        (this.DownloadProgress $"{bucket}/{path}" logger)
                    )

                stream.Seek(0, IO.SeekOrigin.Begin) |> ignore
                return stream
            }

        member this.List (bucket: string) (prefix: string) (logger: ILogger) : Task<string list> =
            task {
                let contents =
                    this.Client.ListObjectsAsync(bucket, prefix)

                use tokenSource = new CancellationTokenSource()
                let token = tokenSource.Token
                let contentsEnumerator = contents.GetAsyncEnumerator(token)

                let mutable results = List.empty
                // This is a bit funky, but, you can't let!-assign to a
                // mutable variable, and we must call MoveNextAsync to get
                // rolling or Current will be null. So.
                let mutable keepGoing = true
                let! shouldStart = contentsEnumerator.MoveNextAsync()
                keepGoing <- shouldStart

                while keepGoing do
                    let got = contentsEnumerator.Current
                    logger.Information("Successfully listing from {Bucket}: {Name}", bucket, got.Name)
                    results <- got.Name :: results

                    let! shouldKeepGoing = contentsEnumerator.MoveNextAsync()
                    keepGoing <- shouldKeepGoing

                return results
            }

        member this.TryExists bucket path : Task<Result<unit, exn>> =
            task {
                try
                    let! _ = this.Client.GetObjectAsync(bucket, path)
                    return () |> Ok
                with
                | exn -> return exn |> Error
            }
