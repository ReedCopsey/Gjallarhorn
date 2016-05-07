using System;
using Gjallarhorn.Bindable;

namespace ViewModels
{
    public static class VM
    {
        public static IBindingSubject<NameModel> CreateMainViewModel(IObservable<NameModel> nameIn, NameModel initialValue)
        {
            var subject = BindingSubject.create<NameModel>();            
            var source = subject.ObservableToSignal(initialValue, nameIn);

            return subject;
        }
    }
}
