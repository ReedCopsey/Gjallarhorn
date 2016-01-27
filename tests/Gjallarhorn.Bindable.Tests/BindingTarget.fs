namespace Gjallarhorn.Bindable.Tests

open Gjallarhorn
open Gjallarhorn.Bindable
open Gjallarhorn.Bindable.Wpf
open Gjallarhorn.Validation
open System.ComponentModel
open NUnit.Framework
open Bind

type TestBindingTarget() =
    inherit BindingTargetBase()

    override __.AddReadWriteProperty<'a> name (value : IView<'a>) =
        View.map id value
    override __.AddReadOnlyProperty<'a> name (view : IView<'a>) =
        ()
    override __.AddCommand name comm =
        ()
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

[<TestFixture>]
type BindingTarget() =


    let getProperty (target : obj) name =
        let props = TypeDescriptor.GetProperties target
        let prop = props.Find(name, false)
        unbox <| prop.GetValue(target)

    let setProperty (target : obj) name value =
        let props = TypeDescriptor.GetProperties target
        let prop = props.Find(name, false)
        prop.SetValue(target, box value)

    let fullNameValidation (value : string) = 
        match System.String.IsNullOrWhiteSpace(value) with
        | true -> Some "Value must contain at least a first and last name"
        | false ->
            let words = value.Split([|' '|], System.StringSplitOptions.RemoveEmptyEntries)
            if words.Length >= 2 then
                None
            else
                Some "Value must contain at least a first and last name"

    [<TestFixtureSetUp>]
    member __.Initialize() =
        Gjallarhorn.Wpf.install()

    [<Test>]
    member __.``BindingTarget raises property changed`` () =
        use bt = new TestBindingTarget()

        let obs = PropertyChangedObserver(bt)    

        let ibt = bt :> IBindingTarget

        ibt.RaisePropertyChanged("Test")
        ibt.RaisePropertyChanged("Test")
    
        Assert.AreEqual(2, obs.["Test"])

    [<Test>]
    member __.``BindingTarget\TrackView tracks a view change`` () =
        use bt = new TestBindingTarget()

        let value = Mutable.create 0
        let obs = PropertyChangedObserver(bt)    

        let ibt = bt :> IBindingTarget

        ibt.TrackView "Test" value
        value.Value <- 1
        value.Value <- 2
    
        Assert.AreEqual(2, obs.["Test"])

    [<Test>]
    member __.``BindingTarget\TrackView ignores view changes with same value`` () =
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
    member __.``BindingTarget\BindView raises property changed`` () =
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
    member __.``BindingTarget\BindView tracks values properly`` () =
        let first = Mutable.create ""
        let last = Mutable.create ""
        let full = View.map2 (fun f l -> f + " " + l) first last

        use dynamicVm = 
            Bind.create()
            |> Bind.watch "Full" full
    
        let fullValue() = getProperty dynamicVm "Full"

        Assert.AreEqual(" ", fullValue())

        first.Value <- "Foo"
        Assert.AreEqual("Foo ", fullValue())

        last.Value <- "Bar"
        Assert.AreEqual("Foo Bar", fullValue())

    [<Test>]
    member __.``BindingTarget\BindView raises property changed appropriately`` () =
        let first = Mutable.create ""
        let last = Mutable.create ""
        let full = View.map2 (fun f l -> f + " " + l) first last

        use dynamicVm = 
            Bind.create()
            |> Bind.watch "First" first
            |> Bind.watch "Last" last
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
    member __.``BindingTarget\Bind\edit with validator sets error state`` () =
        let first = Mutable.create ""
           
        let last = Mutable.create ""
        let full = View.map2 (fun f l -> f + " " + l) first last


        use dynamicVm = 
            Bind.create()
            |> Bind.watch "Full" full

        let first' = dynamicVm.BindEditor "First" notNullOrWhitespace first
        let last' = dynamicVm.BindEditor "Last" notNullOrWhitespace last            
            

        let obs = PropertyChangedObserver(dynamicVm)    

        Assert.IsFalse(dynamicVm.IsValid)
        first.Value <- "Foo"
        Assert.IsFalse(dynamicVm.IsValid)
        Assert.AreEqual(0, obs.["IsValid"])        
        last.Value <- "Bar"
        Assert.IsTrue(dynamicVm.IsValid)
        Assert.AreEqual(1, obs.["IsValid"])        

    [<Test>]
    member __.``BindingTarget\Bind\watch with validator sets error state`` () =
        let first = Mutable.create ""
        let last = Mutable.create ""

        let full = 
            View.map2 (fun f l -> f + " " + l) first last
            |> View.validate (notNullOrWhitespace >> (custom fullNameValidation))

        use dynamicVm = 
            Bind.create()
            |> Bind.watch "First" first
            |> Bind.watch "Last" last
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
    member __.``BindingTarget\Bind\watch puts proper errors into INotifyDataErrorInfo`` () =
        let first = Mutable.create ""
        let last = Mutable.create ""
        let full = 
            View.map2 (fun f l -> f + " " + l) first last
            |> View.validate (notNullOrWhitespace >> fixErrors >> (custom fullNameValidation))

        use dynamicVm =
            binding {            
                watch "First" first
                watch "Last" last            
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

