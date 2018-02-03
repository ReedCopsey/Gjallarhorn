﻿using NUnit.Framework;
using System;
using System.Threading.Tasks;
using Gjallarhorn.Linq;
using Gjallarhorn.Helpers;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

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
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
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
        public async Task SelectAsyncTracksProperly()
        {
            var tracker = new IdleTracker(System.Threading.SynchronizationContext.Current);

            var value = Mutable.Create(0);

            var mapped = value.SelectAsync(
                2,
                tracker,
                async v => {
                    await Task.Delay(20);
                    return v + 2;
                });

            Assert.IsTrue(tracker.Value);
            value.Value = 2;

            Assert.AreEqual(2, value.Value);
            // Mapped should be 2 immediately after setting
            Assert.AreEqual(2, mapped.Value);

            // Give us a chance to start...
            await Task.Delay(10);
            Assert.IsFalse(tracker.Value);

            // And now let us finish
            await Task.Delay(100);

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

        [Test]
        public void ObservableToSignalPropogatesChanges()
        {
            var value = Mutable.Create(42);
            IObservable<int> obs = value;

            var signal = obs.ToSignal(0);
            Assert.AreEqual(42, value.Value);
            Assert.AreEqual(0, signal.Value);

            value.Value = 24;
            Assert.AreEqual(24, value.Value);
            Assert.AreEqual(24, signal.Value);
        }

        [Test]
        public void SignalCombinePropogatesSuccessfully()
        {
            var v1 = Mutable.Create(1);
            var v2 = Mutable.Create(2L);
            var v3 = Mutable.Create(3.0);
            var v4 = Mutable.Create("4");
            var v5 = Mutable.Create(5);
            
            var signal = Signal.Combine(
                            v1, 
                            v2, 
                            v3, 
                            v4, 
                            v5, 
                            (a, b, c, d, e) => $"{a},{b},{c:N6},{d},{e}" );

            Assert.AreEqual("1,2,3.000000,4,5", signal.Value);
            v3.Value = 8;
            Assert.AreEqual("1,2,8.000000,4,5", signal.Value);
        }

        [Test]

        public void SignalWherePropogatesCorrectly()
        {
            var m = Mutable.Create(1);
            var s = m.Select(v => 10 * v).Where(v => v < 100);
            var s2 = m.Select(v => 10 * v).Where(v => v > 50, 55);

            Assert.AreEqual(10, s.Value);
            Assert.AreEqual(55, s2.Value);
            m.Value = 5;
            Assert.AreEqual(50, s.Value);
            Assert.AreEqual(55, s2.Value);
            m.Value = 25;
            Assert.AreEqual(50, s.Value);
            Assert.AreEqual(250, s2.Value);
            m.Value = 7;
            Assert.AreEqual(70, s.Value);
            Assert.AreEqual(70, s2.Value);
        }

        [Test]
        public void SignalWhenFiltersProperly()
        {
            var guard = Mutable.Create(true);

            var input = Mutable.Create(0);

            var output = input.When(guard);

            Assert.AreEqual(0, output.Value);

            input.Value = 1;
            Assert.AreEqual(1, output.Value);

            // Set guard to false will prevent updates
            guard.Value = false;
            input.Value = 2;
            Assert.AreEqual(1, output.Value);

            // Set guard to true will update
            guard.Value = true;
            Assert.AreEqual(2, output.Value);
        }
    }
}