#### 1.2.0-beta1 - Feb 10th 2018
* Add .NetStandard2.0 Build and Package (Pre-release)

#### 1.1.0 - Sept 13th 2017
* Added IO.Direct for direct mapping of a signal into IO channels

#### 1.0.1 - Sept 7th 2017
* Removed explicit FSharp.Core version dependency to allow 4.1 to be used in clients

#### 1.0.0 - Sept 5th 2017
* Official 1.0 release of Gjallarhorn
* Split Bindable into separate repository

#### 1.0.0-beta1 - Sept 4th 2017
* **Significant** breaking changes introduced to Bindable API
* Created new, default API which is type safe and eliminates magic strings
* Preferred, new API for binding in Bind module
* Moved Binding to Bind.Explicit module
* Moved CollectionBinding to Bind.Collection module

#### 0.11.0 - August 25th 2017
* Addressed issue with bi-directional mutable bindings
* Got build working propertly in VS 2017

#### 0.0.10-beta - June 9th 2017
* Fixed issue with missing assembly level attribute for extension methods

#### 0.0.9-beta - February 3rd 2017
* Fixed issue with dependency tracking of IObservable<'a>

#### 0.0.8-beta - December 14th 2016
* Added new thread safe and async mutable interfaces and core types
* Added generator functions in Mutable module for async and threadsafe mutables
* Renamed State to AsyncMutable, cleaned up API

#### 0.0.7-beta - December 12th 2016
* Added new Signal.toFunction and Signal.mapFunction members
* Added ObservableSource<'a> to ease working with observables
* Improved samples
 
#### 0.0.6-beta - December 8th 2016
* Improved collection handling performance via internal optimizations
 
#### 0.0.5-beta - December 2nd 2016
* Reworked notification plumbing to be less surprising
* Added initial framework for building applications directly
* Added component model to framework

#### 0.0.4-beta - June 13th 2016
* Overhauled binding API to make things more clear.

#### 0.0.3-beta - June 1st 2016
* Corrected issue where edit bindings were being created as typed ISignal<obj>, not ISignal<'a>

#### 0.0.2-beta - May 18th 2016
* Corrected potential blocking condition in SignalManager with Mutable.create
* Converted commands to be IObservable<'a> but not ISignal<'a>.  This fits better with the conceptual notion of a command.
 
#### 0.0.1-beta - May 1th 2016
* Initial publish 
* API will still change
