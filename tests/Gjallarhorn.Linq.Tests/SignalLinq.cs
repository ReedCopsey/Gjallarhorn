using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Gjallarhorn;
using Gjallarhorn.Linq;
using Gjallarhorn.Helpers;

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
        public void MutableUpdateWorks()
        {
            var value = Mutable.Create(0);

            Mutable.Update(value, o => o + 1);
            Mutable.Update(value, o => o - 3);

            Assert.AreEqual(-2, value.Value);
        }

        [Test]
        public void MutableUpdateWorksWithCollection()
        {
            var value = Mutable.Create(new List<int> { 42, 54 });

            Mutable.Update(value, (o => o.Concat(new[] {21, 15}).ToList()));

            CollectionAssert.AreEqual(new[] {42,54,21,15}, value.Value);
        }

        [Test]
        public void CanSubscribeToSignalChanges()
        {
            int sum = 0;

            var value = Mutable.Create(0);

            using (var _ = value.Subscribe(v => sum += v))
            {
                value.Value = 10;
                Assert.AreEqual(10, sum);
                value.Value = 5;
                Assert.AreEqual(15, sum);
            }
            
            // Shouldn't impact value
            value.Value = 20;
            Assert.AreEqual(15, sum);            
        }

        [Test]
        public void CopyToTracksSignalChanges()
        {
            var current = Mutable.Create(0);

            var value = Mutable.Create(0);
            
            using (var _ = value.CopyTo(current))
            {                
                value.Value = 10;
                Assert.AreEqual(10, current.Value);
                value.Value = 15;
                Assert.AreEqual(15, current.Value);
            }

            // Shouldn't impact value
            value.Value = 20;
            Assert.AreEqual(15, current.Value);
        }

        [Test]
        public void SubscribeAndUpdateTracksSignalChanges()
        {
            var sum = Mutable.Create(0);

            var value = Mutable.Create(0);

            using (var _ = value.SubscribeAndUpdate(sum, (o,v) => o + v))
            {
                value.Value = 10;
                Assert.AreEqual(10, sum.Value);
                value.Value = 5;
                Assert.AreEqual(15, sum.Value);
            }

            // Shouldn't impact value
            value.Value = 20;
            Assert.AreEqual(15, sum.Value);
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
        public async Task SelectAsyncWorksOffSignal()
        {
            var value = Mutable.Create(0);

            var mapped = value.SelectAsync(
                2, 
                async v => {
                    await Task.Delay(20);
                    return v + 2;
                });

            value.Value = 2;
            Assert.AreEqual(2, value.Value);
            // Mapped should be 2 immediately after setting
            Assert.AreEqual(2, mapped.Value);

            // Mapped should get updated async while this blocks
            await Task.Delay(50);
            Assert.AreEqual(2, value.Value);
            Assert.AreEqual(4, mapped.Value);
        }

        [Test]
        public async Task SelectAsyncTracksPropertly()
        {
            var tracker = new IdleTracker(System.Threading.SynchronizationContext.Current);

            var value = Mutable.Create(0);

            var mapped = value.SelectAsync(
                2,
                tracker,
                async v => {
                    await Task.Delay(30);
                    return v + 2;
                });

            Assert.IsTrue(tracker.Value);
            value.Value = 2;

            Assert.AreEqual(2, value.Value);
            // Mapped should be 2 immediately after setting
            Assert.AreEqual(2, mapped.Value);
            Assert.IsFalse(tracker.Value);

            // Mapped should get updated async while this blocks
            await Task.Delay(50);
            Assert.IsTrue(tracker.Value);
            Assert.AreEqual(2, value.Value);
            Assert.AreEqual(4, mapped.Value);
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