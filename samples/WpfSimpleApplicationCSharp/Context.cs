using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gjallarhorn;
using Gjallarhorn.Bindable;

namespace WpfSimpleApplicationCSharp
{
    internal static class Context
    {
        public static BindingSource CreateBindingSource()
        {
            var source = BindingModule.createSource();

            return source;
        }
    }
}
