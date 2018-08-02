using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Aptiv.Messaging.Async.Testing
{
    [TestClass]
    public class BlockingReplyHandlingTests
    {
        [TestMethod]
        public void MatchAnyTest_1()
        {
            var brh = Setup();

            var test = "Hello, world!";
            var result = brh.SendAndMatchAny(test, 100, "*world?");

            Assert.AreEqual(result, test);
        }

        [TestMethod]
        public void MatchAnyTest_2()
        {
            Stopwatch sw = new Stopwatch();
            var brh = Setup();

            var test = "Hello, world!";

            int timeout = 50;
#if DEBUG
            int delta = 50;
#else
            int delta = 20;
#endif
            bool excepted = false;

            try
            {
                sw.Start();
                var result = brh.SendAndMatchAny(test, timeout, "Not Hello world?");
            }
            catch (TimeoutException) { sw.Stop(); excepted = true; }

            Assert.IsTrue(excepted);
            Assert.AreEqual(timeout, sw.ElapsedMilliseconds, delta);
        }

        [TestMethod]
        public void MatchAnyTest_3()
        {
            Stopwatch sw = new Stopwatch();
            var brh = Setup();

            var test = "Hello, world!";

            int timeout = 50;
#if DEBUG
            int delta = 50;
#else
            int delta = 20;
#endif
            bool excepted = false;

            try
            {
                sw.Start();
                var result = brh.SendAndMatchAny(test, timeout,
                    "Not Hello world?",
                    "Some other random pattern",
                    "Hello, world! (really close) XD");
            }
            catch (TimeoutException) { sw.Stop(); excepted = true; }

            Assert.IsTrue(excepted);
            Assert.AreEqual(timeout, sw.ElapsedMilliseconds, delta);
        }

        [TestMethod]
        public void MatchAnyTest_4()
        {
            var brh = Setup();

            var test = "Hello, world!";

            int timeout = 50;

            var result = brh.SendAndMatchAny(test, timeout,
                "Not Hello world?",
                "Some other random pattern",
                "Hello, world! (really close) XD",
                "Hello*");

            Assert.AreEqual(result, test);
        }

        [TestMethod]
        public void MatchAllTest_1()
        {
            var brh = Setup();

            var test = "Hi there!";
            var result = brh.SendAndMatchAll(test, 1000, test);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(test, result[0]);
        }

        [TestMethod]
        public void MatchAllTest_2()
        {
            var brh = Setup();

            var test = "Hi there!";
            var result = brh.SendAndMatchAll(test, 1000, "*", "?? ?????!", "**", test);

            Assert.AreEqual(4, result.Count);
            foreach (var r in result)
                Assert.AreEqual(test, r);
        }

        [TestMethod]
        public void MatchAllTest_3()
        {
            var brh = Setup();

            var test = "Hi there!";

            for (int i = 0; i < 1000; i++)
            {
                var result = brh.SendAndMatchAll(test, 1000, "*", "?? ?????!", "**", test);

                Assert.AreEqual(4, result.Count);
                foreach (var r in result)
                    Assert.AreEqual(test, r);
            }
        }

        [TestMethod]
        public void MatchAllTest_4()
        {
            var brh = Setup();

            var test = "Hi there!";

            for (int i = 0; i < 1000; i++)
            {
                var result = brh.SendAndMatchAll(test, 1000, "*", "?? ?????!", "**", test);

                Assert.AreEqual(4, result.Count);
                foreach (var r in result)
                    Assert.AreEqual(test, r);
            }
        }

        [TestMethod]
        public void CallOnMatchTest_1()
        {
            var bc = new BlockingCollection<IEnumerable<char>>();

            // Handle adds the received message to the queue.
            var eh = new Action<IEnumerable<char>>((args) =>
            {
                Debug.WriteLine("Received: " + args);
            });

            // Sending a message Invokes the OnReceive handle directly.
            void smd(IEnumerable<char> arg)
            {
                Debug.WriteLine("Sending: " + arg);

                foreach (var d in eh.GetInvocationList())
                    d.DynamicInvoke(arg);
            }

            var brh = new BlockingReplyHandling<char>(smd, ref eh, '?', '*');

            var l = new List<string>();
            var o = new object();

            var test = "Hi there!";
            int count = 100;

            using (brh.Call_On_Match("*", (arg) => 
            {
                lock (o) l.Add((string)arg);
            }, ref eh))
            {
                for (int i = 0; i < count; i++)
                    brh.SendAndMatchAny(test, -1, test);

                var t = new Task((async () => await Task.Delay(100)));
                t.Start();
                t.Wait();
            }

            Assert.AreEqual(100, l.Count);
            for (int i = 0; i < count; i++)
                Assert.AreEqual(test, l[i]);
        }

        [TestMethod]
        public void CallOnMatchTest_2()
        {
            #region setup
            var bc = new BlockingCollection<IEnumerable<char>>();

            // Handle adds the received message to the queue.
            var eh = new Action<IEnumerable<char>>((args) =>
            {
                Debug.WriteLine("Received: " + args);
            });

            // Sending a message Invokes the OnReceive handle directly.
            void smd(IEnumerable<char> arg)
            {
                Debug.WriteLine("Sending: " + arg);

                foreach (var d in eh.GetInvocationList())
                    d.DynamicInvoke(arg);
            }

            var brh = new BlockingReplyHandling<char>(smd, ref eh, '?', '*');
            #endregion

            var l = new List<string>();
            var o = new object();

            var test = "blah blah blah blah";
            int count = 100;

            using (brh.Call_On_Match("*", (arg) =>
            {
                lock (o) l.Add((string)arg);
            }, ref eh))
            {
                var t = new Task(() =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        var x = new Task((async () => await Task.Delay(10)));
                        x.Start();
                        x.Wait();
                        smd(test);
                    }
                });
                t.Start();
                t.Wait();
            }

            Assert.AreEqual(100, l.Count);
            for (int i = 0; i < count; i++)
                Assert.AreEqual(test, l[i]);
        }

        private BlockingReplyHandling<char> Setup()
        {
            var bc = new BlockingCollection<IEnumerable<char>>();

            // Handle adds the received message to the queue.
            var eh = new Action<IEnumerable<char>>((args) =>
            {
                Debug.WriteLine("Received: " + args);
            });

            // Sending a message Invokes the OnReceive handle directly.
            void smd(IEnumerable<char> arg)
            {
                Debug.WriteLine("Sending: " + arg);

                foreach (var d in eh.GetInvocationList())
                    d.DynamicInvoke(arg);
            }

            return new BlockingReplyHandling<char>(smd, ref eh, '?', '*');
        }
    }
}
