namespace Cookbook

module HomePage =

    open Markdig

    let markdownParser =
        MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build()

    let tryLoadContent folder slug (client: IStorageClient) =
        task {

            let! exists = client.TryExists folder slug

            // Pull this apart with a match instead of using Result.map. The
            // match means we can return from each branch, which saves us from
            // having to sort out a Result<Task<string>, exn>
            match exists with
            | Ok (_) ->

                let! contents = client.Get folder slug

                return Markdown.ToHtml(contents, markdownParser) |> Ok
            | Error (exn) -> return exn |> Error
        }
