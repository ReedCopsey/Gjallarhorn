namespace Gjallarhorn

open Gjallarhorn.Internal
open Gjallarhorn.Validation

open System
open System.Collections.Generic

/// Type which allows tracking of multiple disposables at once
type CompositeDisposable() =
    let disposables = ResizeArray<_>()

    /// Add a new disposable to this tracker
    member __.Add (disposable : IDisposable) = disposables.Add(disposable)
    /// Remove a disposable from this tracker without disposing of it
    member __.Remove (disposable : IDisposable) = disposables.Remove(disposable)

    /// Dispose all of our tracked disposables and remove them all 
    member __.Dispose() =
        disposables
        |> Seq.iter (fun d -> d.Dispose())
        disposables.Clear()

    interface IDisposable with
        /// Dispose all of our tracked disposables and remove them all 
        member this.Dispose() = this.Dispose()

module internal DisposeHelpers =
    let getValue (provider : IView<_> option) typeNameFun =
        match provider with 
        | Some(v) -> v.Value
        | None -> raise <| ObjectDisposedException(typeNameFun())        

    let setValue (provider : IMutatable<_> option) mapping value typeNameFun =
        match provider with 
        | Some(v) -> v.Value <- mapping(value)
        | None -> raise <| ObjectDisposedException(typeNameFun())        

    let disposeIfDisposable (v : obj) =
        match v with
        | :? IDisposable as d -> 
            d.Dispose()
        | _ -> ()
        
    let dispose (provider : #IView<'a> option) disposeProviderOnDispose self =
            match provider with
            | None -> ()
            | Some(v) ->
                v.RemoveDependency self
                
                if disposeProviderOnDispose then
                    disposeIfDisposable v

// A lightweight wrapper for a mutable value which provides a mechanism for change notification as needed
type internal Mutable<'a>(value : 'a) =

    let mutable v = value

    member this.Signal () = SignalManager.Signal(this)
     
    member this.Value 
        with get() = v
        and set(value) =
            if not(EqualityComparer<'a>.Default.Equals(v, value)) then            
                v <- value
                this.Signal()

    // Mutable uses SignalManager to manage its dependencies (always)
    interface IView<'a> with
        member __.Value with get() = v
        member this.AddDependency dep =            
            SignalManager.AddDependency this dep                
        member this.RemoveDependency dep =
            SignalManager.RemoveDependency this dep
        member this.Signal () =
            this.Signal()

    interface IMutatable<'a> with
        member this.Value with get() = v and set(v) = this.Value <- v
        
type internal MappingView<'a,'b>(valueProvider : IView<'a>, mapping : 'a -> 'b, disposeProviderOnDispose : bool) as self =
    do
        // TODO: Remove this until needed
        valueProvider.AddDependency self

    let mutable valueProvider = Some(valueProvider)
    let dependencies = DependencyTracker()

    let value () = 
        DisposeHelpers.getValue valueProvider (fun _ -> self.GetType().FullName)
        |> mapping

    override this.Finalize() =
        (this :> IDisposable).Dispose()
        GC.SuppressFinalize this

    abstract member Disposing : unit -> unit
    default __.Disposing() =
        ()

    abstract member Refreshing : unit -> unit
    default __.Refreshing() =
        ()

    interface IDisposableView<'b> with
        member __.Value with get() = value()
        member __.AddDependency dep =
            dependencies.Add dep 
        member __.RemoveDependency dep =
            dependencies.Remove dep |> ignore
        member this.Signal () =
            dependencies.Signal this |> ignore

    interface IDependent with
        member this.RequestRefresh _ = 
            this.Refreshing()
            dependencies.Signal this |> ignore

    interface IDisposable with
        member this.Dispose() =
            this.Disposing()
            DisposeHelpers.dispose valueProvider disposeProviderOnDispose this
            valueProvider <- None
            SignalManager.RemoveAllDependencies this

type internal MappingEditor<'a,'b>(valueProvider : IMutatable<'a>, viewMapping : 'a -> 'b, editMapping : 'b -> 'a, disposeProviderOnDispose : bool) as self =
    do
        valueProvider.AddDependency self

    let mutable valueProvider = Some(valueProvider)
    let dependencies = DependencyTracker()

    let vpToView vp = 
        Option.map (fun m -> m :> IView<_>) vp

    let value () = 
        DisposeHelpers.getValue (vpToView valueProvider) (fun _ -> self.GetType().FullName)
        |> viewMapping

    let set value =
        DisposeHelpers.setValue valueProvider editMapping value (fun _ -> self.GetType().FullName)

    abstract member Disposing : unit -> unit
    default __.Disposing() =
        ()

    override this.Finalize() =
        (this :> IDisposable).Dispose()
        GC.SuppressFinalize this

    interface IDisposableView<'b> 
    interface IMutatable<'b> with
        member __.Value with get() = value() and set(v) = set v
    interface IView<'b> with
        member __.Value with get() = value()
        member __.AddDependency dep =
            dependencies.Add dep 
        member __.RemoveDependency dep =
            dependencies.Remove dep |> ignore
        member this.Signal () =
            dependencies.Signal this |> ignore

    interface IDependent with
        member this.RequestRefresh _ = dependencies.Signal this |> ignore

    interface IDisposable with
        member this.Dispose() =
            this.Disposing()
            DisposeHelpers.dispose (vpToView valueProvider) disposeProviderOnDispose this
            valueProvider <- None
            SignalManager.RemoveAllDependencies this

type internal SteppingEditor<'a,'b>(valueProvider : IMutatable<'a>, viewMapping : 'a -> 'b, stepFunction : 'a -> 'b -> 'a, disposeProviderOnDispose : bool) as self =
    do
        valueProvider.AddDependency self

    let mutable valueProvider = Some(valueProvider)
    let dependencies = DependencyTracker()

    let vpToView vp = 
        Option.map (fun m -> m :> IView<_>) vp

    let currentValue () = DisposeHelpers.getValue (vpToView valueProvider) (fun _ -> self.GetType().FullName)
    let value () : 'b = currentValue() |> viewMapping

    let set (newValue : 'b) =
        let set v = stepFunction (currentValue()) newValue
        DisposeHelpers.setValue valueProvider set newValue (fun _ -> self.GetType().FullName)

    override this.Finalize() =
        (this :> IDisposable).Dispose()
        GC.SuppressFinalize this

    interface IDisposableView<'b> 
    interface IMutatable<'b> with
        member __.Value with get() = value() and set(v) = set v
    interface IView<'b> with
        member __.Value with get() = value()
        member __.AddDependency dep =
            dependencies.Add dep 
        member __.RemoveDependency dep =
            dependencies.Remove dep |> ignore
        member this.Signal () =
            dependencies.Signal this |> ignore

    interface IDependent with
        member this.RequestRefresh _ = 
            dependencies.Signal this |> ignore

    interface IDisposable with
        member this.Dispose() =
            DisposeHelpers.dispose (vpToView valueProvider) disposeProviderOnDispose this
            valueProvider <- None

type internal Mapping2View<'a,'b,'c>(valueProvider1 : IView<'a>, valueProvider2 : IView<'b>, mapping : 'a -> 'b -> 'c) as self =
    do
        valueProvider1.AddDependency self
        valueProvider2.AddDependency self

    let mutable valueProvider1 = Some(valueProvider1)
    let mutable valueProvider2 = Some(valueProvider2)

    let dependencies = DependencyTracker()

    let value () = 
        let v1 = DisposeHelpers.getValue valueProvider1 (fun _ -> self.GetType().FullName)
        let v2 = DisposeHelpers.getValue valueProvider2 (fun _ -> self.GetType().FullName)
        mapping v1 v2

    override this.Finalize() =
        (this :> IDisposable).Dispose()
        GC.SuppressFinalize this

    interface IDisposableView<'c> with
        member __.Value with get() = value()
        member __.AddDependency dep =
            dependencies.Add  dep         
        member __.RemoveDependency dep =
            dependencies.Remove dep |> ignore
        member this.Signal () =
            dependencies.Signal this |> ignore

    interface IDependent with
        member this.RequestRefresh _ =
            dependencies.Signal this |> ignore

    interface IDisposable with
        member this.Dispose() =
            DisposeHelpers.dispose valueProvider1 false this
            DisposeHelpers.dispose valueProvider2 false this
            valueProvider1 <- None
            valueProvider2 <- None
            SignalManager.RemoveAllDependencies this

type internal FilteredView<'a> (valueProvider : IView<'a>, filter : 'a -> bool, disposeProviderOnDispose : bool) as self =
    do
        valueProvider.AddDependency self

    let mutable v = valueProvider.Value

    let mutable valueProvider = Some(valueProvider)
    let dependencies = DependencyTracker()

    let signal() = dependencies.Signal self |> ignore

    override this.Finalize() =
        (this :> IDisposable).Dispose()
        GC.SuppressFinalize this

    interface IDisposableView<'a> with
        member __.Value with get() = v
        member __.AddDependency dep =
            dependencies.Add dep
        member __.RemoveDependency dep =
            dependencies.Remove dep |> ignore
        member this.Signal () =
            signal()

    interface IDependent with
        member __.RequestRefresh _ = 
            match valueProvider with
            | None -> ()
            | Some(provider) ->
                let value = provider.Value
                if filter(value) then
                    v <- value
                    signal()
                
    interface IDisposable with
        member this.Dispose() =
            DisposeHelpers.dispose valueProvider disposeProviderOnDispose this
            valueProvider <- None
            SignalManager.RemoveAllDependencies this

type internal FilteredEditor<'a>(valueProvider : IMutatable<'a>, filter : 'a -> bool, disposeProviderOnDispose : bool) as self =
    do
        valueProvider.AddDependency self

    let mutable v = valueProvider.Value

    let mutable valueProvider = Some(valueProvider)
    let dependencies = DependencyTracker()

    let signal() = 
        self.Signaling()
        dependencies.Signal self |> ignore

    abstract member Disposing : unit -> unit
    default __.Disposing() =
        ()

    abstract member Signaling : unit -> unit
    default __.Signaling() =
        ()

    override this.Finalize() =
        (this :> IDisposable).Dispose()
        GC.SuppressFinalize this
    interface IDisposableMutatable<'a> with
        member __.Value 
            with get() = v
            and set(newVal) =
                let inline setValueLocal nv =
                    v <- nv
                    signal()                    
                if not(EqualityComparer<'a>.Default.Equals(v, newVal)) then            
                    match filter newVal, valueProvider with
                    | true, Some vp->
                        // This will trigger a refresh request from valueProvider, and update us
                        if not(EqualityComparer<'a>.Default.Equals(vp.Value, newVal)) then            
                            vp.Value <- newVal                        
                        else
                            setValueLocal newVal
                    | _ ->
                            setValueLocal newVal

    interface IView<'a> with
        member __.Value with get() = v
        member __.AddDependency dep =
            dependencies.Add dep
        member __.RemoveDependency dep =
            dependencies.Remove dep |> ignore
        member this.Signal () =
            signal()

    interface IDependent with
        member __.RequestRefresh _ = 
            match valueProvider with
            | None -> ()
            | Some(provider) ->
                let value = provider.Value
                if filter(value) then
                    v <- value
                    signal()
                
    interface IDisposable with
        member this.Dispose() =
            this.Disposing()
            DisposeHelpers.dispose valueProvider disposeProviderOnDispose this
            valueProvider <- None
            SignalManager.RemoveAllDependencies this

type internal CachedView<'a> (valueProvider : IView<'a>) as self =
    do
        valueProvider.AddDependency self

    let mutable v = valueProvider.Value

    // Only store a weak reference to our provider
    let mutable handle = WeakReference<_>(valueProvider)

    let dependencies = DependencyTracker()

    let signal() = dependencies.Signal self |> ignore

    override this.Finalize() =
        (this :> IDisposable).Dispose()
        GC.SuppressFinalize this

    interface IDisposableView<'a> with
        member __.Value with get() = v
        member __.AddDependency dep =
            dependencies.Add dep
        member __.RemoveDependency dep =
            dependencies.Remove dep |> ignore
        member __.Signal () =
            signal()

    interface IDependent with
        member __.RequestRefresh _ =
            if handle <> null then
                match handle.TryGetTarget() with
                | true, provider -> 
                    let value = provider.Value                    
                    v <- value
                    signal()
                | false,_ -> ()

    interface IDisposable with
        member this.Dispose() =
            if handle <> null then
                match handle.TryGetTarget() with
                | true, v ->
                    v.RemoveDependency this
                    handle <- null
                | false,_ -> ()

module private ValidationHelpers =
    let isValidValue validator value =
        validate value 
        |> validator 
        |> Validation.result 
        |> isValid
type internal ValidatorMappingEditor<'a>(validator : ValidationCollector<'a> -> ValidationCollector<'a>, valueProvider : IMutatable<'a>) =
    inherit FilteredEditor<'a>(valueProvider, ValidationHelpers.isValidValue validator, true)

    let validateValue newVal =
        validate newVal
        |> validator
        |> Validation.result
    let validateCurrent () = validateValue valueProvider.Value
    let validationResult = Mutable(validateCurrent()) :> IMutatable<ValidationResult>

    let subscriptionHandle =
        let rec dependent =
            {
                new IDependent with
                    member __.RequestRefresh _ =
                        validationResult.Value <- validateCurrent()
                interface System.IDisposable with
                    member __.Dispose() = 
                        valueProvider.RemoveDependency dependent
            }
        valueProvider.AddDependency dependent
        dependent :> System.IDisposable

    member private __.EditAndValidate value =  
        validationResult.Value <- validateValue value

    override this.Signaling() =
        this.EditAndValidate (this :> IView<'a>).Value

    override __.Disposing() =
        subscriptionHandle.Dispose()
        SignalManager.RemoveAllDependencies validationResult

    interface IValidatedMutatable<'a> with
        member __.ValidationResult with get() = validationResult :> IView<ValidationResult>

        member __.IsValid = isValid validationResult.Value
            