using Expecto;
using Expecto.CSharp;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Gjallarhorn.Helpers;

namespace Gjallarhorn.Linq.Tests
{
    public class SignalLinq
    {
        private static Test ActionTestCase(string name, Action action)
        {
            return Runner.TestCase(name, action);
        }

        [Tests]
        public static Test SiqnalLinq =
            Runner.TestList("SiqnalLinq", new Test[] {
                ActionTestCase("CanConstructMutables", CanConstructMutables),
                ActionTestCase("MutableUpdateWorks",  MutableUpdateWorks),
                ActionTestCase("MutableUpdateWorksWithCollection", MutableUpdateWorksWithCollection),
                ActionTestCase("CanSubscribeToSignalChanges",CanSubscribeToSignalChanges),
                ActionTestCase("CopyToTracksSignalChanges",CopyToTracksSignalChanges),
                ActionTestCase("SubscribeAndUpdateTracksSignalChanges",SubscribeAndUpdateTracksSignalChanges),
                ActionTestCase("SelectWorksOffSignal",SelectWorksOffSignal),
                Runner.TestCase("SelectAsyncWorksOffSignal", SelectAsyncWorksOffSignal()),
                Runner.TestCase("SelectAsyncTracksProperly", SelectAsyncTracksProperly()),
                ActionTestCase("SelectWorksWithQuerySyntax",SelectWorksWithQuerySyntax),
                ActionTestCase("SelectPropogatesChangesWithQuerySyntax",SelectPropogatesChangesWithQuerySyntax),
                ActionTestCase("SelectManyWorksWithQuerySyntax",SelectManyWorksWithQuerySyntax),
                ActionTestCase("SelectManyWithFiveValuesWorksWithQuerySyntax",SelectManyWithFiveValuesWorksWithQuerySyntax),
                ActionTestCase("SelectManyPropogatesChangesWithQuerySyntax",SelectManyPropogatesChangesWithQuerySyntax),
                ActionTestCase("ObservableToSignalPropogatesChanges",ObservableToSignalPropogatesChanges),
                ActionTestCase("SignalCombinePropogatesSuccessfully",SignalCombinePropogatesSuccessfully),
                ActionTestCase("SignalWherePropogatesCorrectly",SignalWherePropogatesCorrectly),
                ActionTestCase("SignalWhenFiltersProperly",SignalWhenFiltersProperly)
            });

        public static void CanConstructMutables()
        {
            var value = Mutable.Create(42);
            var value2 = Mutable.Create("Foo");

            Expect.equal(value.Value, 42, "should be equal");
            Expect.equal(value2.Value, "Foo", "should be equal");
        }

        public static void MutableUpdateWorks()
        {
            var value = Mutable.Create(0);

            Mutable.Update(value, o => o + 1);
            Mutable.Update(value, o => o - 3);

            Expect.equal(value.Value, -2, "should be equal");
        }

        public static void MutableUpdateWorksWithCollection()
        {
            var value = Mutable.Create(new List<int> { 42, 54 });

            Mutable.Update(value, (o => o.Concat(new[] { 21, 15 }).ToList()));

            Expect.sequenceEqual(value.Value, new[] { 42, 54, 21, 15 }, "should be equal");
        }

        public static void CanSubscribeToSignalChanges()
        {
            int sum = 0;

            var value = Mutable.Create(0);

            using (var _ = value.Subscribe(v => sum += v))
            {
                value.Value = 10;
                Expect.equal(sum, 10, "should be equal");
                value.Value = 5;
                Expect.equal(sum, 15, "should be equal");
            }

            // Shouldn't impact value
            value.Value = 20;
            Expect.equal(sum, 15, "should be equal");
        }

        public static void CopyToTracksSignalChanges()
        {
            var current = Mutable.Create(0);

            var value = Mutable.Create(0);

            using (var _ = value.CopyTo(current))
            {
                value.Value = 10;
                Expect.equal(current.Value, 10, "should be equal");
                value.Value = 15;
                Expect.equal(current.Value, 15, "should be equal");
            }

            // Shouldn't impact value
            value.Value = 20;
            Expect.equal(current.Value, 15, "should be equal");
        }

        public static void SubscribeAndUpdateTracksSignalChanges()
        {
            var sum = Mutable.Create(0);

            var value = Mutable.Create(0);

            using (var _ = value.SubscribeAndUpdate(sum, (o, v) => o + v))
            {
                value.Value = 10;
                Expect.equal(sum.Value, 10, "should be equal");
                value.Value = 5;
                Expect.equal(sum.Value, 15, "should be equal");
            }

            // Shouldn't impact value
            value.Value = 20;
            Expect.equal(sum.Value, 15, "should be equal");
        }

        public static void SelectWorksOffSignal()
        {
            var value = Mutable.Create(42);
            var mapped = value.Select(v => (v + 2).ToString());

            Expect.equal(value.Value, 42, "should be equal");
            Expect.equal(mapped.Value, "44", "should be equal");
        }

        public static async Task SelectAsyncWorksOffSignal()
        {
            var value = Mutable.Create(0);

            var mapped = value.SelectAsync(
                2,
                async v =>
                {
                    await Task.Delay(20);
                    return v + 2;
                });

            value.Value = 2;
            Expect.equal(value.Value, 2, "should be equal");
            // Mapped should be 2 immediately after setting
            Expect.equal(mapped.Value, 2, "should be equal");

            // Mapped should get updated async while this blocks
            await Task.Delay(50);
            Expect.equal(value.Value, 2, "should be equal");
            Expect.equal(mapped.Value, 4, "should be equal");
        }

