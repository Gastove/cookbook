namespace Cookbook

open System
open System.Threading.Tasks

open Serilog

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


type IStorageClient =
    abstract Get : string -> string -> Task<string>
    abstract GetStream : string -> string -> Task<IO.Stream>
    abstract Put : string -> string -> Media -> Task<string>
    abstract List : string -> string -> Task<string list>
    abstract TryExists : string -> string -> Task<Result<unit, exn>>

module Storage =

    open System.Threading

    open Google.Cloud.Storage
    open Google.Apis.Upload
    open Google.Apis.Download

    open Microsoft.Extensions.Caching.Memory

    type GcsStorageClient(logger: ILogger) =
        let Client = V1.StorageClient.Create()

        member __.DownloadProgress fileName =
            System.Progress<IDownloadProgress> (fun p ->
                logger.Information(
                    "Downloading {FileName}; wrote {BytesDownloaded}, status: {Status}",
                    fileName,
                    p.BytesDownloaded,
                    p.Status
                ))

        member __.UploadProgress file =
            System.Progress<IUploadProgress> (fun p ->
                logger.Information(
                    "Uploading {FileName}; wrote {BytesSent}, status: {Status}",
                    file.FileName,
                    p.BytesSent,
                    p.Status
                ))

        interface IStorageClient with
            member this.Put bucket prefix (file: Media) =
                let acl =
                    Some(V1.PredefinedObjectAcl.PublicRead)
                    |> Option.toNullable

                let options =
                    V1.UploadObjectOptions(PredefinedAcl = acl)

                let objectName = $"{prefix}/{file.FileName}"

                task {
                    let! obj =
                        Client.UploadObjectAsync(
                            bucket = bucket,
                            objectName = objectName,
                            contentType = file.MediaType,
                            source = file.Body,
                            options = options,
                            progress = (this.UploadProgress file)
                        )

                    return obj.MediaLink
                }

            member this.Get (bucket: string) (path: string) : Task<string> =
                task {
                    let! stream = (this :> IStorageClient).GetStream bucket path
                    use stream = stream
                    use reader = new IO.StreamReader(stream)
                    let! content = reader.ReadToEndAsync()
                    return content
                }

            member this.GetStream (bucket: string) (path: string) : Task<IO.Stream> =
                task {
                    let stream =
                        new IO.BufferedStream(new IO.MemoryStream()) :> IO.Stream

                    use tokenSource = new CancellationTokenSource()
                    let token = tokenSource.Token

                    let! _meta =
                        Client.DownloadObjectAsync(
                            bucket,
                            path,
                            stream,
                            null,
                            token,
                            (this.DownloadProgress $"{bucket}/{path}")
                        )

                    stream.Seek(0, IO.SeekOrigin.Begin) |> ignore
                    return stream
                }

            member __.List (bucket: string) (prefix: string) : Task<string list> =
                task {
                    let contents = Client.ListObjectsAsync(bucket, prefix)

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

            member __.TryExists bucket path : Task<Result<unit, exn>> =
                task {
                    try
                        let! _ = Client.GetObjectAsync(bucket, path)
                        return () |> Ok
                    with
                    | exn -> return exn |> Error
                }

    exception FileNotExists of string

    type FileSystemStorageClient(logger: ILogger) =
        interface IStorageClient with
            member _.Get (folder: string) (path: string) : Task<string> =
                logger.Information("Trying to load {Path}", $"{folder}/{path}")

                task {
                    use filestream = IO.File.OpenText($"{folder}/{path}")
                    return! filestream.ReadToEndAsync()
                }

            member _.GetStream (folder: string) (path: string) : System.Threading.Tasks.Task<System.IO.Stream> =
                task { return IO.File.Open($"{folder}/{path}", IO.FileMode.Open) }

            member _.List (folder: string) (path: string) : Task<string list> =
                task {
                    return
                        $"{folder}/{path}"
                        |> IO.Directory.EnumerateFiles
                        |> Seq.map (IO.Path.GetFileName >> (sprintf "%s/%s" path))
                        |> Seq.toList
                }

            member _.Put (folder: string) (path: string) (media: Media) : System.Threading.Tasks.Task<string> =
                task {
                    let filePath = $"{folder}/{path}/{media.FileName}"
                    let handle = IO.File.OpenWrite(filePath)
                    do! media.Body.CopyToAsync(handle)
                    return filePath
                }

            member _.TryExists (folder: string) (path: string) : Task<Result<unit, exn>> =
                Log.Information("Trying to load {Path}", $"{folder}/{path}")

                task {
                    let path = $"{folder}/{path}"

                    if path |> IO.File.Exists then
                        return () |> Ok
                    else
                        return path |> FileNotExists |> Error
                }

    type CachingGcsStorageClient(memoryCache: IMemoryCache, logger: ILogger) =
        let GCSClient =
            GcsStorageClient(logger) :> IStorageClient

        interface IStorageClient with
            member _.Get (bucket: string) (path: string) : Task<string> =
                memoryCache.GetOrCreateAsync(
                    $"{bucket}/{path}",
                    (fun entry ->
                        logger.Information("Failed to find {Path} in the cache, fetching", $"{bucket}/{path}")
                        entry.AbsoluteExpirationRelativeToNow <- TimeSpan.FromMinutes(15)
                        GCSClient.Get bucket path)
                )

            member _.GetStream (bucket: string) (path: string) : Task<IO.Stream> = GCSClient.GetStream bucket path

            member _.List (bucket: string) (prefix: string) : Task<string list> = GCSClient.List bucket prefix

            member _.Put (bucket: string) (prefix: string) (media: Media) : Task<string> =
                GCSClient.Put bucket prefix media

            member _.TryExists (bucket: string) (path: string) : Task<Result<unit, exn>> =
                GCSClient.TryExists bucket path
