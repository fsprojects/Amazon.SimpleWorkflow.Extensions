namespace Amazon.SimpleWorkflow.Extensions

open System
open System.Collections.Generic
open System.Diagnostics
open System.Threading.Tasks

open Microsoft.FSharp.Quotations 
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Quotations.DerivedPatterns

[<AutoOpen>]
module internal Utils =
    let nullOrWs = String.IsNullOrWhiteSpace

    let inline str x = x.ToString()
    let inline trim n (msg : string) = if msg.Length <= n then msg else msg.Substring(0, n-3) + "..."

    // operator which only executes the setter for the property captured by the expr if x is some
    // e.g  Some 2 ?-> <@ x.Length @> // sets the Length property of x to the value 2
    //      None   ?-> <@ x.Length @> // does not invoke the setter on x.Length
    let inline (?->) (x : 'a option) (expr : Expr<'a>) = 
        match x, expr with 
        | None, _ -> ()
        | Some x, PropertyGet(Some(Value(inst, _)), prop, _) when prop.CanWrite ->
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
    
    /// Applies memoization to the supplied function f
    let memoize (f : 'a -> 'b) =
        let cache = new Dictionary<'a, 'b>()

        let memoizedFunc (input : 'a) =
            // check if there is a cached result for this input
            match cache.TryGetValue(input) with
            | true, x   -> x
            | false, _  ->
                // evaluate and add result to cache
                let result = f input
                cache.Add(input, result)
                result

        // return the memoized version of f
        memoizedFunc

    /// Default function for calcuating delay (in milliseconds) between retries, based on (http://en.wikipedia.org/wiki/Exponential_backoff)
    let private exponentialDelay =
        let calcInternal attempts = 
            let rec sum acc = function | 0 -> acc | n -> sum (acc + n) (n - 1)

            let n = pown 2 attempts - 1
            let slots = float (sum 0 n) / float (n + 1)
            int (10.0 * slots)
        memoize calcInternal

    type Async = 
        /// Inspired by Tomas's answer here (http://stackoverflow.com/a/18275864/55074)
        static member StartCatchCancellation(work, ?cancellationToken, ?cancellationCont : Exception -> unit) =
            let cancellationCont = defaultArg cancellationCont (fun _ -> ())
            Async.FromContinuations(fun (cont, exnCont, _) -> 
                Async.StartWithContinuations(work, cont, exnCont, cancellationCont, ?cancellationToken = cancellationToken))

        /// Retries the async computation up to specified number of times. Optionally accepts a function to calculate
        /// the delay in milliseconds between retries, default is exponential delay with a backoff slot of 500ms.
        static member WithRetry(computation : Task<'a>, ?maxRetries, ?calcDelay) =
            let maxRetries = defaultArg maxRetries 3
            let calcDelay  = defaultArg calcDelay exponentialDelay

            let rec loop retryCount =
                async {
                    let! res = computation |> Async.AwaitTask |> Async.Catch
                    match res with
                    | Choice1Of2 x -> return Choice1Of2 x
                    | Choice2Of2 _ when retryCount <= maxRetries -> 
                        do! calcDelay (retryCount + 1) |> Async.Sleep
                        return! loop (retryCount + 1)
                    | Choice2Of2 exn -> return Choice2Of2 exn
                }
            loop 0