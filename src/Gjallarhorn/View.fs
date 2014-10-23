namespace Gjallarhorn

/// Provides mechanisms for working with IView<'a> views
module View =
    
    /// Create a view over a provider given a specific mapping function
    let create provider mapping = View(provider, mapping) :> IView<_>
    
    /// <summary>Create a cached view over a provider</summary>
    /// <remarks>
    /// This will not hold a reference to the provider, and will allow it to be garbage collected.
    /// As such, it caches the "last valid" state of the view locally.
    /// </remarks>
    let createCached provider = ViewCache(provider) :> IView<_>
