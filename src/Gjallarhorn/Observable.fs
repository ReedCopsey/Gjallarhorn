namespace Gjallarhorn

open System
open System.Runtime.CompilerServices

/// Internal type used to wrap an IView into an IObservable
type internal Observer<'a>(provider: IView<'a>) as self =    
    let subscribers = ResizeArray<IObserver<'a>>()
    do
        SignalManager.AddDependency provider self
    let mutable provider = Some(provider)

    let value () = DisposeHelpers.getValue provider (fun _ -> self.GetType().FullName)

    interface IDependent with
        /// Request a refresh
        member __.RequestRefresh _ =
            let v = value()
            lock subscribers (fun _ ->
                subscribers
                |> Seq.iter (fun s -> s.OnNext(v)))
    
    interface IDisposableObservable<'a> with
        
        member __.Subscribe(observer) =
            lock subscribers (fun _ -> subscribers.Add(observer))
            { new IDisposable with
                member __.Dispose() = lock subscribers (fun _ -> subscribers.Remove(observer) |> ignore) }

    interface IDisposable with
        member this.Dispose() =
            DisposeHelpers.dispose provider this
            provider <- None

[<Extension;AbstractClass;Sealed>] 
/// Provides extension methods for working on all IView instances
type ViewExtensions () =        
    [<Extension>]
    /// <summary>Converts any IView into an IObservable</summary>
    /// <remarks>The result can be Disposed to stop tracking</remarks>
    static member AsObservable(this: IView<'a>) = new Observer<'a>(this) :> IDisposableObservable<'a>