        public static async Task SelectAsyncTracksProperly()
        {
            var tracker = new IdleTracker(System.Threading.SynchronizationContext.Current);

            var value = Mutable.Create(0);

            var mapped = value.SelectAsync(
                2,
                tracker,
                async v =>
                {
                    await Task.Delay(20);
                    return v + 2;
                });

            Expect.isTrue(tracker.Value, "tracker value should be true");
            value.Value = 2;

            Expect.equal(value.Value, 2, "should be equal");
            // Mapped should be 2 immediately after setting
            Expect.equal(mapped.Value, 2, "should be equal");
            // Give us a chance to start...
            await Task.Delay(10);
            Expect.isFalse(tracker.Value, "tracker value should be false");
            // And now let us finish
            await Task.Delay(100);

            Expect.isTrue(tracker.Value, "tracker value should be true");
            Expect.equal(value.Value, 2, "should be equal");
            Expect.equal(mapped.Value, 4, "should be equal");
        }

        public static void SelectWorksWithQuerySyntax()
        {
            var value = Mutable.Create(42);
            var mapped = from v in value
                         let newV = v + 2
                         select newV.ToString();

            Expect.equal(value.Value, 42, "should be equal");
            Expect.equal(mapped.Value, "44", "should be equal");
        }

        public static void SelectPropogatesChangesWithQuerySyntax()
        {
            var value = Mutable.Create(42);
            var mapped = from v in value
                         let newV = v + 2
                         select newV.ToString();

            Expect.equal(value.Value, 42, "should be equal");
            Expect.equal(mapped.Value, "44", "should be equal");
            value.Value = 55;

            Expect.equal(value.Value, 55, "should be equal");
            Expect.equal(mapped.Value, "57", "should be equal");
        }

        public static void SelectManyWorksWithQuerySyntax()
        {
            var value = Mutable.Create(42);
            var sv = Mutable.Create("Foo");

            var mapped = from v in value
                         from s in sv
                         let newV = s + v.ToString()
                         select newV;

            Expect.equal(value.Value, 42, "should be equal");
            Expect.equal(mapped.Value, "Foo42", "should be equal");
        }

        public static void SelectManyWithFiveValuesWorksWithQuerySyntax()
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

            Expect.equal(mapped.Value, "12345", "should be equal");
            value3.Value = 0;
            Expect.equal(mapped.Value, "12045", "should be equal");
            value2.Value = "Foo";
            value5.Value = "Bar";
            Expect.equal(mapped.Value, "1Foo04Bar", "should be equal");
        }

        public static void SelectManyPropogatesChangesWithQuerySyntax()
        {
            var value = Mutable.Create(42);
            var sv = Mutable.Create("Foo");

            var mapped = from v in value
                         from s in sv
                         let newV = s + v.ToString()
                         select newV;

            Expect.equal(value.Value, 42, "should be equal");
            Expect.equal(mapped.Value, "Foo42", "should be equal");
            value.Value = 55;
            sv.Value = "Bar";

            Expect.equal(value.Value, 55, "should be equal");
            Expect.equal(mapped.Value, "Bar55", "should be equal");
        }

        public static void ObservableToSignalPropogatesChanges()
        {
            var value = Mutable.Create(42);
            IObservable<int> obs = value;

            var signal = obs.ToSignal(0);
            Expect.equal(value.Value, 42, "should be equal");
            Expect.equal(signal.Value, 0, "should be equal");
            value.Value = 24;
            Expect.equal(value.Value, 24, "should be equal");
            Expect.equal(signal.Value, 24, "should be equal");
        }

        public static void SignalCombinePropogatesSuccessfully()
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
                            (a, b, c, d, e) => $"{a},{b},{c.ToString("N6", System.Globalization.CultureInfo.InvariantCulture)},{d},{e}");

            Expect.equal(signal.Value, "1,2,3.000000,4,5", "should be equal");
            v3.Value = 8;
            Expect.equal(signal.Value, "1,2,8.000000,4,5", "should be equal");
        }

        public static void SignalWherePropogatesCorrectly()
        {
            var m = Mutable.Create(1);
            var s = m.Select(v => 10 * v).Where(v => v < 100);
            var s2 = m.Select(v => 10 * v).Where(v => v > 50, 55);

            Expect.equal(s.Value, 10, "should be equal");
            Expect.equal(s2.Value, 55, "should be equal");
            m.Value = 5;
            Expect.equal(s.Value, 50, "should be equal");
            Expect.equal(s2.Value, 55, "should be equal");
            m.Value = 25;
            Expect.equal(s.Value, 50, "should be equal");
            Expect.equal(s2.Value, 250, "should be equal");
            m.Value = 7;
            Expect.equal(s.Value, 70, "should be equal");
            Expect.equal(s2.Value, 70, "should be equal");
        }

        public static void SignalWhenFiltersProperly()
        {
            var guard = Mutable.Create(true);

            var input = Mutable.Create(0);

            var output = input.When(guard);

            Expect.equal(output.Value, 0, "should be equal");

            input.Value = 1;
            Expect.equal(output.Value, 1, "should be equal");
            // Set guard to false will prevent updates
            guard.Value = false;
            input.Value = 2;
            Expect.equal(output.Value, 1, "should be equal");

            // Set guard to true will update
            guard.Value = true;
            Expect.equal(output.Value, 2, "should be equal");
        }
    }
}
