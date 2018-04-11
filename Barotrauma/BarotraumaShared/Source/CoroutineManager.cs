﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Barotrauma
{
    enum CoroutineStatus
    {
        Running, Success, Failure
    }

    class CoroutineHandle
    {
        public readonly IEnumerator<object> Coroutine;
        public readonly string Name;

        public CoroutineHandle(IEnumerator<object> coroutine, string name = "")
        {
            Coroutine = coroutine;
            Name = string.IsNullOrWhiteSpace(name) ? coroutine.ToString() : name;
        }

    }

    // Keeps track of all running coroutines, and runs them till the end.
    static class CoroutineManager
    {
        static readonly List<CoroutineHandle> Coroutines = new List<CoroutineHandle>();

        public static float UnscaledDeltaTime, DeltaTime;

        public static CoroutineHandle StartCoroutine(IEnumerable<object> func, string name = "")
        {
            var handle = new CoroutineHandle(func.GetEnumerator(), name);
            Coroutines.Add(handle);

            return handle;
        }

        public static void InvokeAfter(Action action, float delay)
        {
            StartCoroutine(DoInvokeAfter(action, delay));
        }

        private static IEnumerable<object> DoInvokeAfter(Action action, float delay)
        {
            if (action == null)
            {
                yield return CoroutineStatus.Failure;
            }

            yield return new WaitForSeconds(delay);

            action();

            yield return CoroutineStatus.Success;
        }


        public static bool IsCoroutineRunning(string name)
        {
            return Coroutines.Any(c => c.Name == name);
        }

        public static bool IsCoroutineRunning(CoroutineHandle handle)
        {
            return Coroutines.Contains(handle);
        }
        
        public static void StopCoroutines(string name)
        {
            Coroutines.RemoveAll(c => c.Name == name);
        }

        public static void StopCoroutines(CoroutineHandle handle)
        {
            Coroutines.RemoveAll(c => c == handle);
        }
        private static bool IsDone(CoroutineHandle handle)
        {
            try
            {
                if (handle.Coroutine.Current != null)
                {
                    WaitForSeconds wfs = handle.Coroutine.Current as WaitForSeconds;
                    if (wfs != null)
                    {
                        if (!wfs.CheckFinished(UnscaledDeltaTime)) return false;
                    }
                    else
                    {
                        switch ((CoroutineStatus)handle.Coroutine.Current)
                        {
                            case CoroutineStatus.Success:
                                return true;

                            case CoroutineStatus.Failure:
                                DebugConsole.ThrowError("Coroutine \"" + handle.Name + "\" has failed");
                                return true;
                        }
                    }
                }

                handle.Coroutine.MoveNext();
                return false;
            }
            catch (Exception e)
            {
                DebugConsole.ThrowError("Coroutine " + handle.Name + " threw an exception: " + e.Message + "\n" + e.StackTrace.ToString());
                return true;
            }
        }
        // Updating just means stepping through all the coroutines
        public static void Update(float unscaledDeltaTime, float deltaTime)
        {
            UnscaledDeltaTime = unscaledDeltaTime;
            DeltaTime = deltaTime;

            foreach (var x in Coroutines.ToList())
                if(IsDone(x))
                    Coroutines.Remove(x);
        }
    }
  
    class WaitForSeconds
    {
        float timer;

        public WaitForSeconds(float time)
        {
            timer = time;
        }

        public bool CheckFinished(float deltaTime) 
        {
            timer -= deltaTime;
            return timer<=0.0f;
        }
    }


}
