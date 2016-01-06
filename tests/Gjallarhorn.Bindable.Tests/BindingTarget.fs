module Gjallarhorn.Bindable.Tests.BindingTarget

open Gjallarhorn
open Gjallarhorn.Bindable
open System.ComponentModel
open NUnit.Framework

type TestBindingTarget() =
    inherit BindingTargetBase()

    override __.BindMutable<'a> name (value : IMutatable<'a>) =
        ()
    override __.BindView<'a> name (view : IView<'a>) =
        ()
    override __.BindCommand name comm =
        ()

type PropertyChangedObserver() =
    let changes = System.Collections.Generic.Dictionary<string,int>()
    member __.Subscribe (o : INotifyPropertyChanged) =
        o.PropertyChanged.Add(fun args ->            
            let current = 
                match changes.ContainsKey args.PropertyName with
                | true -> changes.[args.PropertyName]
                | false -> 0
            changes.[args.PropertyName] <- current + 1)

    member __.Item 
        with get(name) = 
            let success, count = changes.TryGetValue name
            match success with
            | true -> count
            | false -> 0

[<Test>]
let ``BindingTarget raises property changed`` () =
    use bt = new TestBindingTarget()

    let obs = PropertyChangedObserver()
    obs.Subscribe bt

    let ibt = bt :> IBindingTarget

    ibt.RaisePropertyChanged("Test")
    ibt.RaisePropertyChanged("Test")
    
    Assert.AreEqual(2, obs.["Test"])

[<Test>]
let ``BindingTarget\TrackView tracks a view change`` () =
    use bt = new TestBindingTarget()

    let value = Mutable.create 0
    let obs = PropertyChangedObserver()
    obs.Subscribe bt

    let ibt = bt :> IBindingTarget

    ibt.TrackView "Test" value
    value.Value <- 1
    value.Value <- 2
    
    Assert.AreEqual(2, obs.["Test"])

[<Test>]
let ``BindingTarget\TrackView ignores view changes with same value`` () =
    use bt = new TestBindingTarget()

    let value = Mutable.create 0
    let obs = PropertyChangedObserver()
    obs.Subscribe bt

    let ibt = bt :> IBindingTarget

    ibt.TrackView "Test" value
    value.Value <- 1
    value.Value <- 2
    value.Value <- 2
    
    Assert.AreEqual(2, obs.["Test"])

[<Test>]
let ``BindingTarget\BindMutable does not throw`` () =
    Assert.DoesNotThrow(fun _ -> 
        use dvm = new DesktopBindingTarget()
        let v = Mutable.create 42
        dvm.BindMutable "Test" v
    )

[<Test>]
let ``BindingTarget\BindMutable add then read property works`` () =
    use dvm = new DesktopBindingTarget()
    let v = Mutable.create 42
    dvm.BindMutable "Test" v
    
    let props = TypeDescriptor.GetProperties(dvm)
    let prop = props.Find("Test", false)

    Assert.IsNotNull(prop)

[<Test>]
let ``BindingTarget\BindMutable add then read property value`` () =
    use dvm = new DesktopBindingTarget()
    let v = Mutable.create 42
    dvm.BindMutable "Test" v
    
    let props = TypeDescriptor.GetProperties(dvm)
    let prop = props.Find("Test", false)

    let v = unbox <| prop.GetValue(dvm)
    Assert.AreEqual(42, v)

[<Test>]
let ``BindingTarget\BindMutable add then modify property value`` () =
    let v1 = Mutable.create 1
    let v2 = Mutable.create 2
    use dynamicVm = new DesktopBindingTarget()
    dynamicVm.BindMutable "Test" v1
    dynamicVm.BindMutable "Test2" v2
    
    let props = TypeDescriptor.GetProperties(dynamicVm)
    let prop = props.Find("Test", false)

    let cur = unbox <| prop.GetValue(dynamicVm)
    Assert.AreEqual(1, cur)

    v1.Value <- 55
    let cur = unbox <| prop.GetValue(dynamicVm)
    Assert.AreEqual(55, cur)

    let prop = props.Find("Test2", false)

    let cur = unbox <| prop.GetValue(dynamicVm)
    Assert.AreEqual(2, cur)

    v2.Value <- 29
    let cur = unbox <| prop.GetValue(dynamicVm)
    Assert.AreEqual(29, cur)

