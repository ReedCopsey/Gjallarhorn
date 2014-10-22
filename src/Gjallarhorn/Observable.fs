namespace Gjallarhorn

open System
open System.Runtime.CompilerServices

/// Internal type used to wrap an IView<'a> into an IObservable<'a>
type internal Observer<'a>(provider: IView<'a>) as self =    
    let subscribers = ResizeArray<IObserver<'a>>()
    do
        SignalManager.AddDependency provider self

    interface IDependent with
        /// Request a refresh
        member __.RequestRefresh _ =
            let v = provider.Value
            lock subscribers (fun _ ->
                subscribers
                |> Seq.iter (fun s -> s.OnNext(v)))
    interface IObservable<'a> with
        member __.Subscribe(observer) =
            lock subscribers (fun _ -> subscribers.Add(observer))
            { new IDisposable with
                member __.Dispose() = lock subscribers (fun _ -> subscribers.Remove(observer) |> ignore) }


[<Extension>] 
type ViewExtensions () =        
    [<Extension>]
    static member AsObservable(this: IView<'a>) =
        Observer(this) :> IObservable<'a>

