namespace Gjallarhorn.Bindable.Wpf

open Gjallarhorn
open Gjallarhorn.Bindable
open Gjallarhorn.Bindable.Internal
open System
open System.Collections.Generic
open System.ComponentModel
open System.Reflection
open System.Windows.Input

[<assembly:System.Runtime.CompilerServices.InternalsVisibleTo("Gjallarhorn.Bindable.Tests")>]
do ()

type internal IPropertyBag =
    abstract member CustomProperties : Dictionary<string,PropertyDescriptor * IValueHolder>

type [<TypeDescriptionProvider(typeof<BindingTargetTypeDescriptorProvider>)>] internal DesktopBindingTarget<'b>() as self =
    inherit BindingTargetBase<'b>()    

    let customProps = Dictionary<string, PropertyDescriptor * IValueHolder>()

    let bt() =
        self :> IBindingTarget

    member private __.MakePD<'a> name = BindingTargetPropertyDescriptor<'a>(name) :> PropertyDescriptor
    
    override this.AddReadWriteProperty<'a> name (getter : unit -> 'a) (setter : 'a -> unit) =
        customProps.Add(name, (this.MakePD<'a> name, ValueHolder.readWrite getter setter))        
    override this.AddReadOnlyProperty<'a> name (getter : unit -> 'a) =
        customProps.Add(name, (this.MakePD<'a> name, ValueHolder.readOnly getter))   

    interface IPropertyBag with
        member __.CustomProperties = customProps

/// [omit]
/// Internal type used to allow dynamic binding targets to be generated.        
and BindingTargetTypeDescriptorProvider(parent) =
    inherit TypeDescriptionProvider(parent)

    let mutable td = null, null
    new() = BindingTargetTypeDescriptorProvider(TypeDescriptor.GetProvider(typedefof<DesktopBindingTarget<_>>))

    override __.GetTypeDescriptor(objType, inst) =
        match td with
        | desc, i when desc <> null && obj.ReferenceEquals(i, inst) ->
            desc
        | _ ->
            let parent = base.GetTypeDescriptor(objType, inst)
            let desc = BindingTargetTypeDescriptor(parent, inst :?> IPropertyBag) :> ICustomTypeDescriptor
            td <- desc, inst
            desc

and [<AllowNullLiteral>] internal BindingTargetTypeDescriptor(parent, inst : IPropertyBag) =
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

    override __.ComponentType = typeof<IPropertyBag>
    override __.PropertyType = typeof<'a>
    override __.Description = String.Empty
    override __.IsBrowsable = true
    override __.IsReadOnly = false
    override __.CanResetValue(o) = false
    override __.GetValue(comp) =
        match comp with
        | :? IPropertyBag as dvm ->
            let prop = dvm.CustomProperties.[name]
            let vh = snd prop
            vh.GetValue()
        | _ -> null
    override __.ResetValue(comp) = ()
    override __.SetValue(comp, v) =
        match comp with
        | :? IPropertyBag as dvm ->
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
    // Gets, and potentially installs, the WPF synchronization context
    let installAndGetSynchronizationContext () =
        match SynchronizationContext.Current with
        | null ->
            // Create our UI sync context, and install it:
            DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher)
            |> SynchronizationContext.SetSynchronizationContext
        | _ -> ()

        SynchronizationContext.Current

    let private creation (typ : System.Type) =
        let targetType = typedefof<Gjallarhorn.Bindable.Wpf.DesktopBindingTarget<_>>.MakeGenericType([|typ|])
        System.Activator.CreateInstance(targetType) 

    /// Installs WPF targets for binding into Gjallarhorn
    let install installSynchronizationContext =        
        Gjallarhorn.Bindable.Binding.Implementation.installCreationFunction (fun _ -> creation typeof<obj>) creation

        match installSynchronizationContext with
        | true -> installAndGetSynchronizationContext ()
        | false -> SynchronizationContext.Current
