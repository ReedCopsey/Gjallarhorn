using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Gjallarhorn;
using Gjallarhorn.Linq;

namespace Gjallarhorn.Linq.Tests
{
    public class SignalLinq
    {
        System.Globalization.CultureInfo culture = null;

        [SetUp]
        public void Setup()
        {
            culture = System.Threading.Thread.CurrentThread.CurrentCulture;
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        }

        [TearDown]
        public void Teardown()
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = culture;
        }

        [Test]
        public void CanConstructMutables()
        {
            var value = Mutable.Create(42);
            var value2 = Mutable.Create("Foo");

            Assert.AreEqual(42, value.Value);
            Assert.AreEqual("Foo", value2.Value);
        }

        [Test]
        public void SelectWorksOffSignal()
        {
            var value = Mutable.Create(42);
            var mapped = value.Select(v => (v + 2).ToString());

            Assert.AreEqual(42, value.Value);
            Assert.AreEqual("44", mapped.Value);
        }
    }
}