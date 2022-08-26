namespace Cookbook

open System
open System.Threading.Tasks

open Serilog

type IStorageClient =
    abstract Get : string -> string -> ILogger -> Task<string>
    abstract GetStream : string -> string -> ILogger -> Task<IO.Stream>
    abstract Put : string -> string -> Cookbook.GCP.Media -> ILogger -> Task<string>
    abstract List : string -> string -> ILogger -> Task<string list>
    abstract TryExists : string -> string -> Task<Result<unit, exn>>


module Storage =

    type GcsStorageClient() =
        let Client = Cookbook.GCP.Storage.GcsClient.Create()

        interface IStorageClient with
            member __.Put bucket prefix (file: Cookbook.GCP.Media) (logger: ILogger) = Client.Put bucket prefix file logger

            member __.Get (bucket: string) (path: string) (logger: ILogger) : Task<string> =
                Client.Get bucket path logger

            member __.GetStream (bucket: string) (path: string) (logger: ILogger) : Task<IO.Stream> =
                Client.GetStream bucket path logger

            member __.List (bucket: string) (prefix: string) (logger: ILogger) : Task<string list> =
                Client.List bucket prefix logger

            member __.TryExists bucket path : Task<Result<unit, exn>> = Client.TryExists bucket path
