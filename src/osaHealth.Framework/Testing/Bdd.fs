module osaHealth.Framework.Testing.Bdd

open System.Runtime.CompilerServices
open System.Runtime.InteropServices

type Phase =
    | Given
    | When
    | Then
    | And

type Step<'ctx> =
    { Phase: Phase
      Description: string
      Action: 'ctx -> 'ctx }

type Scenario<'ctx> =
    { Name: string
      Initial: 'ctx
      Steps: Step<'ctx> list }

module Scenario =

    type Factory =
        static member create
            (initial: 'ctx, [<CallerMemberName; Optional; DefaultParameterValue("")>] name: string)
            : Scenario<'ctx> =
            { Name = name
              Initial = initial
              Steps = [] }

    let private appendStep
        (phase: Phase)
        (description: string)
        (action: 'ctx -> 'ctx)
        (scenario: Scenario<'ctx>)
        : Scenario<'ctx> =
        { scenario with
            Steps =
                scenario.Steps
                @ [ { Phase = phase
                      Description = description
                      Action = action } ] }

    let GIVEN (description: string) (action: 'ctx -> 'ctx) (scenario: Scenario<'ctx>) : Scenario<'ctx> =
        appendStep Given description action scenario

    let WHEN (description: string) (action: 'ctx -> 'ctx) (scenario: Scenario<'ctx>) : Scenario<'ctx> =
        appendStep When description action scenario

    let THEN (description: string) (action: 'ctx -> 'ctx) (scenario: Scenario<'ctx>) : Scenario<'ctx> =
        appendStep Then description action scenario

    let AND (description: string) (action: 'ctx -> 'ctx) (scenario: Scenario<'ctx>) : Scenario<'ctx> =
        appendStep And description action scenario

    let run (scenario: Scenario<'ctx>): unit =
        printfn "\n\n-------------------------------------"
        printfn $"Scenario: %s{scenario.Name}"

        scenario.Steps
        |> List.fold (fun acc step ->
            printfn $"  %s{step.Phase.ToString()}: %s{step.Description}"
            step.Action acc) scenario.Initial
        |> ignore

        printfn "-------------------------------------"
