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
      Description: 'TContext -> string
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
        (description: 'TContext -> string)
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

    // Plain-string descriptions. The keyword carries a constant message.
    let GIVEN (description: string) action scenario = appendStep Given (fun _ -> description) action scenario
    let WHEN (description: string) action scenario = appendStep When (fun _ -> description) action scenario
    let THEN (description: string) action scenario = appendStep Then (fun _ -> description) action scenario
    let AND (description: string) action scenario = appendStep And (fun _ -> description) action scenario

    // Function descriptions ('TContext -> string). The message is rendered against the context as it
    // enters the step, so it can name values an earlier step put into the context (e.g. a THEN echoing
    // what the WHEN computed). It cannot name a value the step itself produces (not in the context yet).
    let GIVENf (describe: 'TContext -> string) action scenario = appendStep Given describe action scenario
    let WHENf (describe: 'TContext -> string) action scenario = appendStep When describe action scenario
    let THENf (describe: 'TContext -> string) action scenario = appendStep Then describe action scenario
    let ANDf (describe: 'TContext -> string) action scenario = appendStep And describe action scenario

    let run (scenario: Scenario<'TContext, 'TContext>) : unit =
        scenario.Log "\n\n-------------------------------------"
        scenario.Log $"Scenario: %s{scenario.Name}"

        scenario.Steps
        |> List.fold
            (fun context step ->
                logStep step.Keyword (step.Description context) scenario.Log
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
                logStep step.Keyword (step.Description context) scenario.Log
                let! nextContext = step.Action context
                context <- nextContext

            scenario.Log "-------------------------------------"
        }
