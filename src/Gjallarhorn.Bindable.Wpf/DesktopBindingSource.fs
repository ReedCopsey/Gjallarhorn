namespace Gjallarhorn.Wpf

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

type [<TypeDescriptionProvider(typeof<BindingSourceTypeDescriptorProvider>)>] internal DesktopBindingSource<'b>() as self =
    inherit ObservableBindingSource<'b>()    

    let customProps = Dictionary<string, PropertyDescriptor * IValueHolder>()

    member private __.MakePD<'a> name = BindingSourcePropertyDescriptor<'a>(name) :> PropertyDescriptor
    
    override this.AddReadWriteProperty<'a> name (getter : unit -> 'a) (setter : 'a -> unit) =
        if customProps.ContainsKey name then
            failwith <| sprintf "Property [%s] already exists on this binding source" name
        customProps.Add(name, (this.MakePD<'a> name, ValueHolder.readWrite getter setter))        
    override this.AddReadOnlyProperty<'a> name (getter : unit -> 'a) =
        if customProps.ContainsKey name then
            failwith <| sprintf "Property [%s] already exists on this binding source" name
        customProps.Add(name, (this.MakePD<'a> name, ValueHolder.readOnly getter))   

    interface IPropertyBag with
        member __.CustomProperties = customProps

/// [omit]
/// Internal type used to allow dynamic binding sources to be generated.        
and internal BindingSourceTypeDescriptorProvider(parent) =
    inherit TypeDescriptionProvider(parent)

    let mutable td = null, null
    new() = BindingSourceTypeDescriptorProvider(TypeDescriptor.GetProvider(typedefof<DesktopBindingSource<_>>))

    override __.GetTypeDescriptor(objType, inst) =
        match td with
        | desc, i when desc <> null && obj.ReferenceEquals(i, inst) ->
            desc
        | _ ->
            let parent = base.GetTypeDescriptor(objType, inst)
            let desc = BindingSourceTypeDescriptor(parent, inst :?> IPropertyBag) :> ICustomTypeDescriptor
            td <- desc, inst
            desc

and [<AllowNullLiteral>] internal BindingSourceTypeDescriptor(parent, inst : IPropertyBag) =
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

and internal BindingSourcePropertyDescriptor<'a>(name : string) =
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
