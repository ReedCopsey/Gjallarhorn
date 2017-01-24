namespace Gjallarhorn.Bindable.Tests

open Gjallarhorn
open Gjallarhorn.Wpf
open Gjallarhorn.Bindable
open Gjallarhorn.Validation
open Gjallarhorn.Validation.Validators
open System.ComponentModel
open NUnit.Framework
open Binding
open System
open System.Collections.Specialized

type CollectionChangedObserver(o : INotifyCollectionChanged) = 
    let changes = System.Collections.Generic.Dictionary<NotifyCollectionChangedAction, NotifyCollectionChangedEventArgs list>()

    do
        o.CollectionChanged.Add(fun args ->            
            let current = 
                match changes.ContainsKey args.Action with
                | true -> changes.[args.Action]
                | false -> []
            changes.[args.Action] <- current @ [ args ])

    member __.Item 
        with get(action) = 
            let success, args = changes.TryGetValue action
            match success with
            | true -> args 
            | false -> []

[<TestFixture>]
type CollectionBindingTest() =
    let intComponent subscription (source : BindingSource) (model : ISignal<int>) =
        model 
        |> Signal.Subscription.create subscription 
        |> source.AddDisposable
        [ ]

    [<TestFixtureSetUp>]
    member __.Initialize() =
        Gjallarhorn.Wpf.Platform.install(false) |> ignore

    [<Test>]
    member __.``BoundCollection raises collection changed`` () =
        let l = Mutable.create [ 1 ; 2 ; 3 ; 4 ]

        use bc = new BoundCollection<int, unit, int list>(l, intComponent ignore)

        let obs = CollectionChangedObserver(bc)    

        [ 5 ; 2 ] |> Mutable.set l
    
        Assert.AreEqual(2, List.length obs.[NotifyCollectionChangedAction.Remove])

    [<Test>]
    member __.``BoundCollection raises collection changed with remove for single element missing in center`` () =
        let l = Mutable.create [ 1 ; 2 ; 3 ; 4 ]

        use bc = new BoundCollection<int, unit, int list>(l, intComponent ignore)

        let obs = CollectionChangedObserver(bc)    

        [ 1 ; 2 ; 4 ] |> Mutable.set l
    
        Assert.AreEqual(1, List.length obs.[NotifyCollectionChangedAction.Remove])


    [<Test>]
    member __.``BoundCollection raises add on cons`` () =
        let l = Mutable.create [ 2 ; 3 ; 4 ]

        use bc = new BoundCollection<int, unit, int list>(l, intComponent ignore)

        let obs = CollectionChangedObserver(bc)    

        let orig = Mutable.get l
        
        1 :: orig
        |> Mutable.set l
    
        Assert.AreEqual(1, List.length obs.[NotifyCollectionChangedAction.Add])

    [<Test>]
    member __.``BoundCollection raises add with correct index on cons`` () =
        let l = Mutable.create [ 2 ; 3 ; 4 ]

        use bc = new BoundCollection<int, unit, int list>(l, intComponent ignore)

        let obs = CollectionChangedObserver(bc)    

        let orig = Mutable.get l
        
        1 :: orig
        |> Mutable.set l
    
        let change = obs.[NotifyCollectionChangedAction.Add] |> List.head
        Assert.AreEqual([ 1 ; 2 ; 3 ; 4 ] , l.Value)
        Assert.AreEqual(0, change.NewStartingIndex)

    [<Test>]
    member __.``BoundCollection raises add with correct indices on cons x 2`` () =
        let l = Mutable.create [ 2 ; 3 ; 4 ]

        use bc = new BoundCollection<int, unit, int list>(l, intComponent ignore)

        let obs = CollectionChangedObserver(bc)    

        let orig = Mutable.get l
        
        0 :: 1 :: orig
        |> Mutable.set l
    
        let changes = 
            obs.[NotifyCollectionChangedAction.Add]            
            |> List.map (fun v -> v.NewStartingIndex)            
        Assert.AreEqual([ 0 ; 1 ; 2 ; 3 ; 4 ] , l.Value)
        Assert.AreEqual([ 0 ; 1 ], changes)


    [<Test>]
    member __.``BoundCollection raises add on append`` () =
        let l = Mutable.create [ 2 ; 3 ; 4 ]

        use bc = new BoundCollection<int, unit, int list>(l, intComponent ignore)

        let obs = CollectionChangedObserver(bc)    

        let orig = Mutable.get l
        
        orig @ [ 5 ]
        |> Mutable.set l
    
        Assert.AreEqual(1, List.length obs.[NotifyCollectionChangedAction.Add])
        Assert.AreEqual([ 2 ; 3 ; 4 ; 5] , l.Value)

    [<Test>]
    member __.``BoundCollection raises adds on multiple appends`` () =
        let l = Mutable.create [ 2 ; 3 ; 4 ]

        use bc = new BoundCollection<int, unit, int list>(l, intComponent ignore)

        let obs = CollectionChangedObserver(bc)    

        let orig = Mutable.get l
        
        orig @ [ 5 ; 6 ]
        |> Mutable.set l
    
        Assert.AreEqual(2, List.length obs.[NotifyCollectionChangedAction.Add])
        Assert.AreEqual([ 2 ; 3 ; 4 ; 5 ; 6] , l.Value)

    [<Test>]
    member __.``BoundCollection raises adds on multiple inserts`` () =
        let l = Mutable.create [ 1 ; 4 ; 5 ]

        use bc = new BoundCollection<int, unit, int list>(l, intComponent ignore)

        let obs = CollectionChangedObserver(bc)    
        
        [ 1 ; 2 ; 3 ; 4 ; 5 ]
        |> Mutable.set l
    
        let changes = 
            obs.[NotifyCollectionChangedAction.Add]            
            |> List.map (fun v -> v.NewStartingIndex)            
        Assert.AreEqual([ 1 ; 2 ; 3 ; 4 ; 5 ] , l.Value)
        Assert.AreEqual([ 1 ; 2 ], changes)

    [<Test>]
    member __.``BoundCollection raises adds but doesn't trigger property changed on multiple inserts`` () =
        let l = Mutable.create [ 1 ; 4 ; 5 ]
        let count = ref 0
        let subscription _ = incr count
        use bc = new BoundCollection<int, unit, int list>(l, intComponent subscription)

        let obs = CollectionChangedObserver(bc)    
        
        [ 1 ; 2 ; 3 ; 4 ; 5 ]
        |> Mutable.set l
    
        let changes = 
            obs.[NotifyCollectionChangedAction.Add]            
            |> List.map (fun v -> v.NewStartingIndex)            
        Assert.AreEqual([ 1 ; 2 ; 3 ; 4 ; 5 ] , l.Value)
        Assert.AreEqual([ 1 ; 2 ], changes)
        Assert.AreEqual(0, !count)

    [<Test>]
    member __.``BoundCollection causes property changed on multiple disjoint inserts`` () =
        let l = Mutable.create [ 1 ; 3 ; 5 ]
        let count = ref 0
        let subscription _ = incr count
        use bc = new BoundCollection<int, unit, int list>(l, intComponent subscription)

        let obs = CollectionChangedObserver(bc)    
        
        [ 1 ; 2 ; 3 ; 4 ; 5 ]
        |> Mutable.set l
    
        let changes = 
            obs.[NotifyCollectionChangedAction.Add]            
            |> List.map (fun v -> v.NewStartingIndex)            
        Assert.AreEqual([ 1 ; 2 ; 3 ; 4 ; 5 ] , l.Value)
        Assert.AreEqual([ 3 ; 4 ], changes)
        Assert.AreEqual(2, !count) // original 3 & 5 get overwritten

    [<Test>]
    member __.``BoundCollection raises single remove if first element is removed`` () =
        let l = Mutable.create [ 1 ; 2 ; 3 ]
        let count = ref 0
        let subscription _ = incr count
        use bc = new BoundCollection<int, unit, int list>(l, intComponent subscription)

        let obs = CollectionChangedObserver(bc)    
        
        [ 2 ; 3 ]
        |> Mutable.set l
    
        let changes = 
            obs.[NotifyCollectionChangedAction.Remove]            
            |> List.map (fun v -> v.OldStartingIndex)            
        Assert.AreEqual([ 2 ; 3  ] , l.Value)
        Assert.AreEqual([ 0 ], changes)
        Assert.AreEqual(0, !count) // original 3 & 5 get overwritten

    [<Test>]
    member __.``BoundCollection raises single remove if last element is removed`` () =
        let l = Mutable.create [ 1 ; 2 ; 3 ]
        let count = ref 0
        let subscription _ = incr count
        use bc = new BoundCollection<int, unit, int list>(l, intComponent subscription)

        let obs = CollectionChangedObserver(bc)    
        
        [ 1 ; 2 ]
        |> Mutable.set l
    
        let changes = 
            obs.[NotifyCollectionChangedAction.Remove]            
            |> List.map (fun v -> v.OldStartingIndex)            
        Assert.AreEqual([ 1 ; 2 ] , l.Value)
        Assert.AreEqual([ 2 ], changes)
        Assert.AreEqual(0, !count) // original 3 & 5 get overwritten

    [<Test>]
    member __.``BoundCollection raises single remove if single element is removed`` () =
        let l = Mutable.create [ 1 ; 2 ; 3 ]
        let count = ref 0
        let subscription _ = incr count
        use bc = new BoundCollection<int, unit, int list>(l, intComponent subscription)

        let obs = CollectionChangedObserver(bc)    
        
        [ 1 ; 3 ]
        |> Mutable.set l
    
        let changes = 
            obs.[NotifyCollectionChangedAction.Remove]            
            |> List.map (fun v -> v.OldStartingIndex)            
        Assert.AreEqual([ 1 ; 3 ] , l.Value)
        Assert.AreEqual([ 1 ], changes)
        Assert.AreEqual(0, !count) // original 3 & 5 get overwritten

    [<Test>]
    member __.``BoundCollection raises removes if consecutive elements are removed`` () =
        let l = Mutable.create [ 1 ; 2 ; 3 ; 4 ; 5]
        let count = ref 0
        let subscription _ = incr count
        use bc = new BoundCollection<int, unit, int list>(l, intComponent subscription)

        let obs = CollectionChangedObserver(bc)    
        
        [ 1 ; 4 ; 5]
        |> Mutable.set l
    
        let changes = 
            obs.[NotifyCollectionChangedAction.Remove]            
            |> List.map (fun v -> v.OldStartingIndex)            
        Assert.AreEqual([ 1 ; 4 ; 5] , l.Value)
        Assert.AreEqual([ 1 ; 1 ], changes)
        Assert.AreEqual(0, !count) // original 3 & 5 get overwritten

    [<Test>]
    member __.``BoundCollection raises reset if many elements are removed`` () =
        let l = Mutable.create [ 1 .. 100 ]
        let count = ref 0
        let subscription _ = incr count
        use bc = new BoundCollection<int, unit, int list>(l, intComponent subscription)

        let obs = CollectionChangedObserver(bc)    
        
        [ 10 .. 100 ]
        |> Mutable.set l
    
        let changes = 
            obs.[NotifyCollectionChangedAction.Remove]            
            |> List.map (fun v -> v.OldStartingIndex)            
        let resets = obs.[NotifyCollectionChangedAction.Reset] |> List.length
        Assert.AreEqual([ 10 .. 100 ], l.Value)
        Assert.AreEqual([ ], changes)
        Assert.AreEqual(1, resets)
        Assert.AreEqual(0, !count) // original 3 & 5 get overwritten

    [<Test>]
    member __.``BoundCollection raises move if two consecutive elements are swapped`` () =
        let l = Mutable.create [ 1 ; 2 ; 3 ; 4 ]
        let count = ref 0
        let subscription _ = incr count
        use bc = new BoundCollection<int, unit, int list>(l, intComponent subscription)

        let obs = CollectionChangedObserver(bc)    
        
        [ 1 ; 3 ; 2 ; 4 ]
        |> Mutable.set l
    
        let changes = 
            obs.[NotifyCollectionChangedAction.Move]            
            |> List.map (fun v -> v.OldStartingIndex)            
        Assert.AreEqual([ 1 ; 3 ; 2 ; 4 ], l.Value)
        Assert.AreEqual([ 1 ], changes)
        Assert.AreEqual(0, !count) // original 3 & 5 get overwritten

    [<Test>]
    member __.``BoundCollection raises move if first two consecutive elements are swapped`` () =
        let l = Mutable.create [ 1 ; 2 ; 3 ; 4 ]
        let count = ref 0
        let subscription _ = incr count
        use bc = new BoundCollection<int, unit, int list>(l, intComponent subscription)

        let obs = CollectionChangedObserver(bc)    
        
        [ 2 ; 1 ; 3 ; 4 ]
        |> Mutable.set l
    
        let changes = 
            obs.[NotifyCollectionChangedAction.Move]            
            |> List.map (fun v -> v.OldStartingIndex)            
        Assert.AreEqual([ 2 ; 1 ; 3 ; 4 ], l.Value)
        Assert.AreEqual([ 0 ], changes)
        Assert.AreEqual(0, !count) // original 3 & 5 get overwritten
