module Gjallarhorn.Tests.Observable

open Gjallarhorn

open System
open NUnit.Framework

[<TestCase(1, 2)>]
[<TestCase(42, 32)>]
[<TestCase(Int32.MinValue, Int32.MaxValue)>]
let ``Mutation triggers IObservable`` (start : int) finish =
    let result = Mutable.create start
    
    let changedValue = ref result.Value
    use subscription = 
        result |> Observable.subscribe((fun i -> changedValue := i))
    
    result.Value <- finish
    Assert.AreEqual(finish, !changedValue)

[<TestCase(1, 2, "2")>]
[<TestCase(42, 32, "32")>]
[<TestCase(Int32.MinValue, Int32.MaxValue, "2147483647")>]
let ``Signal triggers IObservable`` (start : int) (finish:int) (viewFinish: string) =
    let result = Mutable.create start
    let view = Signal.map (fun i -> i.ToString()) result    
    
    let changedValue = ref view.Value
    use subscription = 
        view |> Observable.subscribe((fun s -> changedValue := s))
        
    result.Value <- finish
    Assert.AreEqual(viewFinish, !changedValue)

[<TestCase(1, 2)>]
[<TestCase(42, 32)>]
[<TestCase(Int32.MinValue, Int32.MaxValue)>]
let ``Observable Dispose stops tracking`` (start:int) finish =
    let result = Mutable.create start    
    
    let changedValue = ref result.Value
    let subscription = 
        result |> Observable.subscribe((fun i -> changedValue := i))
        
    // Should track/change
    result.Value <- finish
    Assert.AreEqual(finish, !changedValue)
    subscription.Dispose()
    
    // Should not track/change anymore
    result.Value <- start
    Assert.AreEqual(finish, !changedValue)
 
[<Test;TestCaseSource(typeof<Utilities>,"CasesStart")>]
let ``Signal\fromObservable initializes properly`` start =
    let evt = Event<'a>()
    let obs = evt.Publish

    let signal = Signal.fromObservable start obs
    Assert.AreEqual(box start, signal.Value)    

[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEnd")>]
let ``Signal\fromObservable tracks changes in values`` start finish =
    let evt = Event<_>()
    let obs = evt.Publish

    let signal = Signal.fromObservable start obs 
    Assert.AreEqual(box start, signal.Value)

    evt.Trigger finish
    Assert.AreEqual(box finish, signal.Value)    

[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEnd")>]
let ``Signal\fromObservable will allow observable to GC when signal GCs`` start finish =
    let mutable obs = Some (Mutable.create start)
    
    let wr = WeakReference(obs.Value)
    let mutable signal = Some(Signal.fromObservable start obs.Value)
    Assert.AreEqual(box start, signal.Value.Value)

    obs.Value.Value <- finish
    Assert.AreEqual(box finish, signal.Value.Value)    

    obs <- None

    GC.Collect()
    // Check that we're alive
    Assert.IsTrue(wr.IsAlive)
    
    // Required to keep optimizer from cleaning us up :)
    GC.KeepAlive(signal.Value)
    signal <- None
    GC.Collect()
    Assert.IsFalse(wr.IsAlive)

    GC.KeepAlive(obs)

[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEnd")>]
let ``Signal\fromObservable will allow observable to GC on Dispose`` start finish =
    let mutable evt = Some (Event<_>())
    
    let wr = WeakReference(evt.Value)
    let signal = Signal.fromObservable start evt.Value.Publish
    Assert.AreEqual(box start, signal.Value)

    evt.Value.Trigger finish
    Assert.AreEqual(box finish, signal.Value)    

    evt <- None

    GC.Collect()
    // Check that we're alive
    Assert.IsTrue(wr.IsAlive)
    
    // Dispose of us
    (box signal :?> IDisposable).Dispose()
    GC.Collect()
    Assert.IsFalse(wr.IsAlive)

