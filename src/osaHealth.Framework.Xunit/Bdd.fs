namespace osaHealth.Framework.Testing.Bdd

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
      Steps: Step<'TContext, 'TResult> list
      Log: string -> unit }

module Scenario =
    type Factory =
        static member create
            (
                initial: 'TContext,
                log: string -> unit,
                [<CallerMemberName; Optional; DefaultParameterValue("")>] name: string
            ) : Scenario<'TContext, 'TResult> =
            { Name = name
              Initial = initial
              Steps = []
              Log = log }

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

    let private logStep (keyword: StepKeyword) (description: string) (log: string -> unit) : unit =
        log $"  %s{keyword.ToString()}: %s{description}"

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
        scenario.Log "\n\n-------------------------------------"
        scenario.Log $"Scenario: %s{scenario.Name}"

        scenario.Steps
        |> List.fold
            (fun context step ->
                logStep step.Keyword step.Description scenario.Log
                step.Action context)
            scenario.Initial
        |> ignore

        scenario.Log "-------------------------------------"

    let runAsync (scenario: Scenario<'TContext, Task<'TContext>>) : Task<unit> =
        task {
            scenario.Log "\n\n-------------------------------------"
            scenario.Log $"Scenario: %s{scenario.Name}"

            let mutable context = scenario.Initial

            for step in scenario.Steps do
                logStep step.Keyword step.Description scenario.Log
                let! nextContext = step.Action context
                context <- nextContext

            scenario.Log "-------------------------------------"
        }
