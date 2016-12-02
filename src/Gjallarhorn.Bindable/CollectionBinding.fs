namespace Gjallarhorn.Bindable

open Gjallarhorn
open Gjallarhorn.Helpers
open Gjallarhorn.Internal
open Gjallarhorn.Interaction
open Gjallarhorn.Validation

open Gjallarhorn.Bindable

open System
open System.Collections
open System.Collections.Generic
open System.Collections.Specialized
open System.ComponentModel
open System.Windows.Input


type internal ChangeType<'Message> =    
    | None
    | Reset
    | Add of index:int * orig:ObservableBindingSource<'Message>
    | Remove of index:int * orig:ObservableBindingSource<'Message>


type BoundCollection<'Model,'Message,'Coll when 'Model : equality and 'Coll :> System.Collections.Generic.IEnumerable<'Model>> (collection : ISignal<'Coll>, comp : Component<'Model,'Message>) as self =
    let internalCollection = ResizeArray<IMutatable<'Model>*ObservableBindingSource<'Message>*IDisposable>()

    let collectionChanged = Event<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>()

    let outputStream = Mutable.create Unchecked.defaultof<'Message * 'Model>

    let outputMessage msg model =
        outputStream.Value <- (msg, model)

    let triggerChange change =
        let args =
            match change with
            | None -> null
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

        let toSeq v = seq { yield v }

        // Handle some of the easy cases
        let changes =
            match nc.Count, internalCollection.Count with
            | 0, _ -> // Clear collection
                clearInternal()
                toSeq Reset
            | _, 0 -> // New collection
                nc |> Seq.map append |> ignore
                toSeq Reset
            | 1, 1 -> 
                internalCollection.[0] |> updateEntry nc.[0]
                toSeq None
            | _ ->
                // All other types require iteration through the series
                seq {
                    for i in 0 .. nc.Count - 1 do
                        // If we're past the collection, just append
                        if !currentIndex > internalCollection.Count - 1 then
                            incr currentIndex
                            yield append nc.[i]
                        else                     
                            // If we're equal, must move on        
                            if isEqual !currentIndex nc.[i] then
                                incr currentIndex
                            // We're equal, but at the end - so replace last element
                            elif !currentIndex = internalCollection.Count - 1 then
                                updateEntry nc.[i] internalCollection.[!currentIndex]
                                incr currentIndex
                            // If we're not equal, but the _next_ items in both collections are, we have a replacement condition
                            elif !currentIndex < internalCollection.Count - 1 && i < nc.Count - 1 && isEqual (!currentIndex + 1) nc.[i + 1] then
                                updateEntry nc.[i] internalCollection.[!currentIndex]
                                incr currentIndex
                            // If we're not equal, but the _next_ item is, we have an remove condition
                            elif !currentIndex < internalCollection.Count - 1 && isEqual (!currentIndex + 1) nc.[i] then
                                yield remove !currentIndex
                            // If we're not equal, but the _next_ items in new collections are, we have a insert
                            elif i < nc.Count - 1 && isEqual (!currentIndex) nc.[i + 1] then
                                yield insert nc.[i] !currentIndex
                                incr currentIndex
                    // Trim off any extra past the end of the collection
                    while internalCollection.Count > nc.Count do
                        yield remove (internalCollection.Count - 1)
                }                               

        if Seq.length (Seq.truncate 4 changes) > 3 then
            triggerChange Reset
        else
            changes |> Seq.iter triggerChange

    let sub = collection |> Signal.Subscription.create updateCollection

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

    interface INotifyCollectionChanged with
        [<CLIEvent>]
        member __.CollectionChanged = collectionChanged.Publish

    interface IDisposable with
        member __.Dispose() =
            sub.Dispose()
