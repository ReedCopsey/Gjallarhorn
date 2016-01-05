namespace Gjallarhorn.Tests

open Gjallarhorn
open Gjallarhorn.Internal

open System
open NUnit.Framework

module Memory =
    [<Test>]
    let ``Mutable\create doesn't cause tracking`` () =
        let value = Mutable.create 42

        Assert.AreEqual(false, SignalManager.IsTracked value)
    
    [<Test>]
    let ``View\map causes tracking`` () =
        let value = Mutable.create 42
        let view = View.map (fun v -> v.ToString()) value

        Assert.AreEqual(true, SignalManager.IsTracked value)
        Assert.AreEqual(false, SignalManager.IsTracked view)

    [<Test>]
    let ``View disposal stops tracking`` () =
        let value = Mutable.create 42
        let view = View.map (fun v -> v.ToString()) value 

        Assert.AreEqual(true, SignalManager.IsTracked value)
        Assert.AreEqual(false, SignalManager.IsTracked view)

        view.Dispose()
        Assert.AreEqual(false, SignalManager.IsTracked value)

    [<Test>]
    let ``Source doesn't prevent view from being garbage collected`` () =
        let value = Mutable.create 42
        let mutable view = Some(View.map (fun v -> v.ToString()) value)

        Assert.AreEqual("42", view.Value.Value)
        let wr = WeakReference(view.Value)
        view <- None
        GC.Collect()
        Assert.AreEqual(false, wr.IsAlive)

    [<Test;TestCaseSource(typeof<Utilities>,"CasesStartEnd")>]
    let ``View\cache allows source to be garbage collected`` start finish =
        let mutable value = Some(Mutable.create start)

        let cached = View.cache value.Value

        Assert.AreEqual(true, SignalManager.IsTracked value.Value)
    
        let wrValue = WeakReference(value.Value)
    
        value.Value.Value <- finish

        value <- None

        GC.Collect()

        Assert.AreEqual(false, wrValue.IsAlive)

        Assert.AreEqual(box finish, cached.Value)

    [<Test;TestCaseSource(typeof<Utilities>,"CasesStartEndToStringPairs")>]
    let ``View\cache allows source and view to be garbage collected`` start _ finish finalView =
        let mutable value = Some(Mutable.create start)
        let mutable view = Some(View.map (fun v -> v.ToString()) value.Value)

        let cached = View.cache view.Value

        Assert.AreEqual(true, SignalManager.IsTracked value.Value)
        Assert.AreEqual(true, SignalManager.IsTracked view.Value)
    
        let wrValue = WeakReference(value.Value)
        let wrView = WeakReference(view.Value)
    
        value.Value.Value <- finish

        view <- None
        value <- None

        GC.Collect()

        Assert.AreEqual(false, wrValue.IsAlive)
        Assert.AreEqual(false, wrView.IsAlive)

        Assert.AreEqual(finalView, cached.Value)
