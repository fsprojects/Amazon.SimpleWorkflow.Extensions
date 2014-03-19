namespace Amazon.SimpleWorkflow.Extensions

open System

[<AutoOpen>]
module Utils =        
    let nullOrWs = String.IsNullOrWhiteSpace

    let inline str x = x.ToString()

    // operator which only executes f with the value of x if x is not None
    let inline (?->) (x : 'a option) (f : 'a -> 'b) = match x with | Some x' -> f x' |> ignore | _ -> ()

    // reverse operator of the above
    let inline (<-?) (f : 'a -> 'b) (x : 'a option) = x ?-> f

    // helper functions to convert from one type to an option type (used in the conversion from HistoryEvent to EventType
    let inline stringOp x  = if String.IsNullOrWhiteSpace x then None else Some x
    let inline taskListOp (x : Amazon.SimpleWorkflow.Model.TaskList) = match x with | null -> None | _ -> stringOp x.Name
    let inline (!) (str : string) = stringOp str

    let inline asOption x = match x with | null -> None | _ -> Some x
    let inline (!?) x = asOption x

    type Async = 
        /// Inspired by Tomas's answer here (http://stackoverflow.com/a/18275864/55074)
        static member StartCatchCancellation(work, ?cancellationToken) =
            Async.FromContinuations(fun (cont, exnCont, _) -> 
                let cancellationCont e = exnCont e

                Async.StartWithContinuations(work, cont, exnCont, cancellationCont, ?cancellationToken = cancellationToken))