namespace Gjallarhorn.Bindable

open Gjallarhorn
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
    let makeViewIV (prop : IView<'a>) = 
        { 
            new IValueHolder with 
                member __.GetValue() = box prop.Value
                member __.SetValue(v) = ()
        }
    let makeCommandIV (prop : ICommand) = 
        { 
            new IValueHolder with 
                member __.GetValue() = box prop
                member __.SetValue(v) = ()
        }

    member internal __.CustomProperties = customProps
    
    override __.BindMutable<'a> name (value : IMutatable<'a>) =        
        (bt()).TrackView name value
        customProps.Add(name, (makePD name, makeEditIV value))

        match value with
        | :? Validation.IValidatedMutatable<'a> as validator ->
            (bt()).TrackValidator name validator.ValidationResult
        | _ -> ()

    override __.BindView<'a> name (view : IView<'a>) =        
        (bt()).TrackView name view
        customProps.Add(name, (makePD name, makeViewIV view))   

        match view with
        | :? Validation.IValidatedView<'a> as validator ->
            (bt()).TrackValidator name validator.ValidationResult
        | _ -> ()

    override __.BindCommand name command =        
        customProps.Add(name, (makePD name, makeCommandIV command))
        
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


module Bind =
    let create () = new DesktopBindingTarget() :> IBindingTarget

module Binding =        
    let create = Binding.BindingBuilder(Bind.create)
