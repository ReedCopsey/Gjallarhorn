module Gjallarhorn.Bindable.Tests.BindingTarget

open Gjallarhorn
open Gjallarhorn.Bindable
open Gjallarhorn.Validation
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

let getProperty (target : obj) name =
    let props = TypeDescriptor.GetProperties target
    let prop = props.Find(name, false)
    unbox <| prop.GetValue(target)

type PropertyChangedObserver(o : INotifyPropertyChanged) =
    let changes = System.Collections.Generic.Dictionary<string,int>()

    do
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

    let obs = PropertyChangedObserver(bt)    

    let ibt = bt :> IBindingTarget

    ibt.RaisePropertyChanged("Test")
    ibt.RaisePropertyChanged("Test")
    
    Assert.AreEqual(2, obs.["Test"])

[<Test>]
let ``BindingTarget\TrackView tracks a view change`` () =
    use bt = new TestBindingTarget()

    let value = Mutable.create 0
    let obs = PropertyChangedObserver(bt)    

    let ibt = bt :> IBindingTarget

    ibt.TrackView "Test" value
    value.Value <- 1
    value.Value <- 2
    
    Assert.AreEqual(2, obs.["Test"])

[<Test>]
let ``BindingTarget\TrackView ignores view changes with same value`` () =
    use bt = new TestBindingTarget()

    let value = Mutable.create 0
    let obs = PropertyChangedObserver(bt)    

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
    
    let v = getProperty dvm "Test"
    Assert.AreEqual(42, v)

[<Test>]
let ``BindingTarget\BindMutable add then modify property value`` () =
    let v1 = Mutable.create 1
    let v2 = Mutable.create 2
    use dynamicVm = new DesktopBindingTarget()
    dynamicVm.BindMutable "Test" v1
    dynamicVm.BindMutable "Test2" v2
        
    let cur = getProperty dynamicVm "Test" 
    Assert.AreEqual(1, cur)

    v1.Value <- 55
    let cur = getProperty dynamicVm "Test" 
    Assert.AreEqual(55, cur)

    let cur = getProperty dynamicVm "Test2" 
    Assert.AreEqual(2, cur)

    v2.Value <- 29
    let cur = getProperty dynamicVm "Test2" 
    Assert.AreEqual(29, cur)

[<Test>]
let ``BindingTarget\BindMutable add then modify property value raises property changed`` () =
    let v1 = Mutable.create 1
    use dynamicVm = new DesktopBindingTarget()
    dynamicVm.BindMutable "Test" v1

    let obs = PropertyChangedObserver(dynamicVm)    
    
    let cur = getProperty dynamicVm "Test" 
    Assert.AreEqual(1, cur)

    v1.Value <- 55 // Change 1
    let cur = getProperty dynamicVm "Test" 
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

    let obs = PropertyChangedObserver(dynamicVm)    
    
    let cur = getProperty dynamicVm "Test" 
    Assert.AreEqual(2, cur)

    v1.Value <- 55 // Change 1
    let cur = getProperty dynamicVm "Test" 
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
    
    let fullValue() = getProperty dynamicVm "Full"

    Assert.AreEqual(" ", fullValue())

    first.Value <- "Foo"
    Assert.AreEqual("Foo ", fullValue())

    last.Value <- "Bar"
    Assert.AreEqual("Foo Bar", fullValue())

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

    let obs = PropertyChangedObserver(dynamicVm)    

    Assert.AreEqual(0, obs.["Full"])        

    first.Value <- "Foo"
    Assert.AreEqual(1, obs.["Full"])        
    Assert.AreEqual(1, obs.["First"])        

    last.Value <- "Bar"
    Assert.AreEqual(1, obs.["Last"])        
    Assert.AreEqual(2, obs.["Full"])

[<Test>]
let ``BindingTarget\Bind\edit with validator sets error state`` () =
    let first = Mutable.createValidated notNullOrWhitespace ""
    let last = Mutable.createValidated notNullOrWhitespace ""
    let full = View.map2 (fun f l -> f + " " + l) first last

    use dynamicVm = 
        Bind.create()
        |> Bind.edit "First" first
        |> Bind.edit "Last" last
        |> Bind.watch "Full" full

    let obs = PropertyChangedObserver(dynamicVm)    

    Assert.IsFalse(dynamicVm.IsValid)
    first.Value <- "Foo"
    Assert.IsFalse(dynamicVm.IsValid)
    Assert.AreEqual(0, obs.["IsValid"])        
    last.Value <- "Bar"
    Assert.IsTrue(dynamicVm.IsValid)
    Assert.AreEqual(1, obs.["IsValid"])        

let fullNameValidation (value : string) = 
    match System.String.IsNullOrWhiteSpace(value) with
    | true -> Some "Value must contain at least a first and last name"
    | false ->
        let words = value.Split([|' '|], System.StringSplitOptions.RemoveEmptyEntries)
        if words.Length >= 2 then
            None
        else
            Some "Value must contain at least a first and last name"

[<Test>]
let ``BindingTarget\Bind\watch with validator sets error state`` () =
    let first = Mutable.create ""
    let last = Mutable.create ""

    let full = 
        View.map2 (fun f l -> f + " " + l) first last
        |> View.validate (notNullOrWhitespace >> (custom fullNameValidation))

    use dynamicVm = 
        Bind.create()
        |> Bind.edit "First" first
        |> Bind.edit "Last" last
        |> Bind.watch "Full" full

    let obs = PropertyChangedObserver(dynamicVm)    

    Assert.IsFalse(dynamicVm.IsValid)
    Assert.AreEqual(0, obs.["IsValid"])        

    first.Value <- "Foo"
    Assert.IsFalse(dynamicVm.IsValid)
    Assert.AreEqual(0, obs.["IsValid"])        
    last.Value <- "Bar"
    Assert.IsTrue(dynamicVm.IsValid)
    Assert.AreEqual(1, obs.["IsValid"])        

[<Test>]
let ``BindingTarget\Bind\watch puts proper errors into INotifyDataErrorInfo`` () =
    let first = Mutable.create ""
    let last = Mutable.create ""

    let full = 
        View.map2 (fun f l -> f + " " + l) first last
        |> View.validate (notNullOrWhitespace >> fixErrors >> (custom fullNameValidation))

//    use dynamicVm = 
//        Bind.create()
//        |> Bind.edit "First" first
//        |> Bind.edit "Last" last
//        |> Bind.watch "Full" full

    use dynamicVm =
        Binding.create {            
            edit "First" first
            edit "Last" last
            watch "Full" full
        }

    let obs = PropertyChangedObserver(dynamicVm)    

    let errors () =
        let inde = dynamicVm :> INotifyDataErrorInfo
        inde.GetErrors("Full")
        |> Seq.cast<string>
        |> Seq.toArray
    
    Assert.IsFalse(dynamicVm.IsValid)
    Assert.AreEqual(0, obs.["IsValid"])        
    Assert.AreEqual(1, errors().Length)        
    Assert.AreEqual("Value cannot be null or empty.", errors().[0])        

    first.Value <- "Foo"
    Assert.IsFalse(dynamicVm.IsValid)
    Assert.AreEqual(0, obs.["IsValid"])        
    Assert.AreEqual(1, errors().Length)        
    Assert.AreEqual("Value must contain at least a first and last name", errors().[0])        

    last.Value <- "Bar"
    Assert.IsTrue(dynamicVm.IsValid)
    Assert.AreEqual(1, obs.["IsValid"])        
    Assert.AreEqual(0, errors().Length)        

    let cur = getProperty dynamicVm "Full" 
    Assert.AreEqual("Foo Bar", cur)

