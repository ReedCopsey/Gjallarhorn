namespace Gjallarhorn.Bindable

open Gjallarhorn
open System
open System.Collections.Generic
open System.ComponentModel
open System.Reflection
open System.Windows.Input

type internal IValueHolder =
    abstract member GetValue : unit -> obj
    abstract member SetValue : obj -> unit

type internal ValueHolder<'a>(value : IMutatable<'a>) =
    interface IValueHolder with
        member __.GetValue() = box value.Value
        member __.SetValue(v) = value.Value <- unbox v         

type internal ViewHolder<'a>(value : IView<'a>) =
    interface IValueHolder with
        member __.GetValue() = box value.Value
        member __.SetValue(v) = ()


type [<TypeDescriptionProvider(typeof<BindingTargetTypeDescriptorProvider>)>] BindingTarget() as self =
    inherit BindingTargetBase()

    let customProps = Dictionary<string, PropertyDescriptor * IValueHolder>()

    let bt() =
        self :> IBindingTarget

    let makePD name = BindingTargetPropertyDescriptor(name) :> PropertyDescriptor

    let makeEditIV prop = ValueHolder(prop) :> IValueHolder
    let makeViewIV prop = ViewHolder(prop) :> IValueHolder

    member internal __.CustomProperties = customProps
    
    override this.BindMutable<'a> name (value : IMutatable<'a>) =        
        (bt()).TrackView name value
        customProps.Add(name, (makePD name, makeEditIV value))    

    override this.BindView<'a> name (view : IView<'a>) =        
        (bt()).TrackView name view
        customProps.Add(name, (makePD name, makeViewIV view))   

    override this.BindCommand name command =
        let view = View.constant command
        customProps.Add(name, (makePD name, makeViewIV view))
        
and BindingTargetTypeDescriptorProvider(parent) =
    inherit TypeDescriptionProvider(parent)

    let mutable td = null, null
    new() = BindingTargetTypeDescriptorProvider(TypeDescriptor.GetProvider(typeof<BindingTarget>))

    override __.GetTypeDescriptor(objType, inst) =
        match td with
        | desc, i when desc <> null && obj.ReferenceEquals(i, inst) ->
            desc
        | _ ->
            let parent = base.GetTypeDescriptor(objType, inst)
            let desc = BindingTargetTypeDescriptor(parent, inst :?> BindingTarget) :> ICustomTypeDescriptor
            td <- desc, inst
            desc

and [<AllowNullLiteral>] internal BindingTargetTypeDescriptor(parent, inst : BindingTarget) =
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

    override __.ComponentType = typeof<BindingTarget>
    override __.PropertyType = typeof<'a>
    override __.Description = String.Empty
    override __.IsBrowsable = true
    override __.IsReadOnly = false
    override __.CanResetValue(o) = false
    override __.GetValue(comp) =
        match comp with
        | :? BindingTarget as dvm ->
            let prop = dvm.CustomProperties.[name]
            let vh = snd prop
            vh.GetValue()
        | _ -> null
    override __.ResetValue(comp) = ()
    override __.SetValue(comp, v) =
        match comp with
        | :? BindingTarget as dvm ->
            let prop = dvm.CustomProperties.[name]
            let vh = snd prop
            vh.SetValue(v)
        | _ -> ()
    override __.ShouldSerializeValue(c) = false