[<Test>]
let ``BindingTarget\BindMutable add then modify property value raises property changed`` () =
    let v1 = Mutable.create 1
    use dynamicVm = new DesktopBindingTarget()
    dynamicVm.BindMutable "Test" v1

    let obs = PropertyChangedObserver()
    obs.Subscribe dynamicVm
    
    let props = TypeDescriptor.GetProperties(dynamicVm)
    let prop = props.Find("Test", false)

    let cur = unbox <| prop.GetValue(dynamicVm)
    Assert.AreEqual(1, cur)

    v1.Value <- 55 // Change 1
    let cur = unbox <| prop.GetValue(dynamicVm)
    Assert.AreEqual(55, cur)

    v1.Value <- 66 // Change 2
    v1.Value <- 66 // No Change  - Value the same
    v1.Value <- 77 // Change 3
    Assert.AreEqual(3, obs.["Test"])

[<Test>]
let ``BindingTarget\BindView raises property changed`` () =
    let v1 = Mutable.create 1
    let v2 = View.map (fun i -> i+1) v1
    use dynamicVm = new DesktopBindingTarget()
    dynamicVm.BindView "Test" v2

    let obs = PropertyChangedObserver()
    obs.Subscribe dynamicVm
    
    let props = TypeDescriptor.GetProperties(dynamicVm)
    let prop = props.Find("Test", false)

    let cur = unbox <| prop.GetValue(dynamicVm)
    Assert.AreEqual(2, cur)

    v1.Value <- 55 // Change 1
    let cur = unbox <| prop.GetValue(dynamicVm)
    Assert.AreEqual(56, cur)

    v1.Value <- 66 // Change 2
    v1.Value <- 66 // No Change  - Value the same
    v1.Value <- 77 // Change 3
    Assert.AreEqual(3, obs.["Test"])

[<Test>]
let ``BindingTarget\BindView tracks values properly`` () =
    let first = Mutable.create ""
    let last = Mutable.create ""
    let full = View.map2 (fun f l -> f + " " + l) first last

    use dynamicVm = 
        Bind.create()
        |> Bind.edit "First" first
        |> Bind.edit "Last" last
        |> Bind.watch "Full" full

    let props = TypeDescriptor.GetProperties(dynamicVm)
    let prop = props.Find("Full", false)

    let cur = unbox <| prop.GetValue(dynamicVm)
    Assert.AreEqual(" ", cur)

    first.Value <- "Foo"
    let cur = unbox <| prop.GetValue(dynamicVm)
    Assert.AreEqual("Foo ", cur)

    last.Value <- "Bar"
    let cur = unbox <| prop.GetValue(dynamicVm)
    Assert.AreEqual("Foo Bar", cur)

[<Test>]
let ``BindingTarget\BindView raises property changed appropriately`` () =
    let first = Mutable.create ""
    let last = Mutable.create ""
    let full = View.map2 (fun f l -> f + " " + l) first last

    use dynamicVm = 
        Bind.create()
        |> Bind.edit "First" first
        |> Bind.edit "Last" last
        |> Bind.watch "Full" full

    let obs = PropertyChangedObserver()
    obs.Subscribe dynamicVm

    Assert.AreEqual(0, obs.["Full"])        

    first.Value <- "Foo"
    Assert.AreEqual(1, obs.["Full"])        
    Assert.AreEqual(1, obs.["First"])        

    last.Value <- "Bar"
    Assert.AreEqual(1, obs.["Last"])        
    Assert.AreEqual(2, obs.["Full"])
