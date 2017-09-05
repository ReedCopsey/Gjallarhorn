namespace Gjallarhorn.Bindable

open Gjallarhorn
open Gjallarhorn.Interaction
open Gjallarhorn.Bindable

open System
open System.Collections
open System.Collections.Generic
open System.Collections.Specialized
open System.Windows.Input

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns

[<AutoOpen>]
module internal RefHelpers =
    let getPropertyFromExpression(expr : Expr) = 
        match expr with 
        | PropertyGet(o, pi, _) ->
            o, pi
        | _ -> failwith "Only quotations representing a lambda of a property getter can be used as an expression"

    let getPropertyNameFromExpression(expr : Expr) = 
        let _, pi = getPropertyFromExpression expr
        pi.Name


/// Functions to work with binding sources     
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Bind =    

    // ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Note: The Bind module is broken into submodules
    // 
    // The "core API" is all in the root level
    // The "Implementation" submodule contains implementation-specific routines and data setup by platforms
    // The "Explicit" submodule contains the alternative, explicit form API which can be used when
    //          operations depend on more than "the model"
    // ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// Internal module used to manage the actual construction of binding sources
    module Implementation =
        let mutable private createBindingSourceFunction : unit -> obj = (fun _ -> failwith "Platform targets not installed")
        let mutable private createObservableBindingFunction : System.Type -> obj = (fun _ -> failwith "Platform targets not installed")

        /// Installs a platform specific binding source creation functions
        let installCreationFunction fBS fOBS = 
            createBindingSourceFunction <- fBS
            createObservableBindingFunction <- fOBS

        /// Retrieves the platform specific creation function 
        let getCreateBindingSourceFunction () = createBindingSourceFunction() :?> BindingSource
        
        /// Retrieves the platform specific creation function 
        let getCreateObservableBindingSourceFunction<'a> () = (createObservableBindingFunction typeof<'a>) :?> ObservableBindingSource<'a>

    /// Create a binding subject for the installed platform        
    let createObservableSource<'a>() = Implementation.getCreateObservableBindingSourceFunction<'a>()

    /// Create a binding source for the installed platform        
    let createSource () = Implementation.getCreateBindingSourceFunction()

    /// Submodule providing API for explicit binding generation from names and signals instead of model/viewmodel
    module Explicit =
        /// Bind a signal to the binding source using the specified name
        let twoWay<'a> (source : BindingSource) name (signal : ISignal<'a>) =
            let edit = IO.InOut.create signal
            edit |> source.AddDisposable
            source.TrackInOut<'a,'a,'a>(name, edit)
            edit.UpdateStream

        /// Add a signal as an editor with validation, bound to a specific name
        let twoWayValidated<'a,'b> (source : BindingSource) name (validator : Validation<'a,'b>) signal =
            let edit = IO.InOut.validated validator signal
            edit |> source.AddDisposable
            source.TrackInOut<'a,'a,'b> (name, edit)
            edit.Output

        /// Add a signal as an editor with validation, bound to a specific name
        let twoWayConvertedValidated<'a,'b,'c> (source : BindingSource) name (converter : 'a -> 'b) (validator : Validation<'b,'c>) signal =
            let edit = IO.InOut.convertedValidated converter validator signal
            edit |> source.AddDisposable
            source.TrackInOut<'a,'b,'c> (name, edit)
            edit.Output

        /// Add a mutable as an editor, bound to a specific name
        let twoWayMutable<'a> (source : BindingSource) name (mutatable : IMutatable<'a>) =
            source.TrackObservable (name, mutatable)
            source.AddReadWriteProperty (name, (fun _ -> mutatable.Value), fun v -> mutatable.Value <- v)

        /// Add a mutable as an editor with validation, bound to a specific name
        let twoWayMutableValidated<'a> (source : BindingSource) name validator mutatable =
            let edit = IO.MutableInOut.validated validator mutatable
            source.TrackInOut<'a,'a,'a> (name, edit)
            edit |> source.AddDisposable
            ()

        /// Add a mutable as an editor with validation, bound to a specific name
        let twoWayMutableConvertedValidated<'a,'b> (source : BindingSource) name (converter : 'a -> 'b) (validator: Validation<'b,'a>) mutatable =
            let edit = IO.MutableInOut.convertedValidated converter validator mutatable
            source.TrackInOut<'a,'b,'a> (name, edit)
            edit |> source.AddDisposable
            ()

        /// Add a binding to a source for a signal for editing with a given property expression and validation, and returns a signal of the user edits
        let memberToFromView<'a,'b> (source : BindingSource) (expr : Expr) (validation : Validation<'a,'a>) (signal : ISignal<'b>) =
            let _, pi = getPropertyFromExpression expr
            let mapped =
                signal
                |> Signal.map (fun b -> pi.GetValue(b) :?> 'a)
            twoWayValidated<'a,'a> source pi.Name validation mapped

        /// Add a watched signal (one way property) to a binding source by name
        let oneWay (source : BindingSource) name signal =
            source.TrackInput (name, IO.Report.create signal)
        
        /// Add a watched signal (one way property) to a binding source by name with validation
        let toViewValidated (source : BindingSource) name validation signal =
            source.TrackInput (name, IO.Report.validated validation signal)        

        /// Add a constant value (one way property) to a binding source by name
        let constant name value (source : BindingSource) =
            source.ConstantToView (value, name)

        /// Bind a component to the given name
        let componentOneWay<'TModel, 'TMessage> (source : BindingSource) name (comp : Component<'TModel,'TMessage>) (signal : ISignal<'TModel>) =
            source.TrackComponent(name, comp, signal)

        /// Creates an ICommand (one way property) to a binding source by name
        let createCommand name (source : BindingSource) =
            let command = Command.createEnabled()
            source.AddDisposable command
            source.ConstantToView (command, name)
            command

        /// Creates a checked ICommand (one way property) to a binding source by name
        let createCommandChecked name canExecute (source : BindingSource) =
            let command = Command.create canExecute
            source.AddDisposable command
            source.ConstantToView (command, name)
            command    

        /// Creates an ICommand (one way property) to a binding source by name which outputs a specific message
        let createCommandParam<'a> name (source : BindingSource) =
            let command : ITrackingCommand<'a> = Command.createParamEnabled()
            source.AddDisposable command
            source.ConstantToView (command, name)
            command

        /// Creates a checked ICommand (one way property) to a binding source by name which outputs a specific message
        let createCommandParamChecked<'a> name canExecute (source : BindingSource) =
            let command : ITrackingCommand<'a> = Command.createParam canExecute
            source.AddDisposable command
            source.ConstantToView (command, name)
            command

        /// Creates an ICommand (one way property) to a binding source by name which outputs a specific message
        let createMessageCommand name message (source : BindingSource) =
            let command = Command.createEnabled()
            source.AddDisposable command
            source.ConstantToView (command, name)
            command |> Observable.map (fun _ -> message)         

        /// Creates a checked ICommand (one way property) to a binding source by name which outputs a specific message
        let createMessageCommandChecked name canExecute message (source : BindingSource) =
            let command = Command.create canExecute
            source.AddDisposable command
            source.ConstantToView (command, name)
            command |> Observable.map (fun _ -> message) 

        /// Creates an ICommand (one way property) to a binding source by name which outputs a specific message
        let createMessageParam name message (source : BindingSource) =
            let command = Command.createParamEnabled ()
            source.AddDisposable command
            source.ConstantToView (command, name)
            command |> Observable.map (fun p -> message p)

        /// Creates a checked ICommand (one way property) to a binding source by name which outputs a specific message
        let createMessageParamChecked name canExecute message (source : BindingSource) =
            let command = Command.createParam canExecute
            source.AddDisposable command
            source.ConstantToView (command, name)
            command |> Observable.map (fun p -> message p)

    /// Submodule providing API for explicit binding generation of collections
    module Collections =
        type internal ChangeType<'Message> =    
            | NoChanges
            | Reset
            | Add of index:int * orig:ObservableBindingSource<'Message>
            | Remove of index:int * orig:ObservableBindingSource<'Message>
            | Move of oldIndex:int * newIndex:int * orig:ObservableBindingSource<'Message>

        type internal BoundCollection<'Model,'Message,'Coll when 'Model : equality and 'Coll :> System.Collections.Generic.IEnumerable<'Model>> (collection : ISignal<'Coll>, comp : Component<'Model,'Message>) as self =
            [<Literal>] 
            let maxChangesBeforeReset = 5

            let internalCollection = ResizeArray<IMutatable<'Model>*ObservableBindingSource<'Message>*IDisposable>()

            let collectionChanged = Event<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>()

            let outputStream = Mutable.create Unchecked.defaultof<'Message * 'Model>

            let outputMessage msg model =
                outputStream.Value <- (msg, model)

            let triggerChange change =
                let args =
                    match change with
                    | NoChanges -> null
                    | Reset -> NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)
                    | Add(i,item) -> NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, i)
                    | Remove(i,item) -> NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, i)
                    | Move(i,j,item) -> NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, [|item|], i, j)

                if args <> null then
                    collectionChanged.Trigger(self, args)

            let cleanItem (mm : IMutatable<'Model>, b : ObservableBindingSource<'Message>, s : IDisposable) =        
                s.Dispose()
                (b :> IDisposable).Dispose()

            let clearInternal () =
                internalCollection |> Seq.iter cleanItem
                internalCollection.Clear()

            let updateEntry (m: 'Model) (mm : IMutatable<'Model>, _b, _s) =
                mm.Value <- m

            let createEntry (m : 'Model) =
                let bs = createObservableSource<'Message> ()
                let mm = Mutable.create m
                comp.Install bs mm
                |> bs.OutputObservables
                let s = bs |> Observable.subscribe (fun msg -> outputMessage msg mm.Value)
                (mm, bs, s)

            let append m =
                let entry = createEntry m
                internalCollection.Add entry
                let (_,bs,_) = entry
                Add(internalCollection.Count - 1, bs)

            let swap i =
                let (a,b,c) = internalCollection.[i + 1]
                internalCollection.[i + 1] <- internalCollection.[i] 
                internalCollection.[i] <- (a,b,c)
                Move(i+1, i, b)

            let insert m index =        
                let entry = createEntry m
                internalCollection.Insert(index, entry)
                let (_,bs,_) = entry
                Add(index, bs)

            let remove index =
                let (_, orig, _) = internalCollection.[index]
                cleanItem internalCollection.[index]
                internalCollection.RemoveAt(index)
                Remove(index, orig)

            let tEqual (mm : IMutatable<'Model>, _b, _s) v =
                mm.Value = v

            let isEqual index v =
                tEqual internalCollection.[index] v

            // Big ball of imperative code here...
            let updateCollection (newCollection : 'Coll) =
        
                let nc = ResizeArray(newCollection)

                // Handle some of the easy cases, brute force the rest
                let changes = ResizeArray<_>()     
        
                let bruteForce () =
                        // All other types require iteration through the series
                        for i in 0 .. nc.Count - 1 do
                            if i > internalCollection.Count - 1 then
                                append nc.[i]
                                |> changes.Add 
                            else                     
                                internalCollection.[i] |> updateEntry nc.[i]
                        // Trim off any extra past the end of the collection
                        while internalCollection.Count > nc.Count do
                            remove nc.Count
                            |> changes.Add 

                let computeChanges () =           
                    match nc.Count, internalCollection.Count, nc.Count - internalCollection.Count with
                    | 0, _, _ -> // Clear collection
                        clearInternal()
                        changes.Add Reset
                    | _, 0, _ -> // New collection
                        nc |> Seq.iter (fun m -> append m |> ignore)
                        changes.Add Reset
                    | 1, 1, _ -> 
                        internalCollection.[0] |> updateEntry nc.[0]                
                    | _, _, sizeChange when sizeChange < 0 ->                
                        let offset = -sizeChange
                        // We need to remove a single element - check some common occurrences
                        if isEqual offset nc.[0] then
                            for i in offset - 1 .. -1 .. 0 do
                                remove i |> changes.Add // Remove first N
                        elif offset <= nc.Count && isEqual (nc.Count - offset) nc.[nc.Count - 1] then
                            // for i in internalCollection.Count - 1 .. -1 .. nc.Count do
                            for i in nc.Count .. internalCollection.Count - 1 do
                                remove nc.Count |> changes.Add // Remove last N
                        else
                            let firstChangeIndex = nc |> Seq.zip internalCollection |> Seq.tryFindIndex (fun (a,b) -> not(tEqual a b))
                            match firstChangeIndex with
                            | None -> ()
                            | Some firstDiff ->
                                if isEqual (firstDiff+offset) nc.[firstDiff] then
                                    for i in 0 .. offset - 1 do
                                        remove firstDiff |> changes.Add
                        bruteForce ()
                    | _, _, sizeChange when sizeChange > 0 ->                
                        let offset = sizeChange
                        // We need to remove a single element - check some common occurrences
                        if isEqual 0 nc.[offset] then
                            for i in 0 .. offset - 1 do
                                insert nc.[i] i |> changes.Add // Add first N
                        elif isEqual (internalCollection.Count - 1) nc.[nc.Count - offset] then
                            for i in internalCollection.Count .. nc.Count - 1 do
                                append nc.[i] |> changes.Add // Add last N
                        else
                            let firstChangeIndex = nc |> Seq.zip internalCollection |> Seq.tryFindIndex (fun (a,b) -> not(tEqual a b))
                            match firstChangeIndex with
                            | None -> ()
                            | Some firstDiff ->
                                if isEqual (firstDiff) nc.[firstDiff + offset] then
                                    for i in firstDiff .. firstDiff + offset - 1 do
                                        insert nc.[i] i |> changes.Add
                        bruteForce ()
                    | _, _, 0 ->
                        // We're going to check for a swap of 2 elements
                        let firstChangeIndex = nc |> Seq.zip internalCollection |> Seq.tryFindIndex (fun (a,b) -> not(tEqual a b))
                        match firstChangeIndex with
                        | Some firstDiff when firstDiff < nc.Count - 1 -> // Check element + next for swap
                            if isEqual (firstDiff) nc.[firstDiff + 1] && isEqual (firstDiff + 1) nc.[firstDiff] then
                                swap firstDiff                        
                                |> changes.Add
                        | _ -> ()
                
                        bruteForce()
                    | _ -> 
                        bruteForce() // Should always be covered by above
        
                computeChanges ()
                changes.RemoveAll(fun v -> v = NoChanges) |> ignore
           
                let triggerChanges changes =
        //            let removes =
        //                changes 
        //                |> Seq.filter (fun c -> match c with | Remove(_) -> true | _ -> false)
        //                |> Seq.length
        //
        //            // TODO:    This breaks occasionally - if there are 3 elements, and remove the 1st and last, remove dies. 
        //            //          Make a good test, and figure out why, then switch this back to just removes
        //            if removes > 1 then
        //                triggerChange Reset
        //            else
                      changes |> Seq.iter triggerChange

                if changes.Count > maxChangesBeforeReset then
                    triggerChange Reset
                else
                    triggerChanges changes

            // Fill the collection with the initial state
            do
                collection.Value |> Seq.iter (fun m -> append m |> ignore)

            let sub = collection |> Signal.Subscription.create updateCollection

            member this.Items with get () = (this :> IEnumerable<obj>)

            interface IObservable<'Message * 'Model> with   
                member __.Subscribe obs = (outputStream :> IObservable<'Message * 'Model>).Subscribe(obs)

            interface IEnumerable<obj> with
                member __.GetEnumerator () =
                    let seq = 
                        internalCollection
                        |> Seq.map (fun (a,b,c) -> box b)
                    seq.GetEnumerator()

            interface IEnumerable with
                member __.GetEnumerator () = 
                    let seq = 
                        internalCollection
                        |> Seq.map (fun (a,b,c) -> box b)
                    (seq :> IEnumerable).GetEnumerator()

            // We implement this for better support in WPF collection space,
            // but it should never be used
            interface ICollection with 
                member __.Count: int = internalCollection.Count
            
                member __.CopyTo(array: Array, index: int) = 
                    for i in 0 .. internalCollection.Count - 1 do
                        let (_,b,_) = internalCollection.[i]
                        array.SetValue(b, i + index)

                member __.SyncRoot = (internalCollection :> ICollection).SyncRoot
                member __.IsSynchronized = false            

            interface IList with
                member __.Add(value: obj): int = failwith "Not implemented"
                member __.Insert(index: int, value: obj) =  failwith "Not implemented"
                member __.Clear(): unit = failwith "Not implemented"
        
                member __.Contains(value: obj) = 
                    internalCollection
                    |> Seq.tryFind (fun (_,b,_) -> b.Equals(value))
                    |> Option.isSome

                member __.IndexOf(value: obj) = 
                    let i = 
                        internalCollection
                        |> Seq.tryFindIndex (fun (_,b,_) -> b.Equals(value))
                    defaultArg i -1

                member __.IsFixedSize = false
                member __.IsReadOnly = false
            
                member __.Item
                    with get (index: int): obj = 
                        let (_,b,_) = internalCollection.[index]
                        box b
                    and set (index: int) (v: obj): unit =  failwith "Not implemented"

                member __.Remove(value: obj): unit = failwith "Not implemented"
                member __.RemoveAt(index: int): unit = failwith "Not implemented"
        
        

            interface INotifyCollectionChanged with
                [<CLIEvent>]
                member __.CollectionChanged = collectionChanged.Publish

            interface IDisposable with
                member __.Dispose() =
                    sub.Dispose()

        /// Add a collection bound to the view
        let oneWay (source : BindingSource) name (signal : ISignal<'Coll>) (comp : Component<'Model,'Message>) =
            let cb = new BoundCollection<_,_,_>(signal, comp)
            source.ConstantToView (cb, name)
            source.AddDisposable cb
            cb :> IObservable<_>

    // /////////////////////////////////////////////////////////////////////////////////////
    // Standard API for binding from here down
    
    /// Add a watched signal (one way property) to a binding source by name
    let oneWay<'Model, 'Prop, 'Msg> (getter : 'Model -> 'Prop) (name : Expr<'Prop>) : BindingSource -> ISignal<'Model> -> IObservable<'Msg> option =
        fun (source : BindingSource) (signal : ISignal<'Model>) ->
            let mapped = signal |> Signal.map getter
            source.TrackInput (getPropertyNameFromExpression name, IO.Report.create mapped)
            None

    /// Add a watched signal (one way validated property) to a binding source by name
    let oneWayValidated<'Model, 'Prop, 'Msg> (getter : 'Model -> 'Prop) validation (name : Expr<'Prop>) : BindingSource -> ISignal<'Model> -> IObservable<'Msg> option =
        fun (source : BindingSource) (signal : ISignal<'Model>) ->
            let mapped = signal |> Signal.map getter
            source.TrackInput (getPropertyNameFromExpression name, IO.Report.validated validation mapped)      
            None

    /// Add a two way property to a binding source by name
    let twoWay<'Model, 'Prop, 'Msg> (getter : 'Model -> 'Prop) (setter : 'Prop -> 'Msg) (name : Expr<'Prop>) : BindingSource -> ISignal<'Model> -> IObservable<'Msg> option =
        fun (source : BindingSource) (signal : ISignal<'Model>) ->
            let name = getPropertyNameFromExpression name
            let mapped = signal |> Signal.map getter
            let output = Explicit.twoWay<'Prop> source name mapped
            output
            |> Observable.map (setter)
            |> Some

    /// Add a two way property to a binding source by name
    let twoWayValidated<'Model, 'Prop, 'Msg> (getter : 'Model -> 'Prop) (validation : Validation<'Prop,'Prop>) (setter : 'Prop -> 'Msg) (name : Expr<'Prop>) : BindingSource -> ISignal<'Model> -> IObservable<'Msg> option =
        fun (source : BindingSource) (signal : ISignal<'Model>) ->
            let name = getPropertyNameFromExpression name
            let mapped = signal |> Signal.map getter
            let validated = Explicit.twoWayValidated<'Prop,'Prop> source name validation mapped
            validated
            |> Observable.toMessage (setter)
            |> Some

    /// Creates an ICommand (one way property) to a binding source by name which outputs a specific message
    let cmd<'Model,'Msg> (name : Expr<VmCmd<'Msg>>) : BindingSource -> ISignal<'Model> -> IObservable<'Msg> option =
        fun (source : BindingSource) (_signal : ISignal<'Model>) ->
            let o, pi = getPropertyFromExpression name
            match o.Value with
            | PropertyGet(_,v,_) ->
                let msg = pi.GetValue(v.GetValue(null)) :?> VmCmd<'Msg>
                Explicit.createMessageCommand pi.Name msg.Value source
            | _ -> failwith "Bad expression"        
            |> Some

    /// Creates an ICommand (one way property) to a binding source by name which outputs a specific message
    let cmdIf<'Model,'Msg> canExecute (name : Expr<VmCmd<'Msg>>) : BindingSource -> ISignal<'Model> -> IObservable<'Msg> option =
        fun (source : BindingSource) (signal : ISignal<'Model>) ->
            let o, pi = getPropertyFromExpression name
            match o.Value with
            | PropertyGet(_,v,_) ->
                let msg = pi.GetValue(v.GetValue(null)) :?> VmCmd<'Msg>
                let canExecuteSignal = signal |> Signal.map canExecute
                Explicit.createMessageCommandChecked pi.Name canExecuteSignal msg.Value source
            | _ -> failwith "Bad expression"        
            |> Some

    /// Bind a component as a two-way property, acting as a reducer for messages from the component
    let comp<'Model,'Msg,'Submodel,'Submsg> (getter : 'Model -> 'Submodel) (componentVm : Component<'Submodel, 'Submsg>) (mapper : 'Submsg * 'Submodel -> 'Msg) (name : Expr<'Submodel>) =
        fun (source : BindingSource) (signal : ISignal<'Model>) ->
            let name = getPropertyNameFromExpression name
            let mapped = signal |> Signal.map getter
            let output = Explicit.componentOneWay source name componentVm mapped 
            output
            |> Observable.map (fun subMsg -> mapper(subMsg, mapped.Value))
            |> Some  
       
    /// Bind a collection as a one-way property, acting as a reducer for messages from the individual components of the collection
    let collection<'Model,'Msg,'Submodel,'Submsg when 'Submodel : equality> (getter : 'Model -> 'Submodel seq) (collectionVm : Component<'Submodel, 'Submsg>) (mapper : 'Submsg * 'Submodel -> 'Msg) (name : Expr<'Submodel seq>) =
        fun (source : BindingSource) (signal : ISignal<'Model>) ->
            let name = getPropertyNameFromExpression name
            let mapped = signal |> Signal.map getter
            let output = Collections.oneWay source name mapped collectionVm
            output
            |> Observable.map mapper
            |> Some

    /// Convert from new API to explicit form
    let toExplicit<'Model,'Msg> (source : BindingSource) (model : ISignal<'Model>) (list : (BindingSource -> ISignal<'Model> -> IObservable<'Msg> option) list) : IObservable<'Msg> list =
        list
        |> List.choose (fun v -> v source model)

