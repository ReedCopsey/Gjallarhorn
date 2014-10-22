namespace Gjallarhorn

/// Provides mechanisms for working with IView<'a> views
module View =
    let create provider mapping = View(provider, mapping) :> IView<_>
    let createCached provider mapping = WeakView(provider, mapping) :> IView<_>
