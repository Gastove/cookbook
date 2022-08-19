namespace Cookbook

module HomePage =

    open System

    open Markdig

    let markdownParser =
        MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build()

    let tryLoadContent folder slug (client: Dropbox.DbxClient) =
        task {

            let! exists = Dropbox.Files.fileExists folder slug client

            if exists then

                use! postResponse = Dropbox.Files.loadFileAsync folder slug client

                let! stream = postResponse.GetContentAsStreamAsync()

                use reader = new IO.StreamReader(stream)

                let! contents = reader.ReadToEndAsync()

                return Markdown.ToHtml(contents, markdownParser) |> Some
            else
                return None
        }
