namespace Gjallarhorn

/// Additional functions related to Observable for use with Gjallarhorn
module Observable =
    /// Filters the input observable by using a separate bool signal. The value of the signal is used as the filtering predicate
    let filterBy (condition : ISignal<bool>) input =
        input
        |> Observable.filter (fun _ -> condition.Value)
