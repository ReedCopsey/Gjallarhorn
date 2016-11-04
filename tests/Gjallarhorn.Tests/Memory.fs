namespace Gjallarhorn.Tests

open Gjallarhorn
open Gjallarhorn.Internal

open System
open NUnit.Framework

module Memory =
    let mutable culture : System.Globalization.CultureInfo = null

    [<SetUp>]
    let setup () =
        culture <- System.Threading.Thread.CurrentThread.CurrentCulture
        System.Threading.Thread.CurrentThread.CurrentCulture <- System.Globalization.CultureInfo.InvariantCulture

    [<TearDown>]
    let teardown () =
        System.Threading.Thread.CurrentThread.CurrentCulture <- culture

    [<Test>]
    let ``Mutable\create doesn't cause tracking`` () =
        let value = Mutable.create 42

        Assert.AreEqual(false, SignalManager.IsTracked value)

    [<Test>]
    let ``Signal\subscribe then GC removes tracking`` () =
        let value = Mutable.create 42

        Assert.AreEqual(false, SignalManager.IsTracked value)

        let test() =
            use _view = Signal.Subscription.create (fun _ ->()) value
            Assert.AreEqual(true, SignalManager.IsTracked value)
            // purposely don't dispose
            let view2 = Signal.Subscription.create (fun _ ->()) value 
            Assert.AreEqual(true, SignalManager.IsTracked value)

        test()

        GC.Collect()
        GC.WaitForPendingFinalizers()

        Assert.AreEqual(false, SignalManager.IsTracked value)
    
    [<Test>]
    let ``Signal\subscribe causes tracking`` () =
        let value = Mutable.create 42
        Assert.AreEqual(false, SignalManager.IsTracked value)

        let view = Signal.map (fun v -> v.ToString()) value
        Assert.AreEqual(true, SignalManager.IsTracked value)
        Assert.AreEqual(false, SignalManager.IsTracked view)
        Assert.AreEqual(false, view.HasDependencies)

        use _sub = Signal.Subscription.create ignore view
        Assert.AreEqual(true, SignalManager.IsTracked value)
        Assert.AreEqual(false, SignalManager.IsTracked view)
        Assert.AreEqual(true, view.HasDependencies)

    [<Test>]
    let ``Signal\subscribe repeatedly updates tracking`` () =
        let value = Mutable.create 42
        Assert.AreEqual(false, SignalManager.IsTracked value)

        let view = Signal.map (fun v -> v.ToString()) value
        Assert.AreEqual(true, SignalManager.IsTracked value)
        Assert.AreEqual(false, SignalManager.IsTracked view)
        Assert.AreEqual(false, view.HasDependencies)

        do
            use _sub = Signal.Subscription.create ignore view
            Assert.AreEqual(true, SignalManager.IsTracked value)
            Assert.AreEqual(false, SignalManager.IsTracked view)
            Assert.AreEqual(true, view.HasDependencies)

        Assert.AreEqual(true, SignalManager.IsTracked value)
        Assert.AreEqual(false, SignalManager.IsTracked view)
        Assert.AreEqual(false, view.HasDependencies)

        do
            use _sub = Signal.Subscription.create ignore view
            Assert.AreEqual(true, SignalManager.IsTracked value)
            Assert.AreEqual(false, SignalManager.IsTracked view)
            Assert.AreEqual(true, view.HasDependencies)

        Assert.AreEqual(true, SignalManager.IsTracked value)
        Assert.AreEqual(false, SignalManager.IsTracked view)
        Assert.AreEqual(false, view.HasDependencies)

    [<Test>]
    let ``Signal\subscribe disposal stops tracking`` () =
        let value = Mutable.create 42
        let view = Signal.map (fun v -> v.ToString()) value

        Assert.AreEqual(true, SignalManager.IsTracked value)
        Assert.AreEqual(false, view.HasDependencies)

        let sub = Signal.Subscription.create ignore view
        Assert.AreEqual(true, SignalManager.IsTracked value)
        Assert.AreEqual(true, view.HasDependencies)

        sub.Dispose()
        Assert.AreEqual(true, SignalManager.IsTracked value)
        Assert.AreEqual(false, view.HasDependencies)

    [<Test>]
    let ``Source doesn't prevent view from being garbage collected`` () =
        let value = Mutable.create 42
        let mutable view = Some(Signal.map (fun v -> v.ToString()) value)

        Assert.AreEqual("42", view.Value.Value)
        let wr = WeakReference(view.Value)
        view <- None
        GC.Collect()
        Assert.AreEqual(false, wr.IsAlive)

    [<Test;TestCaseSource(typeof<Utilities>,"CasesStartEnd")>]
    let ``Signal\cache allows source to be garbage collected`` start finish =
        let mutable value = Some(Mutable.create start)
        // Note: Piping this instead of doing Some(...) causes this test to fail in *debug* builds
        // From what I can tell, the F# compiler keeps the steps in piping on the stack, which doesn't 
        // allow it to get cleaned up.  (Works in release either way)
        let mutable view = Some(Signal.map id value.Value)                        
     
        let cached = view.Value |> Signal.cache

        let valueWr = WeakReference(value.Value)
        let viewWr = WeakReference(view.Value)

        value.Value.Value <- finish
        view <- None    
        value <- None

        GC.Collect()
        GC.WaitForPendingFinalizers()

        Assert.IsFalse(valueWr.IsAlive)
        Assert.IsFalse(viewWr.IsAlive)

        Assert.AreEqual(box finish, cached.Value)

    [<Test;TestCaseSource(typeof<Utilities>,"CasesStartEndToStringPairs")>]
    let ``Signal\cache allows source and view to be garbage collected`` start _ finish finalView =
        let mutable value = Some(Mutable.create start)
        let mutable view = Some(Signal.map (fun v -> v.ToString()) value.Value)

        let cached = Signal.cache view.Value
    
        let wrValue = WeakReference(value.Value)
        let wrView = WeakReference(view.Value)
    
        value.Value.Value <- finish

        view <- None
        value <- None

        GC.Collect()

        Assert.AreEqual(false, wrValue.IsAlive)
        Assert.AreEqual(false, wrView.IsAlive)

        Assert.AreEqual(finalView, cached.Value)
