namespace Gjallarhorn.XamarinForms

open Gjallarhorn
open Gjallarhorn.Bindable
open Gjallarhorn.Bindable.FSharp
open Gjallarhorn.Bindable.Internal

open System
open System.Reflection

type internal DynParameterInfo(memberInfo, t, name) =
    inherit ParameterInfo()

    override __.ParameterType = t
    override __.Member = memberInfo
    override __.Name = name

type internal DynPropertyMethodInfo(pi:PropertyInfo, setter) =
    inherit MethodInfo()

    override __.Name = pi.Name
    override __.DeclaringType = pi.DeclaringType
    override __.ReflectedType = pi.ReflectedType
    override __.Attributes = MethodAttributes.Public
    override __.ReturnTypeCustomAttributes = null
    override __.GetCustomAttributes i = [| |]
    override __.GetCustomAttributes (rt, i) = [| |]
    override __.IsDefined (t, i) = false
    override __.GetBaseDefinition() = null
    override __.GetParameters() = 
        match setter with
        | true ->  [| DynParameterInfo(pi, pi.DeclaringType, "this") ; DynParameterInfo(pi, pi.PropertyType, "value") |]
        | false -> [| DynParameterInfo(pi, pi.PropertyType, "value") |]
    override __.ReturnType = 
        match setter with
        | true -> typeof<System.Void>
        | false -> pi.PropertyType
    override __.Invoke(o, bf, b, p, c) = 
        match setter with
        | true -> 
            pi.SetValue(o, p.[0])
            null
        | false -> 
            pi.GetValue(0)
    override __.GetMethodImplementationFlags() = MethodImplAttributes.IL
    override __.MethodHandle = raise <| System.NotImplementedException()



type internal DynPropertyInfo(owner, name, t, valueHolder : IValueHolder) as self =
    inherit PropertyInfo()

    let getter = DynPropertyMethodInfo(self, false) :> MethodInfo
    let setter = DynPropertyMethodInfo(self, true) :> MethodInfo

    override __.CanRead = true
    override __.CanWrite = not valueHolder.ReadOnly
    override __.PropertyType = t
    override __.DeclaringType = owner
    override __.ReflectedType = owner
    override __.Name = name
    override __.GetValue(o, ia, b, index, culture) =
        valueHolder.GetValue()
    override __.SetValue(o, v, ia, b, index, culture) =
        valueHolder.SetValue v

    override __.GetGetMethod np = getter
    override __.GetSetMethod np = setter
    override __.GetAccessors nonPublic = [| getter ; setter |]
    override __.GetIndexParameters () = [| |]
    override __.Attributes = PropertyAttributes.None
    override __.GetCustomAttributes inh = [| |]
    override __.GetCustomAttributes (t, inh) = [| |]
    override __.IsDefined (t,i) = false

type internal DynTypeInfo (ownerType, getProp) =
    inherit TypeDelegator(ownerType)
    
    override __.GetDeclaredProperty name = 
        match getProp name with
        | Some p -> p
        | None -> base.GetDeclaredProperty(name)
    
type internal RefTypeBindingTarget<'b>() =
    inherit ObservableBindingSource<'b>()

    let ownerType = typeof<ObservableBindingSource<'b>>
    let properties = System.Collections.Generic.Dictionary<string, PropertyInfo>()

    let getProperty name =
        match properties.TryGetValue name with
        | false, _ -> None
        | true, v -> Some v

    override __.AddReadWriteProperty<'a> name (getter : unit -> 'a) (setter : 'a -> unit) =
        let vh = ValueHolder.readWrite getter setter
        let p = DynPropertyInfo(ownerType, name, typeof<'a>, vh)

        if properties.ContainsKey name then
            failwith <| sprintf "Property [%s] already exists on this binding source" name
        properties.Add(name, p)

    override __.AddReadOnlyProperty<'a> name (getter : unit -> 'a) =
        let vh = ValueHolder.readOnly getter 
        let p = DynPropertyInfo(ownerType, name, typeof<'a>, vh)

        if properties.ContainsKey name then
            failwith <| sprintf "Property [%s] already exists on this binding source" name
        properties.Add(name, p)
    
    interface IReflectableType with 
        member __.GetTypeInfo() = DynTypeInfo(ownerType, getProperty) :> _

