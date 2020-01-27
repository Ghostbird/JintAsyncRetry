using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jint;
using Jint.Native;
using Jint.Runtime.Interop;

namespace Demo
{
    class Program
    {
        public static Thrower Thrower = new Thrower();
        public static Retry Retry = new Retry();
        static void Main(string[] args)
        {
            new RetryJsRepository().Test();
            Task.Delay(10000).Wait();
        }
    }

    public class BaseJsRepository {
        
        protected Engine engine = new Engine();

        public void Test() {
            engine.SetValue("retryTest", new ClrFunctionInstance(engine, "retryTest", RetryTest));
            engine.Execute("retryTest('foo', 5)");
            engine.Execute("retryTest('bar', 3)");
            engine.Execute("retryTest('tre', 1)");
        }
        public virtual JsValue RetryTest(JsValue self, JsValue[] args) {
            Console.WriteLine(JsValue.FromObject(engine, Program.Thrower.Throw(args[0].AsString(), args[1].AsNumber())));
            return JsValue.Null;
        }
    }

    public class RetryJsRepository : BaseJsRepository{
        public override JsValue RetryTest(JsValue self, JsValue[] args)
        {
            Program.Retry.EnQueue(() => Task.Run(() => base.RetryTest(self, args)));
            return JsValue.FromObject(engine, "Enqueued...");
        }
    }

    public class Thrower
    {
        private Dictionary<string, int> cache = new Dictionary<string, int>();

        public Thrower() { }

        public string Throw(string key, double throwCount) {
            var count = 0;
            if (!cache.TryGetValue(key, out count) || count < throwCount) {
                cache[key] = ++count;
                throw new Exception($"Throwing {key}: {count}/{throwCount}");
            }
            return $"Not throwing {key} after {throwCount} times.";
        }
    }


    public class Retry
    {
        private SemaphoreSlim semaphore = new SemaphoreSlim(1);
        public Queue<Func<Task>> Queue { get; private set; } = new Queue<Func<Task>>();

        public Retry()
        {
            StartAsTask();
        }

        private void StartAsTask()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    if (Queue.Count > 0)
                    {
                        try
                        {
                            await Queue.Peek()();
                            Queue.Dequeue();
                        }
                        catch (Exception e)
                        {
                            Console.Error.WriteLine(e.Message);
                            // Retry in the future
                            await Task.Delay(1000);
                        }
                    }
                    else
                    {
                        await semaphore.WaitAsync();
                    }
                }
            });
        }

        public void EnQueue(Func<Task> taskFn)
        {
            Queue.Enqueue(taskFn);
            try {
                semaphore.Release();
            } catch { }
        }
    }
}
