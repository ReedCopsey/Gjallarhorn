namespace Gjallarhorn.Bindable

open Gjallarhorn
open Gjallarhorn.Bindable

open System
open System.Collections
open System.Collections.Generic
open System.Collections.Specialized

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
        let bs = Binding.createObservableSource<'Message> ()
        let mm = Mutable.create m
        comp bs mm
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
                    remove (internalCollection.Count - 1)
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
                elif isEqual (nc.Count - offset) nc.[nc.Count - 1] then
                    for i in internalCollection.Count - 1 .. -1 .. nc.Count do
                        remove i |> changes.Add // Remove last N
                else
                    let firstChangeIndex = nc |> Seq.zip internalCollection |> Seq.tryFindIndex (fun (a,b) -> not(tEqual a b))
                    match firstChangeIndex with
                    | None -> ()
                    | Some firstDiff ->
                        if isEqual (firstDiff+offset) nc.[firstDiff] then
                            for i in offset - 1 .. -1 .. 0 do
                                remove (firstDiff + i) |> changes.Add
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
            
        if changes.Count > maxChangesBeforeReset then
            triggerChange Reset
        else
            changes |> Seq.iter triggerChange

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

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
/// Functions to work with binding collections to binding sources
module BindingCollection =
    /// Add a collection bound to the view
    let toView (source : BindingSource) name (signal : ISignal<'Coll>) (comp : Component<'Model,'Message>) =
        let cb = new BoundCollection<_,_,_>(signal, comp)
        source.ConstantToView (cb, name)
        source.AddDisposable cb
        cb :> IObservable<_>
