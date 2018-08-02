using Aptiv.Messages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Aptiv.Messaging.Async.Testing
{
    [TestClass]
    public class AsyncReplyHandlingTests
    {
        [TestMethod]
        public async Task MatchAnyTest_1()
        {
            var brh = Setup();

            var test = "Hello, world!";
            var result = await brh.SendAndMatchAny(test, 100, "*world?");

            Assert.AreEqual(result, test);
        }

        [TestMethod]
        public async Task MatchAnyTest_2()
        {
            Stopwatch sw = new Stopwatch();
            var brh = Setup();

            var test = "Hello, world!";

            int timeout = 50;
            bool excepted = false;

            try
            {
                sw.Start();
                var result = await brh.SendAndMatchAny(test, timeout, "Not Hello world?");
            }
            catch (TimeoutException) { sw.Stop(); excepted = true; }

            Assert.IsTrue(excepted);
            Assert.AreEqual(timeout, sw.ElapsedMilliseconds, 20);
        }

        [TestMethod]
        public async Task MatchAnyTest_3()
        {
            Stopwatch sw = new Stopwatch();
            var brh = Setup();

            var test = "Hello, world!";

            int timeout = 50;
            bool excepted = false;

            try
            {
                sw.Start();
                var result = await brh.SendAndMatchAny(test, timeout,
                    "Not Hello world?",
                    "Some other random pattern",
                    "Hello, world! (really close) XD");
            }
            catch (TimeoutException) { sw.Stop(); excepted = true; }

            Assert.IsTrue(excepted);
            Assert.AreEqual(timeout, sw.ElapsedMilliseconds, 20);
        }

        [TestMethod]
        public async Task MatchAnyTest_4()
        {
            var brh = Setup();

            var test = "Hello, world!";

            int timeout = 50;

            var result = await brh.SendAndMatchAny(test, timeout,
                "Not Hello world?",
                "Some other random pattern",
                "Hello, world! (really close) XD",
                "Hello*");

            Assert.AreEqual(result, test);
        }

        [TestMethod]
        public async Task MatchAllTest_1()
        {
            var brh = Setup();

            var test = "Hi there!";
            var result = await brh.SendAndMatchAll(test, 1000, test);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(test, result[0]);
        }

        [TestMethod]
        public async Task MatchAllTest_2()
        {
            var brh = Setup();

            var test = "Hi there!";
            var result = await brh.SendAndMatchAll(test, 1000, "*", "?? ?????!", "**", test);

            Assert.AreEqual(4, result.Count);
            foreach (var r in result)
                Assert.AreEqual(test, r);
        }

        [TestMethod]
        public async Task MatchAllTest_3()
        {
            var brh = Setup();

            var test = "Hi there!";

            for (int i = 0; i < 1000; i++)
            {
                var result = await brh.SendAndMatchAll(test, 1000, "*", "?? ?????!", "**", test);

                Assert.AreEqual(4, result.Count);
                foreach (var r in result)
                    Assert.AreEqual(test, r);
            }
        }

        [TestMethod]
        public async Task MatchAllTest_4()
        {
            var brh = Setup();

            var test = "Hi there!";

            for (int i = 0; i < 1000; i++)
            {
                var result = await brh.SendAndMatchAll(test, 1000, "*", "?? ?????!", "**", test);

                Assert.AreEqual(4, result.Count);
                foreach (var r in result)
                    Assert.AreEqual(test, r);
            }
        }

        private AsyncReplyHandling<char> Setup()
        {
            // Handle adds the received message to the queue.
            var eh = new EventHandler<MessageReceivedArgs<IEnumerable<char>>>((obj, args) => { });

            // Sending a message Invokes the OnReceive handle directly.
            void smd(IEnumerable<char> arg)
            {
                var args = new MessageReceivedArgs<IEnumerable<char>>(arg);
                eh.DynamicInvoke(this, args);
            }

            return new AsyncReplyHandling<char>(smd, ref eh, '?', '*');
        }
    }
}
