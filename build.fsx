
#r @"packages/FAKE/tools/FakeLib.dll"
open Fake

Target "Default" (fun _ ->
    trace "Hello World from FAKE"
)

RunTargetOrDefault "Default"
