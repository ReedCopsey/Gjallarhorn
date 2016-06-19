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

        [Test]
        public void SelectWorksWithQuerySyntax()
        {
            var value = Mutable.Create(42);
            var mapped = from v in value
                         let newV = v + 2
                         select newV.ToString();

            Assert.AreEqual(42, value.Value);
            Assert.AreEqual("44", mapped.Value);
        }

        [Test]
        public void SelectPropogatesChangesWithQuerySyntax()
        {
            var value = Mutable.Create(42);
            var mapped = from v in value
                         let newV = v + 2
                         select newV.ToString();

            Assert.AreEqual(42, value.Value);
            Assert.AreEqual("44", mapped.Value);
            value.Value = 55;

            Assert.AreEqual(55, value.Value);
            Assert.AreEqual("57", mapped.Value);
        }

        [Test]
        public void SelectManyWorksWithQuerySyntax()
        {
            var value = Mutable.Create(42);
            var sv = Mutable.Create("Foo");

            var mapped = from v in value
                         from s in sv
                         let newV = s + v.ToString()
                         select newV;

            Assert.AreEqual(42, value.Value);
            Assert.AreEqual("Foo42", mapped.Value);
        }

        [Test]
        public void SelectManyWithFiveValuesWorksWithQuerySyntax()
        {
            var value1 = Mutable.Create(1);
            var value2 = Mutable.Create("2");
            var value3 = Mutable.Create(3);
            var value4 = Mutable.Create(4);
            var value5 = Mutable.Create("5");

            var mapped = from a in value1
                         from b in value2
                         from c in value3
                         from d in value4
                         from e in value5
                         select $"{a}{b}{c}{d}{e}";

            Assert.AreEqual("12345", mapped.Value);
            value3.Value = 0;
            Assert.AreEqual("12045", mapped.Value);
            value2.Value = "Foo";
            value5.Value = "Bar";
            Assert.AreEqual("1Foo04Bar", mapped.Value);
        }

        [Test]
        public void SelectManyPropogatesChangesWithQuerySyntax()
        {
            var value = Mutable.Create(42);
            var sv = Mutable.Create("Foo");

            var mapped = from v in value
                         from s in sv
                         let newV = s + v.ToString()
                         select newV;

            Assert.AreEqual(42, value.Value);
            Assert.AreEqual("Foo42", mapped.Value);

            value.Value = 55;
            sv.Value = "Bar";

            Assert.AreEqual(55, value.Value);
            Assert.AreEqual("Bar55", mapped.Value);
        }
    }
}