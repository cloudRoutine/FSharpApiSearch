﻿module FSharpApiSearch.Database.Program

open FSharpApiSearch
open CommandLine
open Nessos.FsPickler
open System.IO
open System.Configuration

type Args = {
  AssemblyResolver : AssemblyLoader.AssemblyResolver
  References: string list
  Help: bool
}

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Args =
  let defaultArg = {
    AssemblyResolver =
      {
        FSharpCore = ConfigurationManager.AppSettings.Get("FSharpCore")
        Framework = ConfigurationManager.AppSettings.Get("Framework")
        Directories = []
      }
    References = []
    Help = false
  }

  let rec parse arg = function
    | KeyValue "--FSharpCore" path :: rest -> parse { arg with AssemblyResolver = { arg.AssemblyResolver with FSharpCore = path } } rest
    | KeyValue "--Framework" path :: rest -> parse { arg with AssemblyResolver = { arg.AssemblyResolver with Framework = path } } rest
    | KeyValue "--lib" path :: rest -> parse { arg with AssemblyResolver = { arg.AssemblyResolver with Directories = path :: arg.AssemblyResolver.Directories } } rest
    | ("--help" | "-h") :: rest -> parse { arg with Help = true } rest
    | path :: rest -> parse { arg with References = path :: arg.References } rest
    | [] -> arg

let helpMessage = """usage: FSharpApiSearch.Database.exe <options> <assemblies>

assemblies: Specifies the assembly name or the assembly path.
            By default, 'mscorlib', 'System', 'System.Core', 'System.Xml', 'System.Configuration' and 'FSharp.Core' are specified.
options:
  --lib:<folder-name>
      Specifies a directory to be searched for assemblies that are referenced.
  --FSharpCore:<folder-name>
      Specifies the FSharp.Core path.
      If omitted, it will use the value in 'FSharpApiSearch.Database.exe.config'.
  --Framework:<folder-name>
      Specifies a directory to be searched for .Net Framework assemblies that are references.
      If omitted, it will use the value in 'FSharpApiSearch.Database.exe.config'.
  --help, -h
      Print this message."""

let printAssemblies (assemblyResolver: AssemblyLoader.AssemblyResolver) assemblies =
  printfn "Create the database of the following assemblies."
  assemblies
  |> Seq.choose assemblyResolver.Resolve
  |> Seq.iter (fun path -> printfn "%s" path)

[<EntryPoint>]
let main argv = 
  let args = Args.parse Args.defaultArg (List.ofArray argv)
  match args with
  | { Help = true } ->
    printfn "%s" helpMessage
    0
  | _ ->
    try
      let assemblies = Seq.concat [ FSharpApiSearchClient.DefaultReferences; List.rev args.References ]
      printAssemblies args.AssemblyResolver assemblies
      let dictionaries =
        AssemblyLoader.load args.AssemblyResolver assemblies
        |> ApiLoader.load
      ApiLoader.save ApiLoader.databaseName dictionaries
      0
    with ex ->
      printfn "%A" ex
      1