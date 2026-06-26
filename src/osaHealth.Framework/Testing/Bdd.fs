module osaHealth.Framework.Testing.Bdd

open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Threading.Tasks

type StepKeyword =
    | Given
    | When
    | Then
    | And

type Step<'TContext, 'TResult> =
    { Keyword: StepKeyword
      Description: string
      Action: 'TContext -> 'TResult }

type Scenario<'TContext, 'TResult> =
    { Name: string
      Initial: 'TContext
      Steps: Step<'TContext, 'TResult> list }

module Scenario =
    type Factory =
        static member create
            (initial: 'TContext, [<CallerMemberName; Optional; DefaultParameterValue("")>] name: string)
            : Scenario<'TContext, 'TResult> =
            { Name = name
              Initial = initial
              Steps = [] }

    let private appendStep
        (keyword: StepKeyword)
        (description: string)
        (action: 'TContext -> 'TResult)
        (scenario: Scenario<'TContext, 'TResult>)
        : Scenario<'TContext, 'TResult> =
        { scenario with
            Steps =
                scenario.Steps
                @ [ { Keyword = keyword
                      Description = description
                      Action = action } ] }

    let private printStep (keyword: StepKeyword) (description: string) : unit =
        printfn $"  %s{keyword.ToString()}: %s{description}"

    let GIVEN
        (description: string)
        (action: 'TContext -> 'TResult)
        (scenario: Scenario<'TContext, 'TResult>)
        : Scenario<'TContext, 'TResult> =
        appendStep Given description action scenario

    let WHEN
        (description: string)
        (action: 'TContext -> 'TResult)
        (scenario: Scenario<'TContext, 'TResult>)
        : Scenario<'TContext, 'TResult> =
        appendStep When description action scenario

    let THEN
        (description: string)
        (action: 'TContext -> 'TResult)
        (scenario: Scenario<'TContext, 'TResult>)
        : Scenario<'TContext, 'TResult> =
        appendStep Then description action scenario

    let AND
        (description: string)
        (action: 'TContext -> 'TResult)
        (scenario: Scenario<'TContext, 'TResult>)
        : Scenario<'TContext, 'TResult> =
        appendStep And description action scenario

    let run (scenario: Scenario<'TContext, 'TContext>) : unit =
        printfn "\n\n-------------------------------------"
        printfn $"Scenario: %s{scenario.Name}"

        scenario.Steps
        |> List.fold
            (fun context step ->
                printStep step.Keyword step.Description
                step.Action context)
            scenario.Initial
        |> ignore

        printfn "-------------------------------------"

    let runAsync (scenario: Scenario<'TContext, Task<'TContext>>) : Task<unit> =
        task {
            printfn "\n\n-------------------------------------"
            printfn $"Scenario: %s{scenario.Name}"

            let mutable context = scenario.Initial

            for step in scenario.Steps do
                printStep step.Keyword step.Description
                let! nextContext = step.Action context
                context <- nextContext

            printfn "-------------------------------------"
        }
