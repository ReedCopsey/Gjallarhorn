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

type internal BoundCollection<'Model,'Message,'Coll when 'Model : equality and 'Coll :> System.Collections.Generic.IEnumerable<'Model>> (collection : ISignal<'Coll>, comp : Component<'Model,'Message>) as self =
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

        if args <> null then
            collectionChanged.Trigger(self, args)

    let cleanItem (mm : IMutatable<'Model>, b : ObservableBindingSource<'Message>, s : IDisposable) =        
        s.Dispose()
        (b :> IDisposable).Dispose()

    let clearInternal () =
        internalCollection |> Seq.iter cleanItem
        internalCollection.Clear()

    let updateEntry (m: 'Model) (mm : IMutatable<'Model>, b : ObservableBindingSource<'Message>, s : IDisposable) =
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

    let isEqual index v =
        let (mm,_,_) = internalCollection.[index]
        mm.Value = v

    let updateCollection (newCollection : 'Coll) =
        // Big ball of imperative code here
        let currentIndex = ref 0

        let nc = ResizeArray(newCollection)

        // Handle some of the easy cases
        let changes = ResizeArray<_>()                
        match nc.Count, internalCollection.Count with
        | 0, _ -> // Clear collection
            clearInternal()
            changes.Add Reset
        | _, 0 -> // New collection
            nc |> Seq.iter (fun m -> append m |> ignore)
            changes.Add Reset
        | 1, 1 -> 
            internalCollection.[0] |> updateEntry nc.[0]                
        | _ ->
            // All other types require iteration through the series
            for i in 0 .. nc.Count - 1 do
                // If we're past the collection, just append
////                if !currentIndex > internalCollection.Count - 1 then
////                    incr currentIndex
////                    append nc.[i]
////                    |> changes.Add 
                if i > internalCollection.Count - 1 then
                    append nc.[i]
                    |> changes.Add 
                else                     
                    internalCollection.[i] |> updateEntry nc.[i]
////                        // If we're equal, must move on        
////                        if isEqual !currentIndex nc.[i] then
////                            incr currentIndex
////                        // We're equal, but at the end - so replace last element
////                        elif !currentIndex = internalCollection.Count - 1 then
////                            updateEntry nc.[i] internalCollection.[!currentIndex]
////                            incr currentIndex
////                        // If we're not equal, but the _next_ items in both collections are, we have a replacement condition
////                        elif !currentIndex < internalCollection.Count - 1 && i < nc.Count - 1 && isEqual (!currentIndex + 1) nc.[i + 1] then
////                            updateEntry nc.[i] internalCollection.[!currentIndex]
////                            incr currentIndex
////                        // If we're not equal, but the _next_ item is, we have an remove condition
////                        elif !currentIndex < internalCollection.Count - 1 && isEqual (!currentIndex + 1) nc.[i] then
////                            remove !currentIndex
////                            |> changes.Add 
////                        // If we're not equal, but the _next_ items in new collections are, we have a insert
////                        elif i < nc.Count - 1 && isEqual (!currentIndex) nc.[i + 1] then
////                            insert nc.[i] !currentIndex
////                            |> changes.Add 
////                            incr currentIndex
            // Trim off any extra past the end of the collection
            while internalCollection.Count > nc.Count do
                remove (internalCollection.Count - 1)
                |> changes.Add 
        changes.RemoveAll(fun v -> v = NoChanges) |> ignore
            
        if changes.Count > 3 then
            triggerChange Reset
        else
            changes |> Seq.iter triggerChange

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
