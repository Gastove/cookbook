namespace Cookbook.Common.Tests
open Expecto

module Run = 
    [<EntryPoint>]
    let main argv =
        Tests.runTestsInAssembly defaultConfig argv
