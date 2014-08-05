namespace Amazon.SimpleWorkflow.Extensions

open System

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Quotations.DerivedPatterns

[<AutoOpen>]
module Utils =        
    let nullOrWs = String.IsNullOrWhiteSpace

    let inline str x = x.ToString()

    // operator which only executes the setter for the property captured by the expr if x is some
    // e.g  Some 2 ?-> <@ x.Length @> // sets the Length property of x to the value 2
    //      None   ?-> <@ x.Length @> // does not invoke the setter on x.Length
    let inline (?->) (x : 'a option) (expr : Expr<'a>) = 
        match x, expr with 
        | None, _ -> ()
        | Some x, PropertyGet(Some(PropertyGet(_, instProp, _)), prop, _) when prop.CanWrite ->
            let inst = instProp.GetGetMethod().Invoke(null, [||])
            prop.GetSetMethod().Invoke(inst, [| x |]) |> ignore
        | _ -> failwith "Expression is not a settable property : %A" expr

    // reverse operator of the above
    let inline (<-?) (expr : Expr<'a>) (x : 'a option) = x ?-> expr

    let inline (?>>) (x : 'a option) (f : 'a -> 'b) = match x with | Some x -> f x |> Some | _ -> None

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