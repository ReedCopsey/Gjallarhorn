namespace Gjallarhorn.Bindable.Wpf

open Gjallarhorn
open Gjallarhorn.Bindable
open System
open System.Collections.Generic
open System.ComponentModel
open System.Reflection
open System.Windows.Input

[<assembly:System.Runtime.CompilerServices.InternalsVisibleTo("Gjallarhorn.Bindable.Tests")>]
do ()

type internal IValueHolder =
    abstract member GetValue : unit -> obj
    abstract member SetValue : obj -> unit

type [<TypeDescriptionProvider(typeof<BindingTargetTypeDescriptorProvider>)>] internal DesktopBindingTarget() as self =
    inherit BindingTargetBase()

    let customProps = Dictionary<string, PropertyDescriptor * IValueHolder>()

    let bt() =
        self :> IBindingTarget

    let makePD name = BindingTargetPropertyDescriptor(name) :> PropertyDescriptor

    let makeEditIV (prop : IMutatable<'a>) = 
        { 
            new IValueHolder with 
                member __.GetValue() = box prop.Value
                member __.SetValue(v) = prop.Value <- unbox v         
        }
    let makeSignalIV (prop : ISignal<'a>) = 
        { 
            new IValueHolder with 
                member __.GetValue() = box prop.Value
                member __.SetValue(v) = ()
        }
    let makeCommandIV (prop : ICommand) = 
        { 
            new IValueHolder with 
                member __.GetValue() = box prop
                member __.SetValue(_) = ()
        }

    member internal __.CustomProperties = customProps
    
    override this.AddReadWriteProperty<'a> name signal =
        let editSource = Mutable.create signal.Value
        Signal.copyTo editSource signal
        |> this.TrackDisposable
        customProps.Add(name, (makePD name, makeEditIV editSource))
        editSource :> ISignal<'a>
    override __.AddReadOnlyProperty<'a> name (signal : ISignal<'a>) =
        customProps.Add(name, (makePD name, makeSignalIV signal))   

    override __.AddCommand name command =        
        customProps.Add(name, (makePD name, makeCommandIV command))

/// [omit]
/// Internal type used to allow dynamic binding targets to be generated.        
and BindingTargetTypeDescriptorProvider(parent) =
    inherit TypeDescriptionProvider(parent)

    let mutable td = null, null
    new() = BindingTargetTypeDescriptorProvider(TypeDescriptor.GetProvider(typeof<DesktopBindingTarget>))

    override __.GetTypeDescriptor(objType, inst) =
        match td with
        | desc, i when desc <> null && obj.ReferenceEquals(i, inst) ->
            desc
        | _ ->
            let parent = base.GetTypeDescriptor(objType, inst)
            let desc = BindingTargetTypeDescriptor(parent, inst :?> DesktopBindingTarget) :> ICustomTypeDescriptor
            td <- desc, inst
            desc

and [<AllowNullLiteral>] internal BindingTargetTypeDescriptor(parent, inst : DesktopBindingTarget) =
    inherit CustomTypeDescriptor(parent)

    override __.GetProperties() =
        let newProps = 
            inst.CustomProperties.Values
            |> Seq.map fst
        let props = 
            base.GetProperties()
            |> Seq.cast<PropertyDescriptor>
            |> Seq.append newProps
            |> Array.ofSeq
        PropertyDescriptorCollection(props)

and internal BindingTargetPropertyDescriptor<'a>(name : string) =
    inherit PropertyDescriptor(name, [| |])

    override __.ComponentType = typeof<DesktopBindingTarget>
    override __.PropertyType = typeof<'a>
    override __.Description = String.Empty
    override __.IsBrowsable = true
    override __.IsReadOnly = false
    override __.CanResetValue(o) = false
    override __.GetValue(comp) =
        match comp with
        | :? DesktopBindingTarget as dvm ->
            let prop = dvm.CustomProperties.[name]
            let vh = snd prop
            vh.GetValue()
        | _ -> null
    override __.ResetValue(comp) = ()
    override __.SetValue(comp, v) =
        match comp with
        | :? DesktopBindingTarget as dvm ->
            let prop = dvm.CustomProperties.[name]
            let vh = snd prop
            vh.SetValue(v)
        | _ -> ()
    override __.ShouldSerializeValue(c) = false

namespace Gjallarhorn

open System.Threading
open System.Windows.Threading

/// Platform installation
module Wpf =
    /// Installs WPF targets for binding into Gjallarhorn
    let install () =
        Gjallarhorn.Bindable.Bind.Internal.installCreationFunction (fun _ -> new Gjallarhorn.Bindable.Wpf.DesktopBindingTarget() :> Gjallarhorn.Bindable.IBindingTarget)

    // Gets, and potentially installs, the WPF synchronization context
    let installAndGetSynchronizationContext () =
        if SynchronizationContext.Current = null then
            // Create our UI sync context, and install it:
            DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher)
            |> SynchronizationContext.SetSynchronizationContext

        SynchronizationContext.Current
