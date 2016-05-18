#### 0.0.1-beta - May 1th 2016
* Initial publish 
* API will still change

#### 0.0.2-beta - May 18th 2016
* Corrected potential blocking condition in SignalManager with Mutable.create
* Converted commands to be IObservable<'a> but not ISignal<'a>.  This fits better with the conceptual notion of a command.